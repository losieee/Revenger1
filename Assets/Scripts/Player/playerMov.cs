using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System;
using TMPro;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMov : MonoBehaviour
{
    [Header("Component")]
    public Rigidbody rb;
    public GameObject gameClearUI;
    public GameObject gameOverUI;
    public GameObject missionUI;
    public GameObject optionUI;
    public TMP_Text nearNPC;
    private Animator animator;
    public GameObject weapon;
    public GameObject weaponChangePanel;
    public GameObject minimap1fPicture;
    public GameObject[] enemies1f;
    public GameObject laundryPuzzle;

    [Header("Move and Rotate")]
    public float speed = 5f;
    private float currentMoveSpeed = 0f;
    public float rotSpeed = 5f;
    public float runSpeed = 3f;
    public Transform cameraPivot;
    int donRunZoneCount = 0;

    [Header("Attack")]
    private bool isAssassinating = false;
    private EnemyMov pendingAssassination = null;
    [SerializeField] private float assassinateApproachDuration = 0.20f;  // 적 뒤로 붙는 시간
    [SerializeField] private float assassinateRotLerp = 20f;             // 회전 보간속도

    [Header("E Cooldown")]
    [SerializeField] private float eCooldownDuration = 0.6f;
    private bool eLocked = false;
    private Coroutine eLockCo;

    // 외부에서 읽을 수 있도록 공개(추가)
    public bool IsELocked => eLocked;

    private IEnumerator ELock(float sec)
    {
        eLocked = true;
        yield return new WaitForSecondsRealtime(sec); // 타임스케일 0이어도 흐름 유지
        eLocked = false;
        eLockCo = null;
    }
    private void ArmELock(float sec = -1f)
    {
        if (sec <= 0f) sec = eCooldownDuration;
        if (eLockCo != null) StopCoroutine(eLockCo); // 새로 누르면 쿨타임 리셋
        eLockCo = StartCoroutine(ELock(sec));
    }
    bool EPressed()
    {
        if (eLocked) return false;                 // 잠금 중이면 무시
        if (KeyBindings.GetKeyDown(GameAction.Interaction))
        {
            ArmELock();                            // 누른 순간 코루틴 락 시작
            return true;
        }
        return false;
    }

    public Vector3 currentMoveInput;
    private bool isRunning;
    private bool canAttack;
    private bool canTakeMission;
    private bool canRun = true;
    private bool canKill = false;

    private float moveX, moveY, velX, velY;
    private float smoothTime = 0.05f;

    // 바닥 감지 (BoxCollider 기반 + 코요테 타임)
    private BoxCollider box;
    private EnemyMov killTarget = null;        // 암살 대상
    private float boxGroundExtra = 0.1f;       // 바닥까지 여유 캐스트 거리
    private float edgeProbeOffset = 0.18f;     // 앞/뒤/좌/우 보조 프로브 오프셋
    private float groundedCoyoteTime = 0.12f;  // 유예 시간
    private float groundedTimer = 0f;

    [Header("Floor detect")]
    [SerializeField] private float minAirTimeForLand = 0.12f; // 최소 공중시간
    [SerializeField] private float landMinDownVel = -0.4f;    // 하강속도 임계
    private float timeSinceLeftGround = 999f;                  // 떠난 뒤 경과시간

    [Header("Climb")]
    public float climbDuration = 3.25f;
    public float climbCheckDistance = 2.0f;
    public LayerMask climbableLayer;
    private bool canClimbZone = false;
    private bool isClimbing = false;
    private bool blockInput = false;
    private float lastBoxWallRemainingHeight = 0f;

    [Header("Sound Range")]
    public float walkDetectRange = 6f;
    public float runDetectRange = 12f;
    public LayerMask aiLayerMask;

    [Header("Climb Holding")]
    [HideInInspector] public float detectedWallHeight = 0f;
    [HideInInspector] public float remainingWallHeight = 0f;
    private bool isHolding = false;
    private bool canStartClimb = false;
    private bool isLerpingHoldOffset = false;
    private Vector3 holdLerpStartPos, holdLerpTargetPos;
    private Quaternion holdLerpStartRot, holdLerpTargetRot;
    private float holdLerpTimer = 0f;
    private float holdLerpDuration = 0.1f;
    private Vector3 holdingStartPos;
    private bool hasPendingWall = false;
    private Vector3 pendingWallPoint;
    private Vector3 pendingWallNormal;
    private float pendingWallTopY;

    [SerializeField] private float attachHoldDistanceFromWall = 0.14f; // 벽에서 떨어져 붙는 거리
    [SerializeField] private float attachLerpDuration = 0.12f;         // 붙을 때 보간 시간

    private Vector3 climbStartPos, climbTargetPos;
    private Quaternion climbStartRot, climbTargetRot;
    private float climbTimer = 0f;

    [Header("Climb Cancle")]
    private bool isCancellingHold = false;
    [SerializeField] private bool holdCancelAllowed;
    public void SetHoldCancelAllowed(bool allowed) => holdCancelAllowed = allowed;
    public void AllowHoldCancel() { holdCancelAllowed = true; }
    public void BlockHoldCancel() { holdCancelAllowed = false; }

    // 점프 / 낙하
    private float verticalVelocity = 0f;
    private bool isLanding = false;
    private float landingTimer = 0f;
    private float landingDelay = 0.6f;
    private bool isGrounded = true;
    private bool wasGroundedLastFrame = true;
    private float airMultiplier;
    private bool ignoreGroundedCheck = false;
    private float ignoreGroundedTimer = 0f;
    private float ignoreDurationAfterJump = 0.25f; // 점프 직후 잠깐 지면 판정 무시
    private float fallTimer = 0f;
    [SerializeField] private float fallDebounce = 0.10f; // 100ms

    public LayerMask groundLayer;
    [Range(0f, 1f)] public float groundMinNormalY = 0.55f;

    [Header("Front wall check")]
    public float frontCheckDistance = 0.35f;
    public float wallPushStrength = 2.0f;

    // Alt 이동
    private Vector3 savedForward, savedRight;
    private bool wasAltPressedLastFrame, justReleasedAlt;

    [Header("Crouching")]
    private bool isCrouching = false;
    [SerializeField] private float crouchCooldown = 0.6f;
    private float crouchCooldownTimer = 0f;
    [SerializeField] float crouchHeight = 1.0f;
    [SerializeField] float colliderLerpTime = 0.10f;
    Vector3 boxSizeStand, boxCenterStand;
    Vector3 boxSizeCrouch, boxCenterCrouch;
    Coroutine crouchColRoutine;

    // Tag 기반 벽 근접 차단(Keep-Out)
    [Header("Wall Keep-Out (by Tag)")]
    [SerializeField] private string wallTag = "Wall";
    [SerializeField] private float wallKeepOutRadius = 0.4f;
    [SerializeField] private int wallKeepOutIterations = 2;
    [SerializeField] private float wallKeepOutSkin = 0.01f;
    [SerializeField] private float wallKeepOutUnderFootTolerance = 0.03f;

    [Header("Door")]
    [SerializeField] private float doorRotateDuration = 0.6f;
    [SerializeField] private float doorOpenAngleY = -90f;

    private bool nearDoor = false;
    private Transform nearDoorRoot;
    private Transform nearDoorLeaf;
    private bool doorOpen = false;
    private bool isDoorRotating = false;
    private Quaternion doorClosedRot;
    private Quaternion doorOpenRot;
    private Coroutine doorRoutine;

    // 무기 바꾸기 관련
    public bool canWeaponSwitch = false;
    private GameObject boxObject;
    public float weaponPanelDelay = 0.7f;           // 박스 연 후 패널 띄우기까지 지연
    Coroutine _openWeaponPanelCo;
    bool _weaponPickFlowActive = false;             // 중복 입력 방지
    private int _selectedWeaponChildIndex = -1;     // 0=Gun, 1=Crowbar

    // 무기 선택
    private bool choiceWeapon;

    // RightHandGrip 애니메이션 레이어 제어
    private int gripLayer;
    private int gripIdleHash;
    private int gripBatPoseHash;
    private int gripGunPoseHash;
    // RightArm 애니메이션 레이어 제어
    private int rightArmLayer;
    private float rightArmMaxWeight = 0.61f;
    private float rightArmDefaultWeight = 0.61f; // 기본 복구값
    private Coroutine _rightArmLerpCo;

    [Header("Minimap UI")]
    public GameObject minimapPanel;
    public GameObject miniPos;
    public float miniPosYOffset = -30f;
    public bool minimapStartsHidden = true;

    float _sceneInputGraceTimer = 0f;

    [Header("세탁실 미션")]
    private bool hasLaundryMission = false;
    public Transform laundryCamTarget;          // 세탁실 미션 카메라 위치
    public float laundryCamBlend = 1.0f;        // 카메라 이동(변환) 시간
    private bool inLaundryRange = false;
    private bool isCamBlending = false;
    private CameraMov cmov;
    private bool isLaundryView = false;         // 전용 뷰에 들어와 있는지
    public float restoreCamBlend = 0.4f;        // 복귀시 리센터 시간
    private Vector3 _preViewCamPos;
    private Quaternion _preViewCamRot;
    private float _preViewCamFov;
    private Coroutine _camBlendRoutine;

    [Header("휴게실 미션")]
    private bool hasFoyerMission = false;
    private bool inFoyerRange = false;
    public Transform foyerCamTarget;
    private float foyerCamBlend = 1.0f;
    private bool isFoyerView = false;         // 전용 뷰에 들어와 있는지


    [Header("미션 간 보이는 Mask")]
    public string[] laundryViewLayers = new[] { "Default", "Ground" , "FoyerPuzzle"};

    private int _savedCamMask;
    private bool _hasSavedCamMask = false;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnemyMov.OnAnyEnemyKilled += HandleEnemyKilled;
        KeyBindings.OnChanged += RefreshInteractionHint;
        WeaponManager.OnWeaponChosen += ApplyChosenWeaponImmediate;
    }
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EnemyMov.OnAnyEnemyKilled -= HandleEnemyKilled;
        KeyBindings.OnChanged -= RefreshInteractionHint;
        WeaponManager.OnWeaponChosen -= ApplyChosenWeaponImmediate;
        var cam = Camera.main;
        if (cam && _hasSavedCamMask) { cam.cullingMask = _savedCamMask; _hasSavedCamMask = false; }
    }

    GameObject[] Panels() => new[] { missionUI, gameClearUI, gameOverUI, optionUI, weaponChangePanel };

    void CloseAllPlayerUI()
    {
        foreach (var p in Panels()) HidePausePanel(p);
        if (nearNPC) 
        {
            RefreshInteractionHint();
            nearNPC.gameObject.SetActive(false); 
        }
        Input.ResetInputAxes();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindSceneUI();

        donRunZoneCount = 0; UpdateRunLock();
        canAttack = false; killTarget = null; choiceWeapon = false; canTakeMission = false;

        nearDoor = false; nearDoorRoot = null; nearDoorLeaf = null;

        canClimbZone = false; isHolding = false; isClimbing = false; blockInput = false;

        CloseAllPlayerUI();

        _sceneInputGraceTimer = 0.2f;

        bool isMenu = scene.name == "MainLobby";
        Cursor.visible = isMenu;
        Cursor.lockState = isMenu ? CursorLockMode.None : CursorLockMode.Locked;
        if (!isMenu) { AudioListener.pause = false; Time.timeScale = 1f; }

        RebindMinimapAndEnemies();

        var cam = Camera.main;
        if (cam && _hasSavedCamMask) { cam.cullingMask = _savedCamMask; _hasSavedCamMask = false; }
    }

    private void HandleEnemyKilled(Transform deadTr)
    {
        var dead = deadTr ? deadTr.GetComponent<EnemyMov>() : null;
        if (dead == null) return;

        if (pendingAssassination == dead)
        {
            pendingAssassination = null;
            isAssassinating = false;
            blockInput = false;
            animator.ResetTrigger("AttackCrowbar");
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        if (killTarget == dead)
        {
            killTarget = null;
            canKill = false;
        }
    }

    bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void Awake() => RebindSceneUI();
    void RebindSceneUI()
    {
        var canvas = GetComponentInChildren<Canvas>(true);
        missionUI = missionUI && missionUI.scene.IsValid() ? missionUI : canvas?.transform.Find("MissionImg")?.gameObject ?? GameObject.FindWithTag("MissionUI");
        optionUI = optionUI && optionUI.scene.IsValid() ? optionUI : canvas?.transform.Find("OptionPop")?.gameObject ?? GameObject.Find("OptionPop");
        gameOverUI = gameOverUI && gameOverUI.scene.IsValid() ? gameOverUI : canvas?.transform.Find("GameOver")?.gameObject ?? GameObject.Find("GameOver");
        weaponChangePanel = weaponChangePanel && weaponChangePanel.scene.IsValid() ? weaponChangePanel : canvas?.transform.Find("Weapon_Choice_Panel")?.gameObject ?? GameObject.Find("Weapon_Choice_Panel");
        if (nearNPC == null || !nearNPC.gameObject.scene.IsValid())
        {
            nearNPC = canvas?.transform.Find("NearNPC")?.GetComponent<TMPro.TMP_Text>()
                   ?? GameObject.Find("NearNPC")?.GetComponent<TMPro.TMP_Text>();
            if (nearNPC) nearNPC.gameObject.SetActive(false);
        }
        RefreshInteractionHint();
    }

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        Time.timeScale = 1f;

        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        cmov = cameraPivot ? cameraPivot.GetComponent<CameraMov>() : null;

        gripLayer = animator.GetLayerIndex("RightHandGrip");
        gripIdleHash = Animator.StringToHash("RightHandGrip.Idle State");
        gripBatPoseHash = Animator.StringToHash("RightHandGrip.BatPose");
        gripGunPoseHash = Animator.StringToHash("RightHandGrip.GunPose");
        rightArmLayer = animator.GetLayerIndex("RightArm");
        RebindMinimapAndEnemies();

        if (gripLayer >= 0) animator.SetLayerWeight(gripLayer, 1f);
        if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, 0f);
        if (minimapPanel) minimapPanel.SetActive(!minimapStartsHidden);

        box = GetComponent<BoxCollider>();

        // 서있는 값 저장
        boxSizeStand = box.size;
        boxCenterStand = box.center;
        canTakeMission = false;

        // 앉은 값 계산(바닥 고정)
        float newH = crouchHeight;
        boxSizeCrouch = new Vector3(box.size.x, newH, box.size.z);
        float deltaH = box.size.y - newH;
        boxCenterCrouch = new Vector3(box.center.x, box.center.y - deltaH * 0.5f, box.center.z);

        groundLayer = LayerMask.GetMask("Ground", "Climbable");

        if (!cameraPivot)
        {
            var cam = FindObjectOfType<CameraMov>(true);
            if (cam) cameraPivot = cam.transform;
            else if (Camera.main) cameraPivot = Camera.main.transform;
        }
    }

    void RebindMinimapAndEnemies()
    {
        minimap1fPicture = GameObject.Find("1FMapPicture");
        enemies1f = GameObject.FindGameObjectsWithTag("Enemy1F");
    }

    void Update()
    {
        if (_sceneInputGraceTimer > 0f) _sceneInputGraceTimer -= Time.deltaTime;

        // 바닥 감지
        isGrounded = CheckGrounded();

        if (wasGroundedLastFrame && !isGrounded) timeSinceLeftGround = 0f;
        if (!isGrounded) timeSinceLeftGround += Time.deltaTime;

        if (crouchCooldownTimer > 0f) crouchCooldownTimer -= Time.deltaTime;

        // 붙기 보간 중이면 Update 로직 스킵
        if (isLerpingHoldOffset)
        {
            holdLerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(holdLerpTimer / holdLerpDuration);
            transform.position = Vector3.Lerp(holdLerpStartPos, holdLerpTargetPos, t);
            transform.rotation = Quaternion.Slerp(holdLerpStartRot, holdLerpTargetRot, t);
            if (t >= 1f)
            {
                isLerpingHoldOffset = false;
                rb.useGravity = false;
                rb.isKinematic = true;
            }
            return;
        }

        // 전용 뷰 상태일 때 (세탁실 미션뷰)
        if (isLaundryView || isFoyerView)
        {
            // 매 프레임 안전하게 멈춤 유지
            currentMoveInput = Vector3.zero;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);
            blockInput = true;

            // E 또는 ESC로 빠져나오기
            if (EPressed() || Input.GetKeyDown(KeyCode.Escape))
            {
                ExitLaundryView();
            }

            // 전용 뷰 동안은 아래 일반 Update 로직 막음
            return;
        }

        // 매달린 상태
        if (isHolding)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            bool isHoldingState = st.IsName("Holding") || st.IsTag("Holding");
            if (animator.IsInTransition(0)) isHoldingState = false;

            bool canCancelNow = holdCancelAllowed && !isLerpingHoldOffset && canStartClimb && isHoldingState;

            if (canCancelNow && !isCancellingHold && KeyBindings.GetKeyDown(GameAction.Back))
                StartCoroutine(CancelHoldAndReturn());

            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);

            if (KeyBindings.GetKeyDown(GameAction.Climb) && canStartClimb)
                StartClimbFromHold(0.52f);
            return;
        }

        // 오르는 중
        if (isClimbing)
        {
            climbTimer += Time.deltaTime;
            float t = Mathf.Clamp01(climbTimer / climbDuration);
            transform.position = Vector3.Lerp(climbStartPos, climbTargetPos, t);
            transform.rotation = Quaternion.Slerp(climbStartRot, climbTargetRot, t);

            currentMoveInput = Vector3.zero;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);

            if (t >= 1f)
            {
                isClimbing = false;
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
            }
            return;
        }

        // 입력
        isRunning = canRun && KeyBindings.GetKey(GameAction.Run) && !isCrouching;
        bool isAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        float h = KeyBindings.GetAxisHorizontal();
        float v = KeyBindings.GetAxisVertical();

        Vector3 camForward = cameraPivot.forward;
        Vector3 camRight = cameraPivot.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();

        if (isAlt && !wasAltPressedLastFrame) { savedForward = camForward; savedRight = camRight; }

        if (wasAltPressedLastFrame && !isAlt)
        {
            cameraPivot.GetComponent<CameraMov>()?.RecenterToPlayerSmooth(0.2f);
            justReleasedAlt = true;
        }

        Vector3 moveForward = (isAlt || justReleasedAlt) ? savedForward : camForward;
        Vector3 moveRight = (isAlt || justReleasedAlt) ? savedRight : camRight;

        // 시체 픽업할땐 못움직임
        if (blockInput)
        {
            currentMoveInput = Vector3.zero;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);
        }

        // 1) 일반 이동
        if (!blockInput)
        {
            Vector3 targetMoveInput = (moveForward * v + moveRight * h).normalized;
            float lerpSpeed = (isGrounded && !isLanding) ? 15f : 5f;
            currentMoveInput = Vector3.Lerp(currentMoveInput, targetMoveInput, Time.deltaTime * lerpSpeed);

            if (currentMoveInput.magnitude < 0.05f)
                currentMoveInput = Vector3.zero;
        }
        else if (blockInput)
        {
            currentMoveInput = Vector3.zero;
        }

        // 애니 파라미터
        Vector3 localMove = transform.InverseTransformDirection(currentMoveInput);
        moveX = Mathf.SmoothDamp(moveX, localMove.x, ref velX, smoothTime);
        moveY = Mathf.SmoothDamp(moveY, localMove.z, ref velY, smoothTime);
        animator.SetFloat("MoveX", moveX);
        animator.SetFloat("MoveY", moveY);

        float speedParam = (isGrounded && currentMoveInput.magnitude > 0.05f)
            ? (isRunning ? 1f : 0.5f)
            : 0f;
        animator.SetFloat("Speed", speedParam, 0.1f, Time.deltaTime);
        if (speedParam == 0f)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
        }

        // 회전
        if (currentMoveInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * 100f * Time.deltaTime);
        }

        wasAltPressedLastFrame = isAlt;
        if (justReleasedAlt && !isAlt) justReleasedAlt = false;

        // 벽 잡기 시작
        if (KeyBindings.GetKeyDown(GameAction.Climb) && canClimbZone && !isHolding && !isClimbing)
        {
            Vector3 dir = transform.forward;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

            if (Physics.Raycast(rayOrigin, dir, out RaycastHit wall, climbCheckDistance, climbableLayer))
            {
                float wallTopY = wall.collider.bounds.max.y;
                float wallBottomY = wall.collider.bounds.min.y;
                float wallHeight = wallTopY - wallBottomY;

                detectedWallHeight = wallHeight;

                float jumpHeight = Mathf.Clamp(wallHeight * 0.5f, 0.4f, 2.0f);
                float rayStartY = Mathf.Min(transform.position.y + jumpHeight, wallTopY - 0.1f);
                Vector3 rayStart = new Vector3(transform.position.x, rayStartY, transform.position.z);

                if (Physics.Raycast(rayStart, dir, out RaycastHit wallHit, climbCheckDistance, climbableLayer))
                {
                    StartHolding(wallHit);
                }
            }
        }

        // Grounded 무시 타이머
        if (ignoreGroundedCheck)
        {
            ignoreGroundedTimer -= Time.deltaTime;
            if (ignoreGroundedTimer <= 0f) ignoreGroundedCheck = false;
        }

        // 낙하 감지
        verticalVelocity = rb.velocity.y;
        bool wantFall = (verticalVelocity < -0.1f) && !isGrounded;

        if (wantFall) fallTimer += Time.deltaTime;
        else fallTimer = 0f;

        bool isFallingAnim = fallTimer > fallDebounce;
        animator.SetBool("IsFalling", isFallingAnim);

        // 착지 트리거
        if (!wasGroundedLastFrame && isGrounded
            && timeSinceLeftGround >= minAirTimeForLand
            && verticalVelocity <= landMinDownVel
            && !ignoreGroundedCheck)
        {
            animator.SetTrigger("Land");
            animator.SetBool("IsJumping", false);
            animator.SetBool("IsFalling", false);
            fallTimer = 0f;
            isLanding = true;
            landingTimer = landingDelay;
        }

        if (isLanding)
        {
            landingTimer -= Time.deltaTime;
            if (landingTimer <= 0f) isLanding = false;
        }

        wasGroundedLastFrame = isGrounded;

        // C 눌러 앉기
        if (KeyBindings.GetKeyDown(GameAction.Crouch) && crouchCooldownTimer <= 0f)
        {
            bool wantCrouch = !isCrouching;

            if (!wantCrouch && !CanStandUp()) return;

            isCrouching = wantCrouch;
            animator.SetBool("IsCrouching", isCrouching);
            crouchCooldownTimer = crouchCooldown;

            SoundManager.i?.PlaySFX(PlayerSfx.CrouchToggle, SfxBus.Effect, 1f);
            ApplyCrouchCollider(isCrouching);
        }

        // 속도
        float moveSpeed = isRunning ? speed * runSpeed : speed;
        if (isCrouching) moveSpeed *= 0.6f;
        currentMoveSpeed = moveSpeed;

        // 소리 범위 알림
        CheckNearbyEnemies();

        // 클리어
        if (canAttack && _sceneInputGraceTimer <= 0f && !IsPointerOverUI() && KeyBindings.GetKeyDown(GameAction.Attack))
            ShowPausePanel(gameClearUI);

        // 미니맵
        if (Input.GetKeyDown(KeyCode.Tab)) minimapPanel?.SetActive(true);
        if (Input.GetKeyUp(KeyCode.Tab)) minimapPanel?.SetActive(false);

        // 미션 받기
        if (canTakeMission && EPressed()) ShowPausePanel(missionUI);

        // ESC 옵션 토글
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (RebindKeyButton.IsAnyListening)
            {
                RebindKeyButton.CancelAll();
                return;
            }

            if (missionUI && missionUI.activeSelf) { HidePausePanel(missionUI); return; }
            if (gameClearUI && gameClearUI.activeSelf) { HidePausePanel(gameClearUI); return; }
            if (gameOverUI && gameOverUI.activeSelf) { HidePausePanel(gameOverUI); return; }
            if (weaponChangePanel && weaponChangePanel.activeSelf) { HidePausePanel(weaponChangePanel); return; }
            if (optionUI && optionUI.activeSelf) { HidePausePanel(optionUI); return; }
            ShowPausePanel(optionUI);
        }

        // 암살
        if (_sceneInputGraceTimer <= 0f && !IsPointerOverUI() && KeyBindings.GetKeyDown(GameAction.Attack) && canKill)
        {
            if (canKill && killTarget != null)
            {
                StartAssassination(killTarget);
            }
            else if (!AnyPauseOpen()) // 클리어/옵션 UI 떠 있으면 무시
            {
                PlayAttackByWeapon();
            }
        }

        // 문열기
        if (nearDoor && EPressed() && nearDoorLeaf != null && !isDoorRotating)
        {
            Quaternion target = doorOpen ? doorClosedRot : doorOpenRot;
            if (doorRoutine != null) StopCoroutine(doorRoutine);
            doorRoutine = StartCoroutine(RotateLocalY_Smooth(nearDoorLeaf, nearDoorLeaf.localRotation, target, doorRotateDuration));
            doorOpen = !doorOpen;
        }

        // 무기 선택창
        if (choiceWeapon && EPressed())
        {
            if (IsAnyWeaponActive())
                ForceUnequipWeapon();

            if (weaponChangePanel && weaponChangePanel.activeSelf)
            {
                HidePausePanel(weaponChangePanel);
                _weaponPickFlowActive = false;
                if (_openWeaponPanelCo != null) { StopCoroutine(_openWeaponPanelCo); _openWeaponPanelCo = null; }
                return;
            }

            if (_weaponPickFlowActive) return; // 진행 중이면 무시
            _weaponPickFlowActive = true;

            ButtonControl button = transform.GetChild(1).GetChild(0).GetChild(1).GetComponent<ButtonControl>();
            button.canNextStage = true;

            blockInput = true;
            currentMoveInput = Vector3.zero;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);

            animator.ResetTrigger("WeaponPick");
            animator.SetTrigger("WeaponPick");
        }

        // 무기 스위치 가능해지면 패널 닫기
        if (WeaponManager.i && WeaponManager.i.canCrowbarSwitch && !canWeaponSwitch)
        {
            canWeaponSwitch = true;
            if (weaponChangePanel && weaponChangePanel.activeSelf) HidePausePanel(weaponChangePanel);
        }

        // 무기 바꾸기
        if (Input.GetKeyDown(KeyCode.Alpha1) && canWeaponSwitch)    // 맨손
        {
            if (gripLayer >= 0) animator.CrossFade(gripIdleHash, 0.1f, gripLayer, 0f);
            if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, 0f);

            if (weapon)
            {
                weapon.SetActive(false);
                for (int i = 0; i < weapon.transform.childCount; i++)
                    weapon.transform.GetChild(i).gameObject.SetActive(false);
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha2) && canWeaponSwitch)    // 무기
        {
            if (WeaponManager.i == null) return;

            // 선택이 없으면 동작 X
            if (_selectedWeaponChildIndex < 0) return;

            // 포즈 적용
            switch (WeaponManager.i.SelectedWeapon)
            {
                case WeaponManager.WeaponType.Gun:
                    if (gripLayer >= 0) animator.CrossFade(gripGunPoseHash, 0.1f, gripLayer, 0f);
                    if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, rightArmMaxWeight);
                    break;

                case WeaponManager.WeaponType.Crowbar:
                    if (gripLayer >= 0) animator.CrossFade(gripBatPoseHash, 0.1f, gripLayer, 0f);
                    if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, rightArmMaxWeight);
                    break;

                case WeaponManager.WeaponType.Bat:
                    if (gripLayer >= 0) animator.CrossFade(gripBatPoseHash, 0.1f, gripLayer, 0f);
                    if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, rightArmMaxWeight);
                    break;
            }

            if (weapon)
            {
                weapon.SetActive(true);
                for (int i = 0; i < weapon.transform.childCount; i++)
                    weapon.transform.GetChild(i).gameObject.SetActive(i == _selectedWeaponChildIndex);
            }
        }

        // 세탁실 미션 카메라 시점 이동
        if (inLaundryRange && hasLaundryMission && EPressed() && !isCamBlending && laundryCamTarget)
        {
            EnterLaundryView();
            StartCoroutine(BlendMainCameraTo(laundryCamTarget, laundryCamBlend));
        }

        // 휴게실 미션
        if(inFoyerRange && hasFoyerMission && EPressed() && !isCamBlending && foyerCamTarget)
        {
            EnterFoyerView();
            StartCoroutine(BlendMainCameraTo(foyerCamTarget, foyerCamBlend));
        }
    }

    // 세탁실 미션 시점 변경 시 플레이어 고정
    void EnterLaundryView()
    {
        // 조작 잠금
        blockInput = true;
        currentMoveInput = Vector3.zero;
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 0f);
        animator.SetFloat("Speed", 0f);
        rb.velocity = Vector3.zero;

        // 현재 메인 카메라 포즈 저장
        var cam = Camera.main;
        if (cam)
        {
            _preViewCamPos = cam.transform.position;
            _preViewCamRot = cam.transform.rotation;
            _preViewCamFov = cam.fieldOfView;
        }

        // 카메라 추적 비활성화
        if (cmov) cmov.enabled = false;

        // Culling Mask 설정
        if (cam)
        {
            if (!_hasSavedCamMask) { _savedCamMask = cam.cullingMask; _hasSavedCamMask = true; }
            cam.cullingMask = LayerMask.GetMask(laundryViewLayers); // "Default","Ground"만 보이게
        }

        isLaundryView = true;
        laundryPuzzle.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (!EventSystem.current)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // 휴게실 시점
    void EnterFoyerView()
    {
        // 조작 잠금
        blockInput = true;
        currentMoveInput = Vector3.zero;
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 0f);
        animator.SetFloat("Speed", 0f);
        rb.velocity = Vector3.zero;

        // 현재 메인 카메라 포즈 저장
        var cam = Camera.main;
        if (cam)
        {
            _preViewCamPos = cam.transform.position;
            _preViewCamRot = cam.transform.rotation;
            _preViewCamFov = cam.fieldOfView;
        }

        // 카메라 추적 비활성화
        if (cmov) cmov.enabled = false;

        // Culling Mask 설정
        if (cam)
        {
            if (!_hasSavedCamMask) { _savedCamMask = cam.cullingMask; _hasSavedCamMask = true; }
            cam.cullingMask = LayerMask.GetMask(laundryViewLayers); // "Default","Ground"만 보이게
        }

        isFoyerView = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (!EventSystem.current)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    public void ExitLaundryView()
    {
        // 진행 중인 블렌드가 있으면 중단
        if (_camBlendRoutine != null) { StopCoroutine(_camBlendRoutine); _camBlendRoutine = null; }

        // Culling Mask 먼저 원복 (복귀 중에도 월드를 다시 보이게)
        var cam = Camera.main;
        if (cam && _hasSavedCamMask)
        {
            cam.cullingMask = _savedCamMask;
            _hasSavedCamMask = false;
        }

        // 어떤 미션이든 열려있던 퍼즐 UI 닫기
        if (laundryPuzzle && laundryPuzzle.activeSelf) HidePausePanel(laundryPuzzle);

        // 저장해둔 포즈로 부드럽게 되돌아간 뒤 CameraMov를 켠다
        _camBlendRoutine = StartCoroutine(BlendBackThenEnableFollow(restoreCamBlend));

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private IEnumerator BlendBackThenEnableFollow(float duration)
    {
        var cam = Camera.main;
        if (!cam) { FinishExitLaundryView(); yield break; }

        Transform tr = cam.transform;
        Vector3 sPos = tr.position;
        Quaternion sRot = tr.rotation;
        float sFov = cam.fieldOfView;

        duration = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float k = Mathf.SmoothStep(0f, 1f, t);

            tr.position = Vector3.Lerp(sPos, _preViewCamPos, k);
            tr.rotation = Quaternion.Slerp(sRot, _preViewCamRot, k);
            cam.fieldOfView = Mathf.Lerp(sFov, _preViewCamFov, k);
            yield return null;
        }

        // 최종 스냅
        tr.position = _preViewCamPos;
        tr.rotation = _preViewCamRot;
        cam.fieldOfView = _preViewCamFov;

        // 이제 추적 재개 (여기서는 리센터 호출 X : 스냅 방지)
        if (cmov) cmov.enabled = true;

        FinishExitLaundryView();
    }

    void FinishExitLaundryView()
    {
        blockInput = false;
        isLaundryView = false;
        isFoyerView = false;
        isCamBlending = false;
        _camBlendRoutine = null;
    }

    // 세탁실 미션 카메라 이동
    private IEnumerator BlendMainCameraTo(Transform target, float duration)
    {
        isCamBlending = true;

        // 따라다니는 카메라 스크립트가 있으면 잠깐 꺼두기
        if (cmov) cmov.enabled = false;

        var cam = Camera.main;
        if (!cam) { isCamBlending = false; yield break; }

        Transform camTr = cam.transform;

        Vector3 startPos = camTr.position;
        Quaternion startRot = camTr.rotation;
        float startFov = cam.fieldOfView;

        // 목표 카메라의 FOV가 있으면 맞춰줌
        float targetFov = startFov;
        var targetCam = target.GetComponent<Camera>();
        if (targetCam) targetFov = targetCam.fieldOfView;

        Vector3 endPos = target.position;
        Quaternion endRot = target.rotation;

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);

        // 타임스케일 영향을 안 받게 Unscaled로 진행 (UI가 잠깐 열려도 부드럽게)
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float k = Mathf.SmoothStep(0f, 1f, t); // 더 부드러운 가속/감속 곡선

            camTr.position = Vector3.Lerp(startPos, endPos, k);
            camTr.rotation = Quaternion.Slerp(startRot, endRot, k);
            cam.fieldOfView = Mathf.Lerp(startFov, targetFov, k);

            yield return null;
        }

        // 최종 스냅
        camTr.position = endPos;
        camTr.rotation = endRot;
        cam.fieldOfView = targetFov;

        isCamBlending = false;

        // 필요하면 다시 카메라 추적 켜기 (여기서는 계속 그 자리에 머무르게 그대로 둠)
        // if (cmov) cmov.enabled = true;
    }

    // 상자를 열기 전 무기를 들고있으면 비활성화
    private bool IsAnyWeaponActive()
    {
        if (!weapon || !weapon.activeSelf) return false;
        for (int i = 0; i < weapon.transform.childCount; i++)
        {
            if (weapon.transform.GetChild(i).gameObject.activeSelf) return true;
        }
        return false;
    }

    private void ForceUnequipWeapon()
    {
        // 애니: 맨손 포즈
        if (gripLayer >= 0) animator.CrossFade(gripIdleHash, 0.1f, gripLayer, 0f);
        if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, 0f);

        // 실제 오브젝트 비활성화
        if (weapon)
        {
            for (int i = 0; i < weapon.transform.childCount; i++)
                weapon.transform.GetChild(i).gameObject.SetActive(false);

            // 완전 숨기고 싶으면 false, 루트는 켜두고 싶으면 true 유지
            weapon.SetActive(false);
        }
    }

    public void AE_WeaponPick_Box()
    {
        // 박스 열기 (PlayBox)
        if (boxObject) boxObject.GetComponentInChildren<BoxOpen>()?.PlayBox();

        // 1초(weaponPanelDelay) 뒤 패널 오픈
        if (_openWeaponPanelCo != null) StopCoroutine(_openWeaponPanelCo);
        _openWeaponPanelCo = StartCoroutine(OpenWeaponPanelAfter(weaponPanelDelay));
    }

    IEnumerator OpenWeaponPanelAfter(float delay)
    {
        // 타임스케일 0 영향을 받지 않도록 Realtime로 대기
        yield return new WaitForSecondsRealtime(delay);

        ShowPausePanel(weaponChangePanel);

        _openWeaponPanelCo = null;
    }

    // 공격 (암살)
    private void StartAssassination(EnemyMov enemy)
    {
        if (enemy == null) return;

        enemy.FreezeForAssassination(true);

        isAssassinating = true;
        pendingAssassination = enemy;

        canKill = false;
        killTarget = null;

        blockInput = true;
        currentMoveInput = Vector3.zero;
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 0f);
        animator.SetFloat("Speed", 0f);

        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        pendingAssassination.PrepareForAssassination(true);
        StartCoroutine(AssassinationApproachRoutine(enemy));
    }

    private IEnumerator AssassinationApproachRoutine(EnemyMov enemy)
    {
        if (enemy == null) yield break;
        Transform et = enemy.transform;

        Vector3 startPos = transform.position;
        float t = 0f;
        float dur = Mathf.Max(0.01f, assassinateApproachDuration);

        while (t < 1f)
        {
            if (enemy == null || !et) yield break;

            t += Time.deltaTime / dur;

            transform.position = startPos;

            Vector3 toEnemy = et.position - transform.position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude > 1e-6f)
            {
                Quaternion lookRot = Quaternion.LookRotation(toEnemy, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, lookRot,
                    assassinateRotLerp * 100f * Time.deltaTime
                );
            }

            yield return null;
        }

        Vector3 finalDir = et.position - transform.position;
        finalDir.y = 0f;
        if (finalDir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(finalDir, Vector3.up);

        TriggerAttackByCurrentWeapon();
    }

    void LateUpdate()
    {
        UpdateMiniPos();

        bool inTwoFloor = false;
        if (Range2F.i != null) inTwoFloor = Range2F.i.inTwoFloor;

        if (inTwoFloor)
        {
            if (minimap1fPicture) minimap1fPicture.SetActive(false);
            if (enemies1f != null)
                foreach (var enemy in enemies1f)
                    if (enemy) enemy.SetActive(false);
        }
        else
        {
            if (minimap1fPicture) minimap1fPicture.SetActive(true);
            if (enemies1f != null)
                foreach (var enemy in enemies1f)
                    if (enemy) enemy.SetActive(true);
        }
    }

    void UpdateMiniPos()
    {
        if (!miniPos) return;
        Vector3 p = transform.position;
        miniPos.transform.position = new Vector3(p.x, p.y + miniPosYOffset, p.z);
    }

    private IEnumerator RotateLocalY_Smooth(Transform tr, Quaternion from, Quaternion to, float duration)
    {
        isDoorRotating = true;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            tr.localRotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }
        tr.localRotation = to;
        isDoorRotating = false;
    }

    void ApplyCrouchCollider(bool crouch)
    {
        if (!box) return;
        if (crouchColRoutine != null) StopCoroutine(crouchColRoutine);

        Vector3 targetSize = crouch ? boxSizeCrouch : boxSizeStand;
        Vector3 targetCenter = crouch ? boxCenterCrouch : boxCenterStand;

        crouchColRoutine = StartCoroutine(LerpCollider(box, targetSize, targetCenter, colliderLerpTime));
    }

    IEnumerator LerpCollider(BoxCollider bc, Vector3 toSize, Vector3 toCenter, float dur)
    {
        Vector3 fromSize = bc.size;
        Vector3 fromCenter = bc.center;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, dur);
            bc.size = Vector3.Lerp(fromSize, toSize, t);
            bc.center = Vector3.Lerp(fromCenter, toCenter, t);
            yield return new WaitForFixedUpdate();
        }
        bc.size = toSize;
        bc.center = toCenter;
    }

    bool CanStandUp()
    {
        float standTop = boxCenterStand.y + boxSizeStand.y * 0.5f;
        float crouchTop = boxCenterCrouch.y + boxSizeCrouch.y * 0.5f;
        float deltaTop = standTop - crouchTop;
        if (deltaTop <= 0.001f) return true;

        float sliceCenterLocalY = (standTop + crouchTop) * 0.5f;
        Vector3 localCenter = new Vector3(boxCenterStand.x, sliceCenterLocalY, boxCenterStand.z);
        Vector3 worldCenter = transform.TransformPoint(localCenter);

        Vector3 half = new Vector3(
            boxSizeStand.x * 0.5f * transform.lossyScale.x,
            deltaTop * 0.5f * transform.lossyScale.y,
            boxSizeStand.z * 0.5f * transform.lossyScale.z
        );

        var hits = Physics.OverlapBox(worldCenter, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (!h || h.isTrigger) continue;
            if (h.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    IEnumerator CancelHoldAndReturn()
    {
        if (isCancellingHold) yield break;
        isCancellingHold = true;

        holdCancelAllowed = false;

        float dur = 0.18f;
        float t = 0f;
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;

        blockInput = true;
        animator.SetBool("Hold", false);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            transform.position = Vector3.Lerp(fromPos, holdLerpStartPos, t);
            transform.rotation = Quaternion.Slerp(fromRot, holdLerpStartRot, t);
            yield return null;
        }

        isHolding = false;
        isLerpingHoldOffset = false;
        rb.isKinematic = false;
        rb.useGravity = true;

        blockInput = false;

        isCancellingHold = false;
    }

    private void ApplyWallPush(ref Vector3 velocity, RaycastHit front)
    {
        bool isGroundLike = ((1 << front.collider.gameObject.layer) & groundLayer) != 0
                            && front.normal.y >= groundMinNormalY;
        if (isGroundLike) return;

        if (!front.collider.CompareTag(wallTag)) return;

        Vector3 horizNormal = new Vector3(front.normal.x, 0f, front.normal.z);
        if (horizNormal.sqrMagnitude > 0.0001f)
        {
            horizNormal.Normalize();
            velocity += horizNormal * wallPushStrength;
        }

        ignoreGroundedCheck = true;
        ignoreGroundedTimer = Mathf.Max(ignoreDurationAfterJump, minAirTimeForLand);
    }

    private void ClearLandTriggers() => animator.ResetTrigger("Land");

    private bool CheckGrounded()
    {
        if (ignoreGroundedCheck) return false;

        bool touching = BoxGroundProbeMulti();
        groundedTimer = touching ? groundedCoyoteTime : groundedTimer - Time.deltaTime;
        return groundedTimer > 0f;
    }

    void FixedUpdate()
    {
        bool block = isClimbing || isHolding;
        if (block) return;

        airMultiplier = isGrounded ? 1f : 0.5f;
        Vector3 move = currentMoveInput * currentMoveSpeed * airMultiplier * Time.fixedDeltaTime;

        Vector3 newPos = rb.position + move;

        EnforceWallKeepOut(ref newPos);

        Vector3 pos = rb.position;
        SlideCast(ref pos, newPos, 0.3f, box.size.y * transform.lossyScale.y, groundLayer);
        rb.MovePosition(newPos);
    }

    bool SlideCast(ref Vector3 from, Vector3 to, float radius, float height, LayerMask mask, float skin = 0.02f)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 1e-4f) return false;
        dir /= dist;

        Vector3 p1 = from + Vector3.up * (radius);
        Vector3 p2 = from + Vector3.up * (height - radius);

        if (Physics.CapsuleCast(p1, p2, radius, dir, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
        {
            from = hit.point + hit.normal * skin;
            Vector3 remain = (to - from);
            Vector3 slide = Vector3.ProjectOnPlane(remain, hit.normal);
            from += slide;
            return true;
        }
        return false;
    }

    private void EnforceWallKeepOut(ref Vector3 pos)
    {
        float footY = box ? box.bounds.min.y : transform.position.y;

        for (int it = 0; it < wallKeepOutIterations; it++)
        {
            Collider[] hits = Physics.OverlapSphere(
                pos,
                wallKeepOutRadius + wallKeepOutSkin,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            bool adjusted = false;

            foreach (var col in hits)
            {
                if (!col) continue;
                if (col.isTrigger) continue;
                if (col.attachedRigidbody == rb) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (!col.CompareTag(wallTag)) continue;

                Vector3 query = new Vector3(pos.x, footY, pos.z);
                Vector3 cp = col.ClosestPoint(query);
                if (cp.y <= footY - wallKeepOutUnderFootTolerance ||
                    col.bounds.max.y <= footY - wallKeepOutUnderFootTolerance)
                {
                    continue;
                }

                Vector3 delta = new Vector3(pos.x - cp.x, 0f, pos.z - cp.z);
                float d = delta.magnitude;
                if (d < wallKeepOutRadius)
                {
                    Vector3 n;
                    if (d > 1e-4f) n = delta / d;
                    else
                    {
                        Vector3 fallback = new Vector3(pos.x - col.bounds.center.x, 0f, pos.z - col.bounds.center.z);
                        n = (fallback.sqrMagnitude > 1e-6f) ? fallback.normalized : transform.forward;
                    }

                    float push = (wallKeepOutRadius - d) + wallKeepOutSkin;
                    pos += n * push;
                    adjusted = true;
                }
            }

            if (!adjusted) break;
        }
    }

    // 무기 선택 시 즉시 적용 함수
    private void ApplyChosenWeaponImmediate(WeaponManager.WeaponType type)
    {
        if (!weapon || !animator) return;

        // 선택된 무기 child index 정하기 (요구사항: 0=Gun, 1=Crowbar)
        switch (type)
        {
            case WeaponManager.WeaponType.Gun:
                _selectedWeaponChildIndex = 0;
                // 포즈: 총
                if (gripLayer >= 0) animator.CrossFade(gripGunPoseHash, 0.1f, gripLayer, 0f);
                if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, rightArmMaxWeight);
                break;

            case WeaponManager.WeaponType.Crowbar:
                _selectedWeaponChildIndex = 1;
                // 포즈: 빠루
                if (gripLayer >= 0) animator.CrossFade(gripBatPoseHash, 0.1f, gripLayer, 0f);
                if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, rightArmMaxWeight);
                break;

            case WeaponManager.WeaponType.Bat:
                _selectedWeaponChildIndex = 2;
                // 포즈: Bat
                if (gripLayer >= 0) animator.CrossFade(gripBatPoseHash, 0.1f, gripLayer, 0f);
                rightArmDefaultWeight = 0.85f;
                rightArmMaxWeight = rightArmDefaultWeight;

                if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, rightArmMaxWeight);
                break;

            default:
                _selectedWeaponChildIndex = -1;
                if (gripLayer >= 0) animator.CrossFade(gripIdleHash, 0.1f, gripLayer, 0f);
                if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, 0f);
                break;
        }

        // 무기 루트는 켜고, 선택한 것만 켜기
        weapon.SetActive(true);
        for (int i = 0; i < weapon.transform.childCount; i++)
            weapon.transform.GetChild(i).gameObject.SetActive(i == _selectedWeaponChildIndex);

        // 스위치 가능 플래그도 열어주기 (버튼 누르면 패널 닫히는 기존 흐름 유지)
        canWeaponSwitch = true;

        if (weaponChangePanel && weaponChangePanel.activeSelf)
            HidePausePanel(weaponChangePanel);

        _weaponPickFlowActive = false;
    }

    private bool BoxGroundProbeMulti()
    {
        if (!box) return false;

        Vector3 center = box.transform.TransformPoint(box.center);
        Vector3 lossy = box.transform.lossyScale;

        Vector3 half = new Vector3(
            Mathf.Abs(box.size.x * 0.5f * lossy.x),
            Mathf.Abs(box.size.y * 0.5f * lossy.y),
            Mathf.Abs(box.size.z * 0.5f * lossy.z)
        );

        Vector3 halfShrink = new Vector3(
            Mathf.Max(half.x - 0.01f, 0.001f),
            Mathf.Max(half.y - 0.01f, 0.001f),
            Mathf.Max(half.z - 0.01f, 0.001f)
        );

        float skin = 0.02f;
        Vector3 start = center + Vector3.up * skin;
        float distance = half.y + boxGroundExtra + skin;
        Quaternion rot = box.transform.rotation;

        bool Probe(Vector3 o)
        {
            if (Physics.BoxCast(o, halfShrink, Vector3.down, out RaycastHit hit, rot, distance, groundLayer, QueryTriggerInteraction.Ignore))
                return hit.normal.y >= groundMinNormalY;
            return false;
        }

        if (Probe(start)) return true;

        Vector3 fwd = transform.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = transform.right; right.y = 0; right.Normalize();
        float off = edgeProbeOffset;

        if (Probe(start + fwd * off)) return true;
        if (Probe(start - fwd * off)) return true;
        if (Probe(start + right * off)) return true;
        if (Probe(start - right * off)) return true;

        return false;
    }

    void StartHolding(RaycastHit hit)
    {
        ClearLandTriggers();
        isCrouching = false;
        animator.SetBool("IsCrouching", false);

        float wallTop = hit.collider.bounds.max.y;
        float footY = box ? box.bounds.min.y : transform.position.y;
        float wallHeight = Mathf.Max(0f, wallTop - footY);

        remainingWallHeight = wallHeight;
        detectedWallHeight = wallHeight;

        if (wallHeight <= 1.0f)
        {
            StartBoxJump(hit.point, hit.normal, wallHeight);
            return;
        }

        blockInput = true;
        isHolding = false;
        isLerpingHoldOffset = false;

        hasPendingWall = true;
        pendingWallPoint = hit.point;
        pendingWallNormal = hit.normal;
        pendingWallTopY = wallTop;

        animator.SetTrigger("Hold");
        holdCancelAllowed = false;
    }

    public void AE_AttachToWall()
    {
        if (!hasPendingWall) return;
        hasPendingWall = false;

        isHolding = true;

        Vector3 targetPos = pendingWallPoint + pendingWallNormal * attachHoldDistanceFromWall;
        targetPos.y = transform.position.y;
        Quaternion targetRot = Quaternion.LookRotation(-pendingWallNormal);

        holdLerpStartPos = transform.position;
        holdLerpTargetPos = targetPos;
        holdLerpStartRot = transform.rotation;
        holdLerpTargetRot = targetRot;
        holdLerpTimer = 0f;
        holdLerpDuration = attachLerpDuration;
        isLerpingHoldOffset = true;

        holdingStartPos = targetPos;
        holdCancelAllowed = false;
    }

    public void StartClimbFromHold(float duration)
    {
        ClearLandTriggers();
        isHolding = false;
        canStartClimb = false;
        climbTimer = 0f;
        climbDuration = duration;

        climbStartPos = transform.position;
        climbStartRot = transform.rotation;

        float climbHeight = Mathf.Clamp(detectedWallHeight + 0.15f, 1f, 3.5f);

        climbTargetPos = holdingStartPos + Vector3.up * climbHeight;
        climbTargetRot = transform.rotation;

        animator.SetBool("Hold", false);
        animator.SetTrigger("Climb");

        isClimbing = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DonRun")) { donRunZoneCount++; UpdateRunLock(); }
        if (other.CompareTag("ClimbZone")) canClimbZone = true;
        if (other.CompareTag("Boss")) canAttack = true;

        if (other.CompareTag("NPC"))
        {
            nearNPC.gameObject.SetActive(true);
            canTakeMission = true;
        }

        if (other.CompareTag("Discorver")) ShowPausePanel(gameOverUI);

        if (other.CompareTag("Attack"))
        {
            var enemy = other.GetComponentInParent<EnemyMov>() ?? other.GetComponent<EnemyMov>();
            if (enemy != null) { killTarget = enemy; canKill = true; }
        }

        if (other.CompareTag("Door"))
        {
            nearDoor = true;

            nearDoorRoot = other.transform.root;
            nearDoorLeaf = (nearDoorRoot.childCount > 0) ? nearDoorRoot.GetChild(0) : nearDoorRoot;

            doorClosedRot = Quaternion.Euler(0f, 0f, 0f);
            doorOpenRot = Quaternion.Euler(0f, doorOpenAngleY, 0f);

            float yNow = nearDoorLeaf.localEulerAngles.y;
            doorOpen = Mathf.Abs(Mathf.DeltaAngle(yNow, doorOpenAngleY)) < 5f;
        }

        if (other.CompareTag("WeaponBox")) 
        { 
            choiceWeapon = true;
            boxObject = other.gameObject;
        }

        if (other.CompareTag("laundryRange"))
        {
            hasLaundryMission = true;
            inLaundryRange = true;
        }

        if (other.CompareTag("foyerRange"))
        {
            hasFoyerMission = true;
            inFoyerRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("DonRun")) { donRunZoneCount = Mathf.Max(0, donRunZoneCount - 1); UpdateRunLock(); }
        if (other.CompareTag("ClimbZone")) canClimbZone = false;
        if (other.CompareTag("Boss")) canAttack = false;

        if (other.CompareTag("NPC")) { nearNPC.gameObject.SetActive(false); canTakeMission = false; }

        if (other.CompareTag("Attack"))
        {
            var enemy = other.GetComponentInParent<EnemyMov>() ?? other.GetComponent<EnemyMov>();
            if (enemy == killTarget) { killTarget = null; canKill = false; }
        }

        if (other.CompareTag("Door"))
        {
            if (nearDoorRoot == null || other.transform.root == nearDoorRoot)
            {
                nearDoor = false;
                nearDoorRoot = null;
                nearDoorLeaf = null;
            }
        }

        if (other.CompareTag("WeaponBox")) choiceWeapon = false;

        if (other.CompareTag("laundryRange"))
        {
            hasLaundryMission = false;
            inLaundryRange = false;
        }

        if (other.CompareTag("foyerRange"))
        {
            hasFoyerMission = false;
            inFoyerRange = false;
        }
    }

    void UpdateRunLock() => canRun = (donRunZoneCount == 0);

    private bool IsGroundContact(Collision col)
    {
        if ((groundLayer.value & (1 << col.gameObject.layer)) == 0) return false;
        foreach (var c in col.contacts) if (c.normal.y >= groundMinNormalY) return true;
        return false;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (ignoreGroundedCheck) return;
        if (!IsGroundContact(collision)) return;

        if (animator.GetBool("IsFalling"))
            animator.SetBool("IsFalling", false);
    }

    public void OnJumpingDownComplete() => isLanding = false;

    public void MoveUpDuringHold(float height, float duration) => StartCoroutine(MoveUpDuringHold_Safe(height, duration));

    private IEnumerator MoveUpDuringHold_Safe(float height, float duration)
    {
        float wait = 0f, timeout = 1.0f;
        while ((isLerpingHoldOffset || !isHolding) && wait < timeout)
        { wait += Time.deltaTime; yield return null; }
        if (!isHolding) yield break;

        yield return StartCoroutine(MoveHoldWithDip(height, duration));
    }

    private IEnumerator MoveHoldWithDip(float height, float duration)
    {
        Vector3 start = transform.position;
        Vector3 downPos = start + new Vector3(0f, -0.1f, 0f);
        float downDuration = 0.1f;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / downDuration;
            transform.position = Vector3.Lerp(start, downPos, t);
            yield return null;
        }

        Vector3 upTarget = start + new Vector3(0f, height, 0.01f);
        t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(downPos, upTarget, t);
            yield return null;
        }

        canStartClimb = true;
    }

    public void MoveForwardAfterClimb(float distance, float duration) => StartCoroutine(ForwardLerpRoutine(distance, duration));

    private IEnumerator ForwardLerpRoutine(float distance, float duration)
    {
        rb.useGravity = false;

        Vector3 start = transform.position;
        Vector3 target = start + transform.forward * distance;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        rb.velocity = Vector3.zero;
    }

    public void OnClimbEnd()
    {
        isClimbing = false;
        rb.useGravity = true;
        rb.isKinematic = false;

        rb.velocity = Vector3.zero;
        blockInput = false;
    }

    void CheckNearbyEnemies()
    {
        if (isCrouching) return;
        if (currentMoveInput.magnitude < 0.05f) return;

        float detectRange = isRunning ? runDetectRange : walkDetectRange;
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange, aiLayerMask);

        foreach (Collider col in hits)
        {
            var enemy = col.GetComponentInParent<EnemyMov>() ?? col.GetComponent<EnemyMov>();
            if (enemy != null) enemy.PlayerDetected(transform.position);
            var villain = col.GetComponentInParent<Villain>() ?? col.GetComponent<Villain>();
            if (villain != null) villain.PlayerDetectedBySound(transform.position);
        }
    }

    void StartBoxJump(Vector3 wallPoint, Vector3 wallNormal, float height)
    {
        ClearLandTriggers();
        isCrouching = false;
        animator.SetBool("IsCrouching", false);

        blockInput = true;
        isHolding = false;
        isClimbing = false;
        rb.useGravity = false;

        Vector3 targetPos = wallPoint + wallNormal * 0.14f;
        targetPos.y = transform.position.y;
        Quaternion targetRot = Quaternion.LookRotation(-wallNormal);

        StartCoroutine(BoxJumpPrepareLerp(targetPos, targetRot, 0.15f));
    }

    private IEnumerator BoxJumpPrepareLerp(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        animator.Play("BoxJump");
    }

    private IEnumerator BoxJumpLerp(Vector3 targetPos, float duration)
    {
        Vector3 start = transform.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, targetPos, t);
            yield return null;
        }

        rb.useGravity = true;
        rb.isKinematic = false;
        blockInput = false;
    }

    public void MoveToBoxTop(float duration)
    {
        float upOffset = 1f;

        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, climbCheckDistance, climbableLayer))
        {
            float wallTopY = hit.collider.bounds.max.y;
            float playerY = transform.position.y;
            float wallHeight = Mathf.Max(0f, wallTopY - playerY);
            upOffset = wallHeight * 0.5f;

            lastBoxWallRemainingHeight = wallHeight;
        }

        Vector3 targetPos = transform.position + Vector3.up * upOffset + transform.forward * 0.3f;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    public void MoveToBoxTopRemaining(float duration)
    {
        float upOffset = lastBoxWallRemainingHeight * 0.5f;
        Vector3 targetPos = transform.position + Vector3.up * upOffset;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    public void OnBoxJumpEnd()
    {
        rb.useGravity = true;
        rb.isKinematic = false;
        blockInput = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, walkDetectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, runDetectRange);

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, wallKeepOutRadius);
    }

    void ShowPausePanel(GameObject panel)
    {
        if (!panel) return;

        panel.SetActive(true);

        var cg = panel.GetComponent<CanvasGroup>();
        if (!cg) cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        foreach (var anim in panel.GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (!EventSystem.current)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        AudioListener.pause = true;
        Time.timeScale = 0f;
    }

    void ShowOverlayPanel_NoPause(GameObject panel)
    {
        if (!panel) return;

        panel.SetActive(true);

        var cg = panel.GetComponent<CanvasGroup>();
        if (!cg) cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        // 퍼즐 조작을 위해 커서 노출 + 잠금 해제
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // EventSystem이 없으면 생성
        if (!EventSystem.current)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        blockInput = true;
    }

    bool AnyPauseOpen()
    {
        return (missionUI && missionUI.activeSelf)
            || (gameClearUI && gameClearUI.activeSelf)
            || (gameOverUI && gameOverUI.activeSelf)
            || (optionUI && optionUI.activeSelf)
            || (weaponChangePanel && weaponChangePanel.activeSelf);
    }

    public void HidePausePanel(GameObject panel)
    {
        if (!panel) return;

        var cg = panel.GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 0f;
        }
        panel.SetActive(false);

        if (!AnyPauseOpen())
        {
            AudioListener.pause = false;
            Time.timeScale = 1f;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            blockInput = false;
            _weaponPickFlowActive = false;
        }
    }

    // 무기별 공격 모션
    void PlayAttackByWeapon()
    {
        // 진행 중엔 무시
        if (blockInput || isAssassinating || isHolding || isClimbing) return;
        if (WeaponManager.i == null) return;

        string trig = null;
        switch (WeaponManager.i.SelectedWeapon)
        {
            case WeaponManager.WeaponType.Gun:
                trig = "AttackGun"; break;
            case WeaponManager.WeaponType.Crowbar:
                trig = "AttackCrowbar"; break;
            case WeaponManager.WeaponType.Bat:
                trig = "AttackBat"; break;
            default:
                return; // 맨손이면 아무 것도 하지 않음 (원하면 여기서 펀치 트리거)
        }

        // 트리거 충돌 방지
        animator.ResetTrigger("AttackGun");
        animator.ResetTrigger("AttackCrowbar");
        animator.ResetTrigger("AttackBat");

        // AE_AttackStart/End가 팔 레이어 블렌드는 이미 처리함
        animator.SetTrigger(trig);
    }

    void TriggerAttackByCurrentWeapon()
    {
        var wm = WeaponManager.i;
        var type = wm ? wm.SelectedWeapon : WeaponManager.WeaponType.Crowbar;

        // 혹시 남아있는 다른 트리거가 충돌하지 않게 리셋
        animator.ResetTrigger("AttackGun");
        animator.ResetTrigger("AttackCrowbar");
        animator.ResetTrigger("AttackBat");

        switch (type)
        {
            case WeaponManager.WeaponType.Gun:
                animator.SetTrigger("AttackGun");
                break;
            case WeaponManager.WeaponType.Bat:
                animator.SetTrigger("AttackBat");
                break;
            case WeaponManager.WeaponType.Crowbar:
            default:
                animator.SetTrigger("AttackCrowbar");
                break;
        }
    }

    public void BindCameraPivot(Transform pivot) { cameraPivot = pivot; }

    // 암살 애니 이벤트
    public void OnAssassinationHit()
    {
        if (!isAssassinating || pendingAssassination == null) return;
        var toKill = pendingAssassination;
        pendingAssassination = null;
        rightArmMaxWeight = 0.01f;
        FadeRightArmLayer(0f, 0.08f);
        toKill.Kill();
    }
    public void OnAssassinationEnd()
    {
        rb.isKinematic = false;
        rb.useGravity = true;

        if (pendingAssassination != null)
            pendingAssassination.PrepareForAssassination(false);

        pendingAssassination = null;
        isAssassinating = false;
        blockInput = false;

        FadeRightArmLayer(0.61f, 0.08f);
        animator.ResetTrigger("AttackCrowbar");
    }

    // 공격 시작 시 (클립 첫 프레임 근처)
    public void AE_AttackStart()
    {
        rightArmMaxWeight = 0.01f;
        FadeRightArmLayer(0f, 0.08f);
    }

    // 공격 끝 시 (클립 마지막 프레임 근처)
    public void AE_AttackEnd()
    {
        rightArmMaxWeight = rightArmDefaultWeight;
        FadeRightArmLayer(rightArmDefaultWeight, 0.08f);
    }

    void RefreshInteractionHint()
    {
        if (!nearNPC) return;
        var keyName = KeyBindings.ToDisplay(KeyBindings.Get(GameAction.Interaction));
        nearNPC.text = $"Press '{keyName}' to talk";
    }

    void FadeRightArmLayer(float targetWeight, float duration = 0.08f)
    {
        if (rightArmLayer < 0 || animator == null) return;
        if (_rightArmLerpCo != null) StopCoroutine(_rightArmLerpCo);
        _rightArmLerpCo = StartCoroutine(RightArmWeightLerp(targetWeight, duration));
    }
    IEnumerator RightArmWeightLerp(float target, float dur)
    {
        float start = animator.GetLayerWeight(rightArmLayer);
        float t = 0f;
        dur = Mathf.Max(0.0001f, dur);
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            animator.SetLayerWeight(rightArmLayer, Mathf.Lerp(start, target, t));
            yield return null;
        }
        animator.SetLayerWeight(rightArmLayer, target);
    }
}
