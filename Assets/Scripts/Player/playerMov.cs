using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using static UnityEditor.Progress;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMov : MonoBehaviour
{
    // 컴포넌트
    public Rigidbody rb;
    public GameObject gameClearUI;
    public GameObject gameOverUI;
    public GameObject missionUI;
    public GameObject optionUI;
    public GameObject nearNPC;
    private Animator animator;
    public GameObject weapon;
    public GameObject weaponChangePanel;

    // 이동 및 회전
    public float speed = 5f;
    private float currentMoveSpeed = 0f;
    public float rotSpeed = 5f;
    public float runSpeed = 3f;
    public Transform cameraPivot;
    int donRunZoneCount = 0;

    // 시체 처리 전용
    [HideInInspector] public bool isDraggingCorpse = false;
    public float dragMoveSpeed = 0.5f;

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

    // 착지 판정 보정
    [SerializeField] private float minAirTimeForLand = 0.12f; // 최소 공중시간
    [SerializeField] private float landMinDownVel = -0.4f;    // 하강속도 임계
    private float timeSinceLeftGround = 999f;                  // 떠난 뒤 경과시간

    // 벽타기
    public float climbDuration = 3.25f;
    public float climbCheckDistance = 2.0f;
    public LayerMask climbableLayer;
    private bool canClimbZone = false;
    private bool isClimbing = false;
    private bool blockInput = false;
    private float lastBoxWallRemainingHeight = 0f;

    // 움직이는 소리 범위
    public float walkDetectRange = 6f;
    public float runDetectRange = 12f;
    public LayerMask aiLayerMask;

    // 벽 매달리기
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
    // 애니메이션 이벤트로 '붙기'를 지연하기 위한 대기 값
    private bool hasPendingWall = false;
    private Vector3 pendingWallPoint;
    private Vector3 pendingWallNormal;
    private float pendingWallTopY;

    [SerializeField] private float attachHoldDistanceFromWall = 0.14f; // 벽에서 떨어져 붙는 거리
    [SerializeField] private float attachLerpDuration = 0.12f;         // 붙을 때 보간 시간

    private Vector3 climbStartPos, climbTargetPos;
    private Quaternion climbStartRot, climbTargetRot;
    private float climbTimer = 0f;

    // 매달리기 취소용 저장값
    private bool isCancellingHold = false;
    [SerializeField] private bool holdCancelAllowed;        // 이벤트가 true로 열어줄 때만 S 허용
    public void SetHoldCancelAllowed(bool allowed) => holdCancelAllowed = allowed;

    public void AllowHoldCancel() { holdCancelAllowed = true; }
    public void BlockHoldCancel() { holdCancelAllowed = false; }

    // 점프 / 낙하
    private bool isJumping = false;
    private float jumpForce = 5f;
    private float verticalVelocity = 0f;
    private bool isLanding = false;
    private float landingTimer = 0f;
    private float landingDelay = 0.6f;
    private bool isGrounded = true;
    private bool wasGroundedLastFrame = true;
    private float jumpCooldown = 1.9f; // 점프 쿨타임
    private float jumpCooldownTimer = 0f;
    private float airMultiplier;
    private bool ignoreGroundedCheck = false;
    private float ignoreGroundedTimer = 0f;
    private float ignoreDurationAfterJump = 0.25f; // 점프 직후 잠깐 지면 판정 무시
    private float fallTimer = 0f;
    [SerializeField] private float fallDebounce = 0.10f; // 100ms

    public LayerMask groundLayer;                 // '땅' 레이어에 닿았을 때만 착지
    [Range(0f, 1f)] public float groundMinNormalY = 0.55f; // 허용 경사(~56˚)

    // 점프 순간 앞벽 체크(간단 고정 힘)
    public float frontCheckDistance = 0.35f;
    public float wallPushStrength = 2.0f; // 점프 직전 바짝 붙었을 때만 수평 밀어내기

    // Alt 이동
    private Vector3 savedForward, savedRight;
    private bool wasAltPressedLastFrame, justReleasedAlt;

    // 앉기
    private bool isCrouching = false;
    [SerializeField] private float crouchCooldown = 0.6f;       // 앉기 쿨타임
    private float crouchCooldownTimer = 0f;
    [SerializeField] float crouchHeight = 1.0f;                 // 앉을 때 콜라이더 높이
    [SerializeField] float colliderLerpTime = 0.10f;
    Vector3 boxSizeStand, boxCenterStand;
    Vector3 boxSizeCrouch, boxCenterCrouch;
    Coroutine crouchColRoutine;

    // Tag 기반 벽 근접 차단(Keep-Out)
    [Header("Wall Keep-Out (by Tag)")]
    [SerializeField] private string wallTag = "Wall";           // 태그명
    [SerializeField] private float wallKeepOutRadius = 0.4f;    // 벽 최소 접근 거리(XZ 기준)
    [SerializeField] private int wallKeepOutIterations = 2;     // 모서리/다중벽 보정 반복
    [SerializeField] private float wallKeepOutSkin = 0.01f;     // 살짝 여유
    [SerializeField] private float wallKeepOutUnderFootTolerance = 0.03f;   // 발바닥보다 아래 표면 무시 허용치

    [Header("Door")]
    [SerializeField] private float doorRotateDuration = 0.6f; // 회전 시간(초)
    [SerializeField] private float doorOpenAngleY = -90f;     // 열릴 때 Y 로테이션(상대각)

    private bool nearDoor = false;
    private Transform nearDoorRoot;    // 트리거에 걸린 Door 루트
    private Transform nearDoorLeaf;    // 실제로 회전시킬 자식
    private bool doorOpen = false;     // 현재 열린 상태인지
    private bool isDoorRotating = false;
    private Quaternion doorClosedRot;  // 닫힘 기준 회전값
    private Quaternion doorOpenRot;    // 열림 목표 회전값
    private Coroutine doorRoutine;

    // 무기 바꾸기 관련
    private bool canWeaponSwitch = false;

    // 무기 선택
    private bool choiceWeapon;
    private bool canChoiceWeapon = false;

    // RightHandGrip 애니메이션 레이어 제어
    private int gripLayer;
    private int gripIdleHash;
    private int gripGunPoseHash;
    // RightArm 애니메이션 레이어 제어
    private int rightArmLayer;
    private float rightArmMaxWeight = 0.61f;

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            donRunZoneCount = 0;
            UpdateRunLock();

            // UI 초기화
            canTakeMission = false;
            nearNPC?.SetActive(false);
            if (missionUI && missionUI.activeSelf) HidePausePanel(missionUI);

            // 커서는 씬에 따라
            bool isMenu = scene.name == "MainLobby";
            Cursor.visible = isMenu;
            Cursor.lockState = isMenu ? CursorLockMode.None : CursorLockMode.Locked;

            // 메뉴가 아니면 혹시 모를 일시정지 상태 복구
            if (!isMenu) { AudioListener.pause = false; Time.timeScale = 1f; }
        };
    }

    void Awake()
    {
        RebindSceneUI();
        SceneManager.sceneLoaded += (_, __) => RebindSceneUI();
    }

    void RebindSceneUI()
    {
        // 플레이어 자식 캔버스 쪽부터 찾아보고, 없으면 씬 전체에서 태그/이름으로 찾기
        var canvas = GetComponentInChildren<Canvas>(true);

        missionUI = missionUI && missionUI.scene.IsValid() ? missionUI : canvas?.transform.Find("MissionImg")?.gameObject ?? GameObject.FindWithTag("MissionUI");
        optionUI = optionUI && optionUI.scene.IsValid() ? optionUI : canvas?.transform.Find("OptionPop")?.gameObject ?? GameObject.Find("OptionPop");
        gameOverUI = gameOverUI && gameOverUI.scene.IsValid() ? gameOverUI : canvas?.transform.Find("GameOver")?.gameObject ?? GameObject.Find("GameOver");
        weaponChangePanel = weaponChangePanel && weaponChangePanel.scene.IsValid() ? weaponChangePanel : canvas?.transform.Find("Weapon_Choice_Panel")?.gameObject ?? GameObject.Find("Weapon_Choice_Panel");
    }

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        Time.timeScale = 1f;

        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        // 레이어/상태 세팅
        gripLayer = animator.GetLayerIndex("RightHandGrip");
        gripIdleHash = Animator.StringToHash("RightHandGrip.Idle State");
        gripGunPoseHash = Animator.StringToHash("RightHandGrip.GunPose");
        rightArmLayer = animator.GetLayerIndex("RightArm");

        // 보조 레이어가 항상 영향을 주도록
        if (gripLayer >= 0) animator.SetLayerWeight(gripLayer, 1f);
        if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, 0f);

        box = GetComponent<BoxCollider>();

        // 서있는 값 저장
        boxSizeStand = box.size;
        boxCenterStand = box.center;

        // 앉은 값 계산(바닥 고정: center.y를 절반만큼 내려줌)
        float newH = crouchHeight;
        boxSizeCrouch = new Vector3(box.size.x, newH, box.size.z);
        float deltaH = box.size.y - newH;
        boxCenterCrouch = new Vector3(box.center.x, box.center.y - deltaH * 0.5f, box.center.z);

        groundLayer = LayerMask.GetMask("Ground", "Climbable");

        // 혹시 비어있으면 자동 바인딩 (씬마다 안전)
        if (!cameraPivot)
        {
            var cam = FindObjectOfType<CameraMov>(true);
            if (cam) cameraPivot = cam.transform;
            else if (Camera.main) cameraPivot = Camera.main.transform;
        }
    }

    void Update()
    {
        // 바닥 감지
        isGrounded = CheckGrounded();

        // 공중으로 떠난 순간 타이머 리셋/누적
        if (wasGroundedLastFrame && !isGrounded)
            timeSinceLeftGround = 0f;
        if (!isGrounded)
            timeSinceLeftGround += Time.deltaTime;

        // 앉기 타이머 감소
        if (crouchCooldownTimer > 0f)
            crouchCooldownTimer -= Time.deltaTime;

        // 벽 붙은 상태 처리(보간 중)
        if (isLerpingHoldOffset)
        {
            holdLerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(holdLerpTimer / holdLerpDuration);
            transform.position = Vector3.Lerp(holdLerpStartPos, holdLerpTargetPos, t);
            transform.rotation = Quaternion.Slerp(holdLerpStartRot, holdLerpTargetRot, t);
            if (t >= 1f)
            {
                isLerpingHoldOffset = false;

                // ★ 붙기 보간이 끝난 '지금' 중력/키네마틱 전환
                rb.useGravity = false;
                rb.isKinematic = true;
            }
            return;
        }

        // 매달린 상태
        if (isHolding)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            bool isHoldingState = st.IsName("Holding") || st.IsTag("Holding");
            if (animator.IsInTransition(0)) isHoldingState = false;

            bool canCancelNow = holdCancelAllowed
                                && !isLerpingHoldOffset
                                && canStartClimb
                                && isHoldingState;

            if (canCancelNow && !isCancellingHold && Input.GetKeyDown(KeyCode.S))
                StartCoroutine(CancelHoldAndReturn());

            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);

            if (Input.GetKeyDown(KeyCode.Space) && canStartClimb)
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

            // climb 중 입력 무시
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
        isRunning = canRun && Input.GetKey(KeyCode.LeftShift) && !isCrouching;
        bool isAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

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

        // 시체 끌땐 뒤로가기만 허용
        // 1) 일반 입력 (드래그/블록 아닐 때만)
        if (!blockInput && !isDraggingCorpse)
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

        // 2) 드래그 전용 입력: 뒤로가기만 허용 (S키)
        float back01 = 0f;
        if (isDraggingCorpse)
        {
            back01 = Mathf.Max(0f, -Input.GetAxisRaw("Vertical")); // S만 반응 (0~1)
            currentMoveInput = -transform.forward * back01;              // 뒤로만
        }

        // 3) (애니메이션 파라미터는 여기서 계산)

        // 애니메이션 파라미터
        if (isDraggingCorpse)
        {
            // 전용 파라미터(권장: 애니메이터에 Bool/Float 추가)
            //animator.SetBool("IsDragging", true);
            //animator.SetFloat("DragSpeed", back01);   // 0~1

            // 기존 파라미터도 쓰고 싶다면 안정적으로 고정
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", -back01);      // 뒤로가는 값
            animator.SetFloat("Speed", back01, 0.1f, Time.deltaTime);
        }
        else
        {
            //animator.SetBool("IsDragging", false);

            Vector3 localMove = transform.InverseTransformDirection(currentMoveInput);
            moveX = Mathf.SmoothDamp(moveX, localMove.x, ref velX, smoothTime);
            moveY = Mathf.SmoothDamp(moveY, localMove.z, ref velY, smoothTime);
            animator.SetFloat("MoveX", moveX);
            animator.SetFloat("MoveY", moveY);

            float speedParam = (isGrounded && currentMoveInput.magnitude > 0.05f)
                                ? (isRunning ? 1f : 0.5f) : 0f;
            animator.SetFloat("Speed", speedParam, 0.1f, Time.deltaTime);
            if (speedParam == 0f) { animator.SetFloat("MoveX", 0f); animator.SetFloat("MoveY", 0f); }
        }

        // 회전
        if (!isDraggingCorpse && currentMoveInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * 100f * Time.deltaTime);
        }

        wasAltPressedLastFrame = isAlt;
        if (justReleasedAlt && !isAlt) justReleasedAlt = false;

        // 벽 잡기 시작 조건
        if (Input.GetKeyDown(KeyCode.Space) && canClimbZone && !isHolding && !isClimbing)
        {
            Vector3 dir = transform.forward;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f; // 발밑

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

        // 점프
        if (Input.GetKeyDown(KeyCode.Space) && !canClimbZone && isGrounded && !isJumping && jumpCooldownTimer <= 0f && !isCrouching && canRun)
        {
            ClearLandTriggers();
            isJumping = true;
            jumpCooldownTimer = jumpCooldown;
            animator.SetBool("IsJumping", true);

            // 점프 직후 Grounded 무시
            ignoreGroundedCheck = true;
            ignoreGroundedTimer = ignoreDurationAfterJump;

            rb.isKinematic = false;
            rb.useGravity = true;

            Vector3 velocity = rb.velocity;
            Vector3 jumpDir = currentMoveInput.sqrMagnitude > 0.01f ? currentMoveInput.normalized : transform.forward;
            float groundSpeed = isRunning ? speed * runSpeed : speed;
            float jumpForwardSpeed = (currentMoveInput.magnitude > 0.1f) ? groundSpeed * 0.4f : groundSpeed * 0.2f;

            velocity.x = jumpDir.x * jumpForwardSpeed;
            velocity.z = jumpDir.z * jumpForwardSpeed;
            velocity.y = jumpForce;

            // 앞에 벽이 있으면(태그 wall) 수평으로만 살짝 밀어냄
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward,
                                out RaycastHit front, frontCheckDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                ApplyWallPush(ref velocity, front);
            }

            rb.velocity = velocity;
        }

        // 점프 직후 Grounded 무시 타이머
        if (ignoreGroundedCheck)
        {
            ignoreGroundedTimer -= Time.deltaTime;
            if (ignoreGroundedTimer <= 0f)
                ignoreGroundedCheck = false;
        }

        // 점프 쿨타임
        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        // 낙하 감지
        verticalVelocity = rb.velocity.y;
        bool wantFall = (verticalVelocity < -0.1f) && !isGrounded;

        if (wantFall) fallTimer += Time.deltaTime;
        else fallTimer = 0f;

        bool isFallingAnim = fallTimer > fallDebounce;
        animator.SetBool("IsFalling", isFallingAnim);

        // 착지 트리거(프레임 전이)
        if (!wasGroundedLastFrame && isGrounded
                && timeSinceLeftGround >= minAirTimeForLand
                && verticalVelocity <= landMinDownVel
                && !ignoreGroundedCheck)
        {
            animator.SetTrigger("Land");
            animator.SetBool("IsJumping", false);
            animator.SetBool("IsFalling", false);
            fallTimer = 0f;             // 낙하 타이머 리셋
            isJumping = false;
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
        if (Input.GetKeyDown(KeyCode.C) && crouchCooldownTimer <= 0f)
        {
            bool wantCrouch = !isCrouching;

            // 앉은 상태에서 서려고 할 때 머리 공간 체크
            if (!wantCrouch && !CanStandUp())
            {
                return;
            }

            isCrouching = wantCrouch;
            animator.SetBool("IsCrouching", isCrouching);

            // C키 쿨타임 시작
            crouchCooldownTimer = crouchCooldown;

            // 효과음
            SoundManager.i?.PlaySFX(PlayerSfx.CrouchToggle, SfxBus.Effect, 1f);

            // 앉는 콜라이더로 번경
            ApplyCrouchCollider(isCrouching);
        }

        // 속도 조정
        float moveSpeed = isDraggingCorpse ? dragMoveSpeed : (isRunning ? speed * runSpeed : speed);        // 드래그 중엔 무조건 이 값

        if (!isDraggingCorpse && isCrouching) moveSpeed *= 0.6f;

        currentMoveSpeed = moveSpeed;

        // 움직이는 소리 범위
        CheckNearbyEnemies();

        // 클리어
        if (canAttack && Input.GetMouseButtonDown(0))
            ShowPausePanel(gameClearUI);

        // 미션 받기
        if (canTakeMission && Input.GetKeyDown(KeyCode.E))
            ShowPausePanel(missionUI);

        // ESC로 옵션창 토글
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 1) 열려 있는 다른 패널이 있다면 그걸 먼저 닫기
            if (missionUI && missionUI.activeSelf) { HidePausePanel(missionUI); return; }
            if (gameClearUI && gameClearUI.activeSelf) { HidePausePanel(gameClearUI); return; }
            if (gameOverUI && gameOverUI.activeSelf) { HidePausePanel(gameOverUI); return; }
            if (weaponChangePanel && weaponChangePanel.activeSelf) { HidePausePanel(weaponChangePanel); return; }
            if (optionUI && optionUI.activeSelf) { HidePausePanel(optionUI); return; }

            // 2) 어느 것도 안 열려 있으면 옵션창 열기
            ShowPausePanel(optionUI);
        }

        // 암살
        if (canKill && Input.GetMouseButtonDown(0))
        {
            if (killTarget != null)
            {
                killTarget.Kill();
                canKill = false;
                killTarget = null;
            }
        }

        // 문열기
        if (nearDoor && Input.GetKeyDown(KeyCode.E) && nearDoorLeaf != null && !isDoorRotating)
        {
            // 목표 회전 선택
            Quaternion target = doorOpen ? doorClosedRot : doorOpenRot;

            if (doorRoutine != null) StopCoroutine(doorRoutine);
            doorRoutine = StartCoroutine(RotateLocalY_Smooth(nearDoorLeaf, nearDoorLeaf.localRotation, target, doorRotateDuration));

            doorOpen = !doorOpen; // 상태 토글
        }

        // 무기 선택창
        if (choiceWeapon && Input.GetKeyDown(KeyCode.E))
        {
            ButtonControl button = transform.GetChild(1).GetChild(0).GetChild(1).GetComponent<ButtonControl>();
            button.canNextStage = true;

            canChoiceWeapon = !canChoiceWeapon;

            if (canChoiceWeapon)
                ShowPausePanel(weaponChangePanel);
            else
                HidePausePanel(weaponChangePanel);
        }

        // 무기를 들수있는지 확인
        if (WeaponManager.i && WeaponManager.i.canSwitch && !canWeaponSwitch)
        {
            canWeaponSwitch = true;

            // 무기창이 실제로 열려 있을 때만 닫기
            if (weaponChangePanel && weaponChangePanel.activeSelf)
                HidePausePanel(weaponChangePanel);
        }

        // 무기 바꾸기
        if (Input.GetKeyDown(KeyCode.Alpha1) && canWeaponSwitch)    // 맨손
        {
            if (gripLayer >= 0) animator.CrossFade(gripIdleHash, 0.1f, gripLayer, 0f);
            if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, 0f);
            if (weapon) weapon.SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2) && canWeaponSwitch)    // 무기 들었을 때
        {
            if (gripLayer >= 0) animator.CrossFade(gripGunPoseHash, 0.1f, gripLayer, 0f);
            if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, rightArmMaxWeight);
            if (weapon) weapon.SetActive(true);
        }
    }

    // 부드럽게 문열기
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
        tr.localRotation = to; // 정밀 보정
        isDoorRotating = false;
    }

    // 앉을 때 콜라이더 변경
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

        // 물리 프레임과 맞추기 위해 FixedUpdate 타이밍으로 보간
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

    // 앉았다 일어날 때 머리 위에 벽있으면 못일어남
    bool CanStandUp()
    {
        float standTop = boxCenterStand.y + boxSizeStand.y * 0.5f;
        float crouchTop = boxCenterCrouch.y + boxSizeCrouch.y * 0.5f;
        float deltaTop = standTop - crouchTop;
        if (deltaTop <= 0.001f) return true;

        // 머리 쪽 추가로 차지하게 될 윗부분만 검사
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

    // 벽타기 취소
    IEnumerator CancelHoldAndReturn()
    {
        if (isCancellingHold) yield break;
        isCancellingHold = true;

        holdCancelAllowed = false;

        // 원위치로 부드럽게 복귀
        float dur = 0.18f;
        float t = 0f;
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;

        // 이동/입력 잠깐 막기
        blockInput = true;
        animator.SetBool("Hold", false);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            transform.position = Vector3.Lerp(fromPos, holdLerpStartPos, t);
            transform.rotation = Quaternion.Slerp(fromRot, holdLerpStartRot, t);
            yield return null;
        }

        // 상태/물리/애니메이터 복구
        isHolding = false;
        isLerpingHoldOffset = false;
        rb.isKinematic = false;
        rb.useGravity = true;

        blockInput = false;

        isCancellingHold = false;
    }

    // 점프 직전 앞벽 밀어내기(태그 wall 대상)
    private void ApplyWallPush(ref Vector3 velocity, RaycastHit front)
    {
        // 바닥처럼 위로 향한 면이면 무시
        bool isGroundLike = ((1 << front.collider.gameObject.layer) & groundLayer) != 0
                            && front.normal.y >= groundMinNormalY;
        if (isGroundLike) return;

        // 태그 검사 (wallTag에만 적용)
        if (!front.collider.CompareTag(wallTag)) return;

        // 수평 성분으로만 밀어냄
        Vector3 horizNormal = new Vector3(front.normal.x, 0f, front.normal.z);
        if (horizNormal.sqrMagnitude > 0.0001f)
        {
            horizNormal.Normalize();
            velocity += horizNormal * wallPushStrength;
        }

        // 잠깐 Grounded 무시 → Land 오인 방지
        ignoreGroundedCheck = true;
        ignoreGroundedTimer = Mathf.Max(ignoreDurationAfterJump, minAirTimeForLand);
    }

    // 애니메이터 트리거/상태 초기화
    private void ClearLandTriggers()
    {
        animator.ResetTrigger("Land");
    }

    // 바닥 체크(코요테 타임)
    private bool CheckGrounded()
    {
        if (ignoreGroundedCheck) return false;

        bool touching = BoxGroundProbeMulti();

        // 코요테 타임: 잠깐 떨어져도 유지
        groundedTimer = touching ? groundedCoyoteTime : groundedTimer - Time.deltaTime;
        return groundedTimer > 0f;
    }

    // 고정 업데이트(이동/근접 차단)
    void FixedUpdate()
    {
        bool block = isClimbing || isHolding;
        if (block)
        {
            return;
        }

        // 이동 처리
        airMultiplier = (isDraggingCorpse || isGrounded) ? 1f : 0.5f;

        Vector3 move = currentMoveInput * currentMoveSpeed * airMultiplier * Time.fixedDeltaTime;

        // 최종 목표 위치
        Vector3 newPos = rb.position + move;

        // 태그가 wall인 오브젝트에 wallKeepOutRadius 이하로 접근 금지(수평)
        EnforceWallKeepOut(ref newPos);

        Vector3 pos = rb.position;
        SlideCast(ref pos, newPos, /*radius*/0.3f, /*height*/box.size.y * transform.lossyScale.y, groundLayer);
        rb.MovePosition(newPos);
    }

    // 벽 뚫기 보강
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
            // 벽에 붙여놓고 표면을 따라 미끄러지기
            from = hit.point + hit.normal * skin;
            Vector3 remain = (to - from);
            Vector3 slide = Vector3.ProjectOnPlane(remain, hit.normal);
            from += slide;            // 표면 따라 이동
            return true;
        }
        return false;
    }

    // Tag=wall 근접 차단(Keep-Out) 보정 (수평 XZ만)
    private void EnforceWallKeepOut(ref Vector3 pos)
    {
        // 플레이어 발바닥 월드 Y (BoxCollider 기준)
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

                // '발바닥보다 아래'에 있는 표면은 밀어내기 제외
                Vector3 query = new Vector3(pos.x, footY, pos.z);
                Vector3 cp = col.ClosestPoint(query);
                if (cp.y <= footY - wallKeepOutUnderFootTolerance ||
                    col.bounds.max.y <= footY - wallKeepOutUnderFootTolerance)
                {
                    continue; // 이미 내가 위에 있으므로 밀지 않음
                }

                // 수평 거리 기준으로만 분리
                Vector3 delta = new Vector3(pos.x - cp.x, 0f, pos.z - cp.z);
                float d = delta.magnitude;
                if (d < wallKeepOutRadius)
                {
                    Vector3 n;
                    if (d > 1e-4f) n = delta / d;
                    else
                    {
                        Vector3 fallback = new Vector3(
                            pos.x - col.bounds.center.x, 0f, pos.z - col.bounds.center.z
                        );
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

    // 바닥 감지(BoxCast 멀티 프로브)
    private bool BoxGroundProbeMulti()
    {
        if (!box) return false;

        // BoxCast 파라미터 계산 (월드 기준)
        Vector3 center = box.transform.TransformPoint(box.center);
        Vector3 lossy = box.transform.lossyScale;

        // 박스 절반 크기(월드)
        Vector3 half = new Vector3(
            Mathf.Abs(box.size.x * 0.5f * lossy.x),
            Mathf.Abs(box.size.y * 0.5f * lossy.y),
            Mathf.Abs(box.size.z * 0.5f * lossy.z)
        );

        // 살짝 축소해 자기 자신/측면에 긁히는 것 방지
        Vector3 halfShrink = new Vector3(
            Mathf.Max(half.x - 0.01f, 0.001f),
            Mathf.Max(half.y - 0.01f, 0.001f),
            Mathf.Max(half.z - 0.01f, 0.001f)
        );

        // 시작을 아주 조금 위로 띄워서(겹침 방지), 아래로 캐스트
        float skin = 0.02f;
        Vector3 start = center + Vector3.up * skin;
        float distance = half.y + boxGroundExtra + skin;
        Quaternion rot = box.transform.rotation;

        bool Probe(Vector3 o)
        {
            if (Physics.BoxCast(o, halfShrink, Vector3.down, out RaycastHit hit, rot, distance, groundLayer, QueryTriggerInteraction.Ignore))
            {
                // 경사 허용치 이상만 지면으로 인정
                return hit.normal.y >= groundMinNormalY;
            }
            return false;
        }

        // 센터 + 앞/뒤/좌/우 4방향 보조 프로브
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

    // 벽 잡기(Holding) 시작
    void StartHolding(RaycastHit hit)
    {
        ClearLandTriggers();
        isCrouching = false;
        animator.SetBool("IsCrouching", false);

        float wallTop = hit.collider.bounds.max.y;
        float footY = box ? box.bounds.min.y : transform.position.y;
        float wallHeight = Mathf.Max(0f, wallTop - footY);

        remainingWallHeight = wallHeight;
        detectedWallHeight = wallHeight; // 안전하게 갱신

        if (wallHeight <= 1.0f)
        {
            StartBoxJump(hit.point, hit.normal, wallHeight);
            return;
        }

        // 여기서는 '대기'만: 실제 붙기는 애니메이션 이벤트에서
        blockInput = true;
        isHolding = false;               // 아직 안 붙음
        isLerpingHoldOffset = false;

        hasPendingWall = true;
        pendingWallPoint = hit.point;
        pendingWallNormal = hit.normal;
        pendingWallTopY = wallTop;

        animator.SetTrigger("Hold");     // 제자리에서 손 뻗는 모션 재생
        holdCancelAllowed = false;
    }

    // 애니메이션 이벤트: 실제로 벽으로 '붙기' 시작
    public void AE_AttachToWall()
    {
        if (!hasPendingWall) return;
        hasPendingWall = false;

        // 붙는 중엔 isHolding을 켜두되, 최우선은 보간 블록(isLerpingHoldOffset)이라 Update가 안전하게 return 됨
        isHolding = true;

        Vector3 targetPos = pendingWallPoint + pendingWallNormal * attachHoldDistanceFromWall;
        targetPos.y = transform.position.y;                 // 스페이스 누른 '그 자리' 높이 유지 (공중 시작 X)
        Quaternion targetRot = Quaternion.LookRotation(-pendingWallNormal);

        holdLerpStartPos = transform.position;
        holdLerpTargetPos = targetPos;
        holdLerpStartRot = transform.rotation;
        holdLerpTargetRot = targetRot;
        holdLerpTimer = 0f;
        holdLerpDuration = attachLerpDuration;
        isLerpingHoldOffset = true;

        holdingStartPos = targetPos;
        holdCancelAllowed = false; // 필요시 이후 이벤트로 열어줘
    }

    // 벽 오르기(Climb) 시작
    public void StartClimbFromHold(float duration)
    {
        ClearLandTriggers();
        isHolding = false;
        canStartClimb = false;
        climbTimer = 0f;
        climbDuration = duration;

        climbStartPos = transform.position;
        climbStartRot = transform.rotation;

        // 벽 높이에 따라 올라갈 높이 계산
        float climbHeight = Mathf.Clamp(detectedWallHeight + 0.15f, 1f, 3.5f);

        climbTargetPos = holdingStartPos + Vector3.up * climbHeight;
        climbTargetRot = transform.rotation;

        animator.SetBool("Hold", false);
        animator.SetTrigger("Climb");

        isClimbing = true;
        rb.useGravity = false;
    }

    // 트리거 충돌
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DonRun"))
        {
            donRunZoneCount++;
            UpdateRunLock();
        }

        if (other.CompareTag("ClimbZone"))
            canClimbZone = true;

        if (other.CompareTag("Boss"))
            canAttack = true;

        if (other.CompareTag("NPC"))
        {
            nearNPC.SetActive(true);
            canTakeMission = true;
        }

        if (other.CompareTag("Discorver"))
            ShowPausePanel(gameOverUI);

        if (other.CompareTag("Attack"))
        {
            var enemy = other.GetComponentInParent<EnemyMov>();
            if (enemy == null) enemy = other.GetComponent<EnemyMov>();
            if (enemy != null)
            {
                killTarget = enemy;
                canKill = true;
            }
        }

        if (other.CompareTag("Door"))
        {
            nearDoor = true;

            // 문 루트 & 첫 번째 자식(없으면 루트 자체)
            nearDoorRoot = other.transform.root;
            nearDoorLeaf = (nearDoorRoot.childCount > 0) ? nearDoorRoot.GetChild(0) : nearDoorRoot;

            // 0도 닫힘, -90도 열림
            doorClosedRot = Quaternion.Euler(0f, 0f, 0f);
            doorOpenRot = Quaternion.Euler(0f, doorOpenAngleY, 0f);

            // 현재 열린 상태인지(0/-90 중 어디에 더 가까운지) 추정
            float yNow = nearDoorLeaf.localEulerAngles.y;
            doorOpen = Mathf.Abs(Mathf.DeltaAngle(yNow, doorOpenAngleY)) < 5f; // -90°에 더 가까우면 열린 상태로 간주
        }

        if (other.CompareTag("WeaponBox"))
        {
            choiceWeapon = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("DonRun"))
        {
            donRunZoneCount = Mathf.Max(0, donRunZoneCount - 1);
            UpdateRunLock();
        }

        if (other.CompareTag("ClimbZone"))
            canClimbZone = false;

        if (other.CompareTag("Boss"))
            canAttack = false;

        if (other.CompareTag("NPC"))
        {
            nearNPC.SetActive(false);
            canTakeMission = false;
        }
        if (other.CompareTag("Attack"))
        {
            var enemy = other.GetComponentInParent<EnemyMov>();
            if (enemy == null) enemy = other.GetComponent<EnemyMov>();
            if (enemy == killTarget)
            {
                killTarget = null;
                canKill = false;
            }
        }
        if (other.CompareTag("Door"))
        {
            // 같은 문 루트에서 나갔을 때만 해제 (겹치는 문의 간섭 방지)
            if (nearDoorRoot == null || other.transform.root == nearDoorRoot)
            {
                nearDoor = false;
                nearDoorRoot = null;
                nearDoorLeaf = null;
            }
        }
        if (other.CompareTag("WeaponBox"))
        {
            choiceWeapon = false;
        }
    }

    // 달리기 제한
    void UpdateRunLock()
    {
        // 시체를 끄는 중엔 계속 못 달리게 유지
        canRun = (donRunZoneCount == 0) && !isDraggingCorpse;
    }

    // 지면 접촉 판단(착지 처리에만 사용)
    private bool IsGroundContact(Collision col)
    {
        // groundLayer에 포함된 레이어인지 확인
        if ((groundLayer.value & (1 << col.gameObject.layer)) == 0) return false;

        // 위를 향한 면만 지면으로 인정
        foreach (var c in col.contacts)
            if (c.normal.y >= groundMinNormalY) return true;

        return false;
    }

    private void OnCollisionStay(Collision collision)
    {
        // 점프 직후 무시 시간에는 어떤 충돌도 착지로 처리하지 않음
        if (ignoreGroundedCheck) return;

        // 지면과의 접촉이 아니면(=벽/측면) 무시
        if (!IsGroundContact(collision)) return;

        // 상태 보정
        if (isJumping || animator.GetBool("IsJumping"))
        {
            isJumping = false;
            animator.SetBool("IsJumping", false);
        }
        if (animator.GetBool("IsFalling"))
            animator.SetBool("IsFalling", false);
    }

    public void OnJumpingDownComplete()
    {
        isLanding = false;
    }

    // 매달리기 전 단계 모션
    public void MoveUpDuringHold(float height, float duration)
    {
        StartCoroutine(MoveUpDuringHold_Safe(height, duration));
    }

    private IEnumerator MoveUpDuringHold_Safe(float height, float duration)
    {
        // AE_AttachToWall 호출 후 붙기 보간이 끝날 때까지 대기
        float wait = 0f, timeout = 1.0f; // 필요시 조정
        while ((isLerpingHoldOffset || !isHolding) && wait < timeout)
        {
            wait += Time.deltaTime;
            yield return null;
        }
        if (!isHolding) yield break; // 붙지 못했으면 취소

        // 이제 보간 충돌 없음: 살짝 내려갔다가 위로 올리기
        yield return StartCoroutine(MoveHoldWithDip(height, duration));
    }

    private IEnumerator MoveHoldWithDip(float height, float duration)
    {
        // 1) 살짝 아래로
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

        // 2) 위로 올리기
        Vector3 upTarget = start + new Vector3(0f, height, 0.01f);
        t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(downPos, upTarget, t);
            yield return null;
        }

        // Climb 가능
        canStartClimb = true;
    }

    // 벽 안뚫리게 앞으로 이동
    public void MoveForwardAfterClimb(float distance, float duration)
    {
        StartCoroutine(ForwardLerpRoutine(distance, duration));
    }

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

    // 움직임 소리 범위
    void CheckNearbyEnemies()
    {
        if (isCrouching) return; // 앉아있으면 소리 감지 X
        if (currentMoveInput.magnitude < 0.05f) return;

        float detectRange = isRunning ? runDetectRange : walkDetectRange;
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange, aiLayerMask);

        foreach (Collider col in hits)
        {
            EnemyMov enemy = col.GetComponent<EnemyMov>();
            if (enemy != null)
                enemy.PlayerDetected(transform.position);
        }
    }

    // 낮은 벽 BoxJump
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
        float upOffset = 1f; // 기본값

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

    // 기즈모
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, walkDetectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, runDetectRange);

        // Keep-Out 반경 시각화(참고용)
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, wallKeepOutRadius);
    }

    // UI 표시/숨김
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

    // 다른 패널이 열려있으면 전역 해제하지 않도록
    bool AnyPauseOpen()
    {
        return (missionUI && missionUI.activeSelf)
            || (gameClearUI && gameClearUI.activeSelf)
            || (gameOverUI && gameOverUI.activeSelf)
            || (optionUI && optionUI.activeSelf)
            || (weaponChangePanel && weaponChangePanel.activeSelf);
    }

    void HidePausePanel(GameObject panel)
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
        }
    }

    // 드래그 중 달리기 무시
    public void OnDragStart()
    {
        isDraggingCorpse = true;
        canRun = false;                  // 달리기 입력 무시
    }
    public void OnDragStop()
    {
        isDraggingCorpse = false;
        canRun = true;
    }

    public void BindCameraPivot(Transform pivot) { cameraPivot = pivot; }
}
