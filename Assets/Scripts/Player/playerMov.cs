using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Audio;
using UnityEditor;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(-100)]
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
    public GameObject minimapInside;
    public GameObject minimapOut;
    public GameObject[] enemies1f;
    public GameObject laundryPuzzle;
    [SerializeField] Image poseImgPat;
    [SerializeField] Sprite[] poseImg;
    [SerializeField] private AudioMixerGroup sfxGroup;

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

    [Header("Game Clear (attack anim timing)")]
    [SerializeField] private bool _gameClearArmed = false;
    [SerializeField] private bool _gameClearShown = false;

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
    int _animLockDepth = 0;

    // 바닥 감지 (BoxCollider 기반 + 코요테 타임)
    private BoxCollider box;
    private EnemyMov killTarget = null;        // 암살 대상
    private float boxGroundExtra = 0.1f;       // 바닥까지 여유 캐스트 거리
    private float edgeProbeOffset = 0.18f;     // 앞/뒤/좌/우 보조 프로브 오프셋
    private float groundedCoyoteTime = 0.12f;  // 유예 시간
    private float groundedTimer = 0f;
    int _lastAttackSfxFrame = -1;

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
    public static bool blockInput = false;
    bool _wasCrouchingBeforeClimb;
    private float lastBoxWallRemainingHeight = 0f;
    bool inSecretRange = false;
    bool _boxJumpWantsCrawl = false;
    bool _boxJumpSfxPlayed = false;
    [SerializeField] private Collider extraCollidersToToggle;
    int _colDisableDepth = 0;

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

    [Header("Crawl")]
    private bool isCrawling = false;
    [SerializeField] private float crawlHeight = 0.3f;     // 엎드릴 때 콜라이더 높이
    [SerializeField] private float crawlSpeedMul = 0.35f;   // 엎드린 이동 속도 비율
    [SerializeField] private float crawlScaleZ = 1.25f;
    [SerializeField, Range(0f, 1f)]
    Vector3 boxSizeCrawl, boxCenterCrawl;
    private bool isCrawlAnimating = false;
    private bool _lieMoveOn = false;

    [Header("Crawl Cam")]
    public float crawlCamDown = 1f;   // 얼마나 내릴지
    public float crawlCamLerp = 0.12f;   // 보간 시간
    private bool _crawlCamOn = false;

    private Transform camT;
    private Vector3 camLocalStart;
    private Coroutine camYCo;

    // Tag 기반 벽 근접 차단(Keep-Out)
    [Header("Wall Keep-Out (by Tag)")]
    [SerializeField] private string wallTag = "Wall";
    [SerializeField] private float wallKeepOutRadius = 0.4f;
    [SerializeField] private int wallKeepOutIterations = 2;
    [SerializeField] private float wallKeepOutSkin = 0.01f;
    [SerializeField] private float wallKeepOutUnderFootTolerance = 0.03f;

    [Header("Door")]
    [SerializeField] private float doorRotateDuration = 0.6f;
    [SerializeField] private float doorZDelta = 90f;

    private readonly HashSet<Transform> nearDoorLeaves = new();
    private readonly Dictionary<Transform, Coroutine> doorRoutines = new();
    private readonly Dictionary<Transform, DoorData> doors = new();

    struct DoorData
    {
        public Quaternion closed;
        public Quaternion open;
        public bool isOpen;
        public int sign;

        public bool requiresBedroomKey;
    }

    // 무기 바꾸기 관련
    public bool canWeaponSwitch = false;
    private GameObject boxObject;
    public float weaponPanelDelay = 0.7f;           // 박스 연 후 패널 띄우기까지 지연
    Coroutine _openWeaponPanelCo;
    bool _weaponPickFlowActive = false;             // 중복 입력 방지
    private int _selectedWeaponChildIndex = -1;     // 0=Gun, 1=Crowbar

    // 애니/장착 제어
    private int takeWeaponLayer;
    private int takeGunHash, takeCrowbarHash, takeBatHash;
    private bool equipInProgress = false;
    private bool _putAwayViaEquip = false;
    private WeaponManager.WeaponType pendingEquipType = WeaponManager.WeaponType.None;

    // 무기 선택
    private bool choiceWeapon;
    [Header("Weapon Move Speed")]
    public float handSpeedMul = 1.0f;
    public float gunSpeedMul = 1.0f;
    public float crowbarSpeedMul = 0.75f;
    public float batSpeedMul = 0.5f;
    [Header("Weapon Anim Speed")]
    public float handAnimMul = 1.0f;
    public float gunAnimMul = 1.0f;
    public float crowbarAnimMul = 0.95f;
    public float batAnimMul = 0.8f;

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
    private bool _laundryRangeDisabled = false; // 성공 후 범위 비활성화
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

    [Header("식당 미션")]
    public float interactRange = 2f;
    public LayerMask interactMask;   // Pickup/SlotPlate가 있는 레이어 포함

    [Header("서재 미션")]
    public Transform studyCamTarget;
    private bool hasStudyMission = false;
    private bool inStudyRange = false;
    private bool isStudyView = false;
    public Transform studyResultCamTarget;
    private bool hasStudyResult = false;
    private bool inStudyResult = false;
    private bool isStudyResultView = false;

    [Header("드레스룸 미션")]
    public GameObject jewelryUI;
    private bool inJewelryRange = false;

    [Header("게스트룸 미션")]
    public Transform guestCamTarget;
    public GameObject guestBoxUI1;
    public GameObject guestBoxUI2;
    public GameObject guestBoxUI3;
    private bool hasGuestMission = false;
    private bool inGuestRange = false;
    private bool inGuestBox1 = false;
    private bool inGuestBox2 = false;
    private bool inGuestBox3 = false;



    [Header("미션 간 보이는 Mask")]
    public string[] laundryViewLayers = new[] 
    { "Default", "Ground" , "FoyerPuzzle" , "OutlineRendererHelper" , "OutlineRendererBox" , "OutlineRendererBook"};

    private int _savedCamMask;
    private bool _hasSavedCamMask = false;

    [Header("Ghost Mode")]
    public bool ghostMode = false;
    [SerializeField] private float ghostMoveSpeed = 8f;
    [SerializeField] private float ghostVerticalSpeed = 5f;

    private struct GhostColliderState
    {
        public Collider col;
        public bool enabled;
    }
    private List<GhostColliderState> ghostColliderStates;

    // 무기를 들고있는가
    bool IsWeaponShown()
    {
        if (!weapon || !weapon.activeSelf) return false;
        for (int i = 0; i < weapon.transform.childCount; i++)
            if (weapon.transform.GetChild(i).gameObject.activeSelf) return true;
        return false;
    }
    
    // 무기를 바꾸는 중인가
    bool IsEquipBusy()
    {
        if (equipInProgress) return true;

        if (animator && takeWeaponLayer >= 0)
        {
            // 레이어 전이 중이면 바쁨
            if (animator.IsInTransition(takeWeaponLayer)) return true;

            // Take_* 상태가 아직 끝나지 않았으면 바쁨
            var st = animator.GetCurrentAnimatorStateInfo(takeWeaponLayer);
            bool isTakeState =
                st.fullPathHash == takeGunHash ||
                st.fullPathHash == takeCrowbarHash ||
                st.fullPathHash == takeBatHash;

            if (isTakeState && st.normalizedTime < 1f) return true;
        }
        return false;
    }

    //무기타입 -> 장착 스테이트 해시
    int GetEquipStateHash(WeaponManager.WeaponType t)
    {
        return t switch
        {
            WeaponManager.WeaponType.Gun => takeGunHash,
            WeaponManager.WeaponType.Bat => takeBatHash,
            WeaponManager.WeaponType.Crowbar => takeCrowbarHash,
            _ => takeCrowbarHash,
        };
    }

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
        var follow = CameraMov.i ?? FindObjectOfType<CameraMov>(true);
        if (follow) BindCameraPivot(follow.transform);
        else if (Camera.main) BindCameraPivot(Camera.main.transform);

        RebindSceneUI();

        donRunZoneCount = 0; UpdateRunLock();
        canAttack = false; killTarget = null; choiceWeapon = false; canTakeMission = false;

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

    void Awake() 
    {
        RebindSceneUI(); 
        SfxPlayer.outputGroup = sfxGroup; 
    }

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
        var follow = CameraMov.i ?? FindObjectOfType<CameraMov>(true);
        if (follow) BindCameraPivot(follow.transform);
        else if (Camera.main) BindCameraPivot(Camera.main.transform);

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
        takeWeaponLayer = animator.GetLayerIndex("TakeWeapon");
        takeGunHash = Animator.StringToHash("TakeWeapon.Take_Gun");
        takeCrowbarHash = Animator.StringToHash("TakeWeapon.Take_CrowBar");
        takeBatHash = Animator.StringToHash("TakeWeapon.Take_Bat");
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

        // 엎드린 값 계산
        float crawlH = crawlHeight;
        float z0 = box.size.z;
        float z1 = z0 * crawlScaleZ;
        boxSizeCrawl = new Vector3(box.size.x, crawlH, z1);
        float deltaHc = box.size.y - crawlH;
        float centerY = box.center.y - deltaHc * 0.5f;

        float deltaZ = z1 - z0;
        float offsetZ = 0f;

        boxCenterCrawl = new Vector3(box.center.x, centerY, box.center.z + offsetZ);

        groundLayer = LayerMask.GetMask("Ground", "Climbable");

        if (!cameraPivot)
        {
            var cam = FindObjectOfType<CameraMov>(true);
            if (cam) cameraPivot = cam.transform;
            else if (Camera.main) cameraPivot = Camera.main.transform;
        }

        if (cameraPivot)
        {
            camT = cameraPivot;
            camLocalStart = camT.localPosition;
        }

        if (takeWeaponLayer >= 0) animator.SetLayerWeight(takeWeaponLayer, 0f);
    }

    void RebindMinimapAndEnemies()
    {
        minimap1fPicture = GameObject.Find("1FMapPicture");
        enemies1f = GameObject.FindGameObjectsWithTag("Enemy1F");
    }

    void Update()
    {
        if (_sceneInputGraceTimer > 0f) _sceneInputGraceTimer -= Time.deltaTime;

        // 유령화
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ToggleGhostMode();
        }
        if (ghostMode)
        {
            HandleGhostMovement();
            return;
        }

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
            nearNPC.gameObject.SetActive(false);

            // E 또는 ESC로 빠져나오기
            if (EPressed() || Input.GetKeyDown(KeyCode.Escape))
            {
                ExitLaundryView();
                nearNPC.gameObject.SetActive(true);
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

        // 움직임 제한
        if (blockInput)
        {
            currentMoveInput = Vector3.zero;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);
        }

        // 일반 이동
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
        Vector3 localMove;

        if (isCrawling)
        {
            // 카메라 기준으로 투영: X=카메라 Right, Z=카메라 Forward
            float lx = Vector3.Dot(currentMoveInput, moveRight);
            float lz = Vector3.Dot(currentMoveInput, moveForward);
            localMove = new Vector3(lx, 0f, lz);
        }
        else
        {
            // 기존 방식(캐릭터 기준)
            localMove = transform.InverseTransformDirection(currentMoveInput);
        }

        moveX = Mathf.SmoothDamp(moveX, localMove.x, ref velX, smoothTime);
        moveY = Mathf.SmoothDamp(moveY, localMove.z, ref velY, smoothTime);
        animator.SetFloat("MoveX", moveX);
        animator.SetFloat("MoveY", moveY);

        float speedParam = (isGrounded && currentMoveInput.magnitude > 0.05f)
            ? (isRunning ? 1f : 0.5f)
            : 0f;

        float animMul = handAnimMul;

        if (IsWeaponShown() && WeaponManager.i != null)
        {
            switch (WeaponManager.i.SelectedWeapon)
            {
                case WeaponManager.WeaponType.Gun:
                    animMul = gunAnimMul;
                    break;
                case WeaponManager.WeaponType.Crowbar:
                    animMul = crowbarAnimMul;
                    break;
                case WeaponManager.WeaponType.Bat:
                    animMul = batAnimMul;
                    break;
                case WeaponManager.WeaponType.None:
                default:
                    animMul = handAnimMul;
                    break;
            }
        }
        animator.SetFloat("Speed", speedParam * animMul, 0.1f, Time.deltaTime);

        if (speedParam == 0f)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
        }

        if (isCrawling && !blockInput)
        {
            // localMove는 위에서 반드시 할당됨
            bool moving = (localMove.x * localMove.x + localMove.z * localMove.z) > 0.0004f; // ~0.02²
            if (moving)
            {
                if (!_lieMoveOn)
                {
                    SoundManager.i.PlayLoopSFX(PlayerSfx.LieMoving);
                    _lieMoveOn = true;
                }
            }
            else if (_lieMoveOn)
            {
                SoundManager.i.StopSFX(PlayerSfx.LieMoving);
                _lieMoveOn = false;
            }
        }
        else if (_lieMoveOn)
        {
            SoundManager.i.StopSFX(PlayerSfx.LieMoving);
            _lieMoveOn = false;
        }

        // 회전
        if (isCrawling)
        {
            // 엎드릴 땐 카메라가 보는 쪽으로만 바라보게
            Vector3 face = moveForward;         // 카메라 전방
            if (face.sqrMagnitude > 0.0001f)
            {
                Quaternion faceRot = Quaternion.LookRotation(face, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, faceRot, rotSpeed * 100f * Time.deltaTime);
            }
        }
        else if (currentMoveInput.sqrMagnitude > 0.001f)
        {
            // 기존: 이동 방향을 바라보게
            Quaternion targetRot = Quaternion.LookRotation(currentMoveInput);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotSpeed * 100f * Time.deltaTime);
        }

        wasAltPressedLastFrame = isAlt;
        if (justReleasedAlt && !isAlt) justReleasedAlt = false;

        // 벽 잡기 시작
        if (KeyBindings.GetKeyDown(GameAction.Climb) && canClimbZone && !isHolding && !isClimbing && !IsClimbBlockedByWeapon())
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
            // 엎드리는 애니메이션 중 앉기 무시
            if (isCrawlAnimating) return;

            if (isCrawling)
            {
                // 엎드림 → 즉시 앉기 전환
                SwitchCrawlToCrouch();
                crouchCooldownTimer = crouchCooldown;
                SoundManager.i?.PlaySFX(PlayerSfx.CrouchToggle, SfxBus.Effect, 1f);
                return;
            }

            // 일반 토글(서기↔앉기)
            bool wantCrouch = !isCrouching;

            // 서기로 갈 때만 천장 체크
            if (!wantCrouch && !CanStandUp()) return;

            isCrouching = wantCrouch;
            animator.SetBool("IsCrouching", isCrouching);
            poseImgPat.sprite = isCrouching ? poseImg[1] : poseImg[0];
            crouchCooldownTimer = crouchCooldown;

            SoundManager.i?.PlaySFX(PlayerSfx.CrouchToggle, SfxBus.Effect, 1f);
            ApplyCrouchCollider(isCrouching);
        }

        // 엎드리기
        if (KeyBindings.GetKeyDown(GameAction.Crawl) && crouchCooldownTimer <= 0f)
        {
            ToggleCrawl();
            SoundManager.i?.PlaySFX(PlayerSfx.LieDown, SfxBus.Effect, 1f, 1.2f);
            // 크로스 토글 간 충돌 방지 쿨다운 (원하는 값으로)
            crouchCooldownTimer = 0.25f;
        }

        // 엎드린 동안엔 달리기/점프/등반 이동 잠금
        if (isCrawling)
        {
            isRunning = false;                 // 달리기 금지
            animator.SetBool("IsCrouching", false); // 앉기와 상태 충돌 방지
        }

        // 속도
        float moveSpeed = isRunning ? speed * runSpeed : speed;
        if (isCrouching) moveSpeed *= 0.55f;
        if (isCrawling) moveSpeed *= crawlSpeedMul;
        // 무기별 속도
        float weaponMul = handSpeedMul;

        if (IsWeaponShown() && WeaponManager.i != null)
        {
            switch (WeaponManager.i.SelectedWeapon)
            {
                case WeaponManager.WeaponType.Gun:
                    weaponMul = gunSpeedMul;
                    break;
                case WeaponManager.WeaponType.Crowbar:
                    weaponMul = crowbarSpeedMul;
                    break;
                case WeaponManager.WeaponType.Bat:
                    weaponMul = batSpeedMul;
                    break;
                case WeaponManager.WeaponType.None:
                default:
                    weaponMul = handSpeedMul;
                    break;
            }
        }
        moveSpeed *= weaponMul;
        currentMoveSpeed = moveSpeed;

        // 소리 범위 알림
        CheckNearbyEnemies();

        // 클리어
        if (canAttack && _sceneInputGraceTimer <= 0f && !IsPointerOverUI() && KeyBindings.GetKeyDown(GameAction.Attack) && CanAttackWithWeapon())
        {
            _gameClearArmed = true;
            TriggerAttackByCurrentWeapon();
        }
        //ShowPausePanel(gameClearUI);

        // 미니맵
        if (KeyBindings.GetKeyDown(GameAction.MiniMap))
        {
            minimapPanel?.SetActive(true);

            string currentScene = SceneManager.GetActiveScene().name;

            if (minimapInside != null) minimapInside.SetActive(false);
            if (minimapOut != null) minimapOut.SetActive(false);

            // 현재 씬 이름에 따라 해당 미니맵만 켜기
            if (currentScene == "1_stage_inside" && minimapInside != null)
            {
                minimapInside.SetActive(true);
            }
            else if (currentScene == "1_stage_out" && minimapOut != null)
            {
                minimapOut.SetActive(true);
            }
        }
        if (KeyBindings.GetKeyUp(GameAction.MiniMap))
        {
            minimapPanel?.SetActive(false);
        }

        // 미션 받기
        if (canTakeMission && EPressed() && !isCrawling) ShowPausePanel(missionUI);

        // ESC 옵션 토글
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (RebindKeyButton.IsAnyListening)
            {
                RebindKeyButton.CancelAll();
                return;
            }

            
            if (gameClearUI && gameClearUI.activeSelf) { HidePausePanel(gameClearUI); return; }
            if (gameOverUI && gameOverUI.activeSelf) { HidePausePanel(gameOverUI); return; }
            if (weaponChangePanel && weaponChangePanel.activeSelf) { HidePausePanel(weaponChangePanel); return; }
            if (jewelryUI && jewelryUI.activeSelf) { HidePausePanel(jewelryUI); return; }
            if (guestBoxUI1 && guestBoxUI1.activeSelf) { HidePausePanel(guestBoxUI1); return; }
            if (guestBoxUI2 && guestBoxUI2.activeSelf) { HidePausePanel(guestBoxUI2); return; }
            if (guestBoxUI3 && guestBoxUI3.activeSelf) { HidePausePanel(guestBoxUI3); return; }
            ShowPausePanel(optionUI);
        }

        // 암살
        if (_sceneInputGraceTimer <= 0f && !IsPointerOverUI() && KeyBindings.GetKeyDown(GameAction.Attack) && canKill && !isCrawling && CanAttackWithWeapon())
        {
            if (canKill && killTarget != null)
            {
                StartAttack(killTarget);
            }
            else if (!AnyPauseOpen()) // 클리어/옵션 UI 떠 있으면 무시
            {
                PlayAttackByWeapon();
            }
        }

        // 문열기
        if (nearDoorLeaves.Count > 0 && KeyBindings.GetKeyDown(GameAction.Interaction) && !isCrawling)
        {
            var leaves = new List<Transform>(nearDoorLeaves);

            var first = leaves[0];
            if (!doors.TryGetValue(first, out var baseData)) goto SkipDoor;

            // 열쇠 부족이면 리턴
            if (baseData.requiresBedroomKey)
            {
                if (KeyManager.i == null || !KeyManager.i.canInBedroom)
                {
                    // SoundManager.i?.PlaySFX(PlayerSfx.DoorLocked, SfxBus.Effect, 1f);
                    return;
                }
            }

            bool targetOpen = !baseData.isOpen;

            foreach (var leaf in leaves)
            {
                if (!leaf) continue;
                if (!doors.TryGetValue(leaf, out var data)) continue;

                Quaternion from = leaf.localRotation;
                Quaternion to = targetOpen ? data.open : data.closed;

                if (doorRoutines.TryGetValue(leaf, out var running) && running != null)
                    StopCoroutine(running);

                var co = StartCoroutine(RotateLocal_Smooth(leaf, from, to, doorRotateDuration));
                doorRoutines[leaf] = co;

                data.isOpen = targetOpen;
                doors[leaf] = data;
            }
        }
        SkipDoor:;

        // 무기 선택창
        if (choiceWeapon && EPressed() && !isCrawling)
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

            ButtonControl button = GetComponentInChildren<ButtonControl>();
            button.canNextStage = true;

            blockInput = true;
            currentMoveInput = Vector3.zero;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);

            animator.ResetTrigger("WeaponPick");
            animator.SetTrigger("WeaponPick");

            SoundManager.i.PlaySFX(PlayerSfx.ChestOpen, SfxBus.Effect, 1f);
        }

        // 무기 스위치 가능해지면 패널 닫기
        if (WeaponManager.i && WeaponManager.i.canCrowbarSwitch && !canWeaponSwitch)
        {
            canWeaponSwitch = true;
            if (weaponChangePanel && weaponChangePanel.activeSelf) HidePausePanel(weaponChangePanel);
            SoundManager.i.PlaySFX(PlayerSfx.ChestClose, SfxBus.Effect, 1f);
        }

        // 무기 바꾸기
        if (Input.GetKeyDown(KeyCode.Alpha1) && canWeaponSwitch && !isCrawling)    // 맨손
        {
            if (IsEquipBusy()) return;

            if (IsWeaponShown())
            {
                if (equipInProgress) return;
                if (!WeaponManager.i) return;

                var cur = WeaponManager.i.SelectedWeapon;
                if (cur == WeaponManager.WeaponType.None) return;

                _putAwayViaEquip = true;
                equipInProgress = true;

                if (takeWeaponLayer < 0)
                    takeWeaponLayer = animator.GetLayerIndex("TakeWeapon");
                if (takeWeaponLayer >= 0) animator.SetLayerWeight(takeWeaponLayer, 1f);

                if (gripLayer >= 0) animator.CrossFade(gripIdleHash, 0.08f, gripLayer, 0f);
                if (rightArmLayer >= 0) FadeRightArmLayer(0f, 0.10f);

                SoundManager.i.PlaySFX(PlayerSfx.WeaponDraw, SfxBus.Effect, 1f);
                int stateHash = GetEquipStateHash(cur);
                animator.speed = 1f;
                animator.CrossFade(stateHash, 0.05f, takeWeaponLayer, 0f);
                return;
            }

            // 이미 맨손 상태면 가볍게 손 포즈만 유지
            if (gripLayer >= 0) animator.CrossFade(gripIdleHash, 0.1f, gripLayer, 0f);
            if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, 0f);

            if (weapon)
            {
                weapon.SetActive(false);
                for (int i = 0; i < weapon.transform.childCount; i++)
                    weapon.transform.GetChild(i).gameObject.SetActive(false);
            }

            if (takeWeaponLayer >= 0) animator.SetLayerWeight(takeWeaponLayer, 0f);
            equipInProgress = false;
        }
        if (Input.GetKeyDown(KeyCode.Alpha2) && canWeaponSwitch && !isCrawling)    // 무기
        {
            if (IsEquipBusy()) return;

            // 이미 들고 있으면 아무 것도 하지 않음
            if (IsWeaponShown()) return;

            if (_selectedWeaponChildIndex < 0 || pendingEquipType == WeaponManager.WeaponType.None) return;
            if (equipInProgress) return;
            equipInProgress = true;

            SoundManager.i.PlaySFX(PlayerSfx.WeaponDraw, SfxBus.Effect, 1f);
            
            if (takeWeaponLayer < 0)
                takeWeaponLayer = animator.GetLayerIndex("TakeWeapon");
            if (takeWeaponLayer >= 0) animator.SetLayerWeight(takeWeaponLayer, 1f);

            int stateHash = GetEquipStateHash(pendingEquipType);
            animator.CrossFade(stateHash, 0.05f, takeWeaponLayer, 0f);
        }

        // 세탁실 미션
        if (inLaundryRange && hasLaundryMission && EPressed() && !isCamBlending && laundryCamTarget)
        {
            EnterLaundryView();
            StartCoroutine(BlendMainCameraTo(laundryCamTarget, laundryCamBlend));
        }
        if (LaundryPuzzleManager.i.puzzleCleared && !_laundryRangeDisabled)
        {
            _laundryRangeDisabled = true;

            var laundryRoot = GameObject.Find("LaundryPuzzle");
            if (laundryRoot != null)
            {
                // Range 콜라이더 비활성화
                var rangeTr = laundryRoot.transform.Find("Range");
                if (rangeTr != null)
                {
                    var col = rangeTr.GetComponent<BoxCollider>();
                    if (col != null) col.enabled = false;
                }

                // BedRoomKey 활성화
                var keyTr = laundryRoot.transform.Find("BedRoomKey1");
                if (keyTr != null)
                    keyTr.gameObject.SetActive(true);
            }

            hasLaundryMission = false;
            inLaundryRange = false;
        }

        // 휴게실 미션
        if (inFoyerRange && hasFoyerMission && EPressed() && !isCamBlending && foyerCamTarget)
        {
            EnterFoyerView();
            StartCoroutine(BlendMainCameraTo(foyerCamTarget, foyerCamBlend));
        }

        // 서재 미션
        if (inStudyRange && hasStudyMission && EPressed() && !isCamBlending && studyCamTarget)
        {
            EnterFoyerView();
            StartCoroutine(BlendMainCameraTo(studyCamTarget, foyerCamBlend));
        }

        // 서재 미션 (정답 창)
        if (inStudyResult && hasStudyResult && EPressed() && !isCamBlending && studyResultCamTarget)
        {
            EnterFoyerView();
            StartCoroutine(BlendMainCameraTo(studyResultCamTarget, foyerCamBlend));

            var ui = FindObjectOfType<InventoryBooksUI>(true);
            if (ui) ui.Open();
        }

        // 드레스룸 미션
        if(inJewelryRange && EPressed())
        {
            ShowOverlayPanel_NoPause(jewelryUI);
        }

        // 게스트룸 미션
        if (inGuestRange && hasGuestMission && EPressed() && !isCamBlending && foyerCamTarget)
        {
            EnterFoyerView();
            StartCoroutine(BlendMainCameraTo(guestCamTarget, foyerCamBlend));
        }

        // 게스트룸 미션 (여행 가방)
        if ((inGuestBox1 || inGuestBox2 || inGuestBox3) && EPressed())
        {
            if(inGuestBox1)
                ShowOverlayPanel_NoPause(guestBoxUI1);
            if (inGuestBox2)
                ShowOverlayPanel_NoPause(guestBoxUI2);
            if (inGuestBox3)
                ShowOverlayPanel_NoPause(guestBoxUI3);
        }

        // 식당 미션
        if (EPressed())
        {
            if (TryInteract())   // 먹기 or 슬롯 배치 성공 시
                return;
        }

        // 치트
        if (Input.GetKeyDown(KeyCode.P))
        {
            transform.position = new Vector3(-0.956f, 5.022f, 19.404f);
            KeyManager.i.canInBedroom = true;
        }
    }

    // 엎드려있는 상태에서 앉기
    void SwitchCrawlToCrouch()
    {
        if (!CanCrawlToCrouch())    return;
        
        // 1) 상태 플래그
        isCrawling = false;                 // 크롤 종료
        isCrouching = true;                 // 앉기 시작
        poseImgPat.sprite = poseImg[1];

        // 2) 애니메이터 파라미터
        animator.ResetTrigger("CrawlDown");
        animator.ResetTrigger("CrawlUp");
        animator.SetBool("IsCrawling", false);
        animator.SetBool("IsCrouching", true);

        // 전용 트리거를 만들었다면 같이 쏴주기
        animator.SetTrigger("CrawlToCrouch");

        // 3) 콜라이더 보간
        ApplyColliderPose(boxSizeCrouch, boxCenterCrouch, 0.08f);

        (cmov ?? CameraMov.i)?.SetCrawl(false);

        // 4) 이동/속도 등 보정
        isRunning = false;

        if (_lieMoveOn) { SoundManager.i.StopSFX(PlayerSfx.LieMoving); _lieMoveOn = false; }

        SetCrawlCamByState(false);
    }

    // 무기가 손에 있을 때 벽타기 금지
    bool IsClimbBlockedByWeapon()
    {
        if (!IsWeaponShown() || WeaponManager.i == null)
            return false;

        switch (WeaponManager.i.SelectedWeapon)
        {
            case WeaponManager.WeaponType.Crowbar:
            case WeaponManager.WeaponType.Bat:
                return true;
            default:
                return false;
        }
    }

    // 엎드리기
    void ToggleCrawl()
    {
        // 이미 다른 컷신/등반/홀드 중이면 무시
        if (blockInput || isHolding || isClimbing) return;

        if (!isCrawling)
        {
            // 서거나 앉은 상태 → 엎드리기
            isCrawlAnimating = true;
            isCrawling = true;
            isCrouching = false;                       // crouch와 동시 해제
            animator.SetBool("IsCrouching", false);
            poseImgPat.sprite = poseImg[2];

            animator.ResetTrigger("CrawlUp");
            animator.SetTrigger("CrawlDown");
            animator.SetBool("IsCrawling", true);

            // 콜라이더 즉시/빠르게 낮추기
            ApplyColliderPose(boxSizeCrawl, boxCenterCrawl, 0.10f);

            ForceUnequipWeapon();

            CameraMov.i?.SetCrawl(true, crawlCamDown);
            if (_lieMoveOn) { SoundManager.i.StopSFX(PlayerSfx.LieMoving); _lieMoveOn = false; }
            SetCrawlCamByState(true);
        }
        else
        {
            // 엎드림 → 해제(서기)
            if (!CanStandUp()) return; // 머리 위에 막히면 해제 금지

            isCrawlAnimating = false;
            isCrawling = false;

            animator.ResetTrigger("CrawlDown");
            animator.SetTrigger("CrawlUp");
            animator.SetBool("IsCrawling", false);
            poseImgPat.sprite = poseImg[0];

            // 콜라이더 되돌리기 (Up 애니 시작과 동시에)
            ApplyColliderPose(boxSizeStand, boxCenterStand, 0.10f);

            (cmov ?? CameraMov.i)?.SetCrawl(false);

            SetCrawlCamByState(false);
        }
    }

    // 세탁실 미션 시점 변경 시 플레이어 고정
    void EnterLaundryView()
    {
        if (_lieMoveOn) { SoundManager.i.StopSFX(PlayerSfx.LieMoving); _lieMoveOn = false; }

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
        if (CameraMov.i) CameraMov.i.enabled = false;

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
        if (_lieMoveOn) { SoundManager.i.StopSFX(PlayerSfx.LieMoving); _lieMoveOn = false; }

        // 조작 잠금
        blockInput = true;
        currentMoveInput = Vector3.zero;
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 0f);
        animator.SetFloat("Speed", 0f);
        rb.velocity = Vector3.zero;
        nearNPC.gameObject.SetActive(false);

        // 현재 메인 카메라 포즈 저장
        var cam = Camera.main;
        if (cam)
        {
            _preViewCamPos = cam.transform.position;
            _preViewCamRot = cam.transform.rotation;
            _preViewCamFov = cam.fieldOfView;
        }

        // 카메라 추적 비활성화
        if (CameraMov.i) CameraMov.i.enabled = false;

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

        FinishExitLaundryView();

        var ui = FindObjectOfType<InventoryBooksUI>(true);
        if (ui) ui.Close();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        nearNPC.gameObject.SetActive(true);

        if (CameraMov.i)
        {
            CameraMov.i.enabled = true;
        }
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
        tr.SetPositionAndRotation(_preViewCamPos, _preViewCamRot);
        cam.fieldOfView = _preViewCamFov;

        // 퍼즐뷰 플래그/입력 잠금 해제
        FinishExitLaundryView();

        // 바로 카메라 조작 가능하게: 켜고 활성화 블렌드만
        if (CameraMov.i)
        {
            CameraMov.i.transform.SetPositionAndRotation(tr.position, tr.rotation);
            CameraMov.i.enabled = true;
            CameraMov.i.BeginBlendIn(restoreCamBlend);

            // 입력이 잠깐(예: 0.35초) 없으면 그때만 부드럽게 리센터
            CameraMov.i.RecenterIfNoMouseFor(0.35f, restoreCamBlend);
        }
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
        if (CameraMov.i) CameraMov.i.enabled = false;

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

    // 식당 미션
    bool TryInteract()
    {
        var query = QueryTriggerInteraction.Collide;

        // 1) 정면 레이
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask, query))
        {
            var rspot = hit.collider.GetComponentInParent<ReturnSpot>();
            if (rspot != null && rspot.TryInteract()) return true;

            var slot = hit.collider.GetComponent<SlotPlate>();
            if (slot != null && slot.TryOpenUI()) return true;

            var pickup = hit.collider.GetComponent<PickupItem>();
            if (pickup != null && pickup.TryPickupOrReturn(this)) return true;

            var candle = hit.collider.GetComponent<CandleSpot>() ?? hit.collider.GetComponentInParent<CandleSpot>();
            if (candle != null && candle.TryInteract(this)) return true;
        }

        // 2) 주변 보정(반경)
        Collider[] cols = Physics.OverlapSphere(transform.position, 1f, interactMask, query);
        foreach (var col in cols)
        {
            var rspot = col.GetComponentInParent<ReturnSpot>();
            if (rspot != null && rspot.TryInteract()) return true;

            var slot = col.GetComponent<SlotPlate>();
            if (slot != null && slot.TryOpenUI()) return true;

            var pickup = col.GetComponent<PickupItem>();
            if (pickup != null && pickup.TryPickupOrReturn(this)) return true;

            var candle = col.GetComponent<CandleSpot>() ?? col.GetComponentInParent<CandleSpot>();
            if (candle != null && candle.TryInteract(this)) return true;
        }

        return false;
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
    private void StartAttack(EnemyMov enemy)
    {
        if (enemy == null) return;

        enemy.FreezeForAttack(true);

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

        if (_crawlCamOn && camT)
        {
            // camLocalStart의 월드 위치
            Vector3 baseWorld = camT.parent ? camT.parent.TransformPoint(camLocalStart) : camLocalStart;
            Vector3 targetWorld = baseWorld + Vector3.down * Mathf.Abs(crawlCamDown);

            // 부드럽게 월드 포지션 보정
            float k = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.01f, crawlCamLerp));
            camT.position = Vector3.Lerp(camT.position, targetWorld, k);
        }
    }

    void UpdateMiniPos()
    {
        if (!miniPos) return;
        Vector3 p = transform.position;
        miniPos.transform.position = new Vector3(p.x, p.y + miniPosYOffset, p.z);
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

        EnterCrawlSilently();

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
        if (ghostMode)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        if (blockInput || isClimbing || isHolding)
        {
            rb.velocity = Vector3.zero;
            return;
        }

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

    // 무기 선택 만
    private void ApplyChosenWeaponImmediate(WeaponManager.WeaponType type)
    {
        if (!weapon || !animator) return;

        pendingEquipType = type;

        switch (type)
        {
            case WeaponManager.WeaponType.Gun: _selectedWeaponChildIndex = 0; break;
            case WeaponManager.WeaponType.Crowbar: _selectedWeaponChildIndex = 1; break;
            case WeaponManager.WeaponType.Bat: _selectedWeaponChildIndex = 2; break;
            default: _selectedWeaponChildIndex = -1; break;
        }

        // 상자에서 선택만 하고, 실제 장착은 하지 않는다
        canWeaponSwitch = true;

        // 안전하게 다 꺼두기
        if (weapon)
        {
            for (int i = 0; i < weapon.transform.childCount; i++)
                weapon.transform.GetChild(i).gameObject.SetActive(false);
            weapon.SetActive(false);
        }

        // 패널이 떠있다면 닫아도 됨
        if (weaponChangePanel && weaponChangePanel.activeSelf)
            HidePausePanel(weaponChangePanel);

        _weaponPickFlowActive = false;

        // 손 포즈는 일단 Idle로(실장 취향)
        if (gripLayer >= 0) animator.CrossFade(gripIdleHash, 0.08f, gripLayer, 0f);
        if (rightArmLayer >= 0) animator.SetLayerWeight(rightArmLayer, 0f);
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
        _wasCrouchingBeforeClimb = isCrouching;
        animator.SetBool("IsCrouching", false);

        float wallTop = hit.collider.bounds.max.y;
        float footY = box ? box.bounds.min.y : transform.position.y;
        float wallHeight = Mathf.Max(0f, wallTop - footY);

        remainingWallHeight = wallHeight;
        detectedWallHeight = wallHeight;

        if (wallHeight <= 1.2f)
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

    // 벽을 탄 후 엎드리기
    void EnterCrawlSilently()
    {
        // 애니 트리거 충돌 방지
        animator?.ResetTrigger("CrawlDown");
        animator?.ResetTrigger("CrawlUp");

        // 상태 플래그
        isCrawling = true;
        isCrouching = false;
        isRunning = false;

        // 애니 파라미터
        animator?.SetBool("IsCrouching", false);
        animator?.SetBool("IsCrawling", true);

        // 무기는 넣어두기
        ForceUnequipWeapon();

        // 콜라이더/카메라를 즉시 엎드림 세팅으로
        ApplyColliderPose(boxSizeCrawl, boxCenterCrawl, 0.10f);
        (cmov ?? CameraMov.i)?.SetCrawl(true, crawlCamDown);
        SetCrawlCamByState(true);
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

        EnterCrawlSilently();
    }

    public void StartClimbFromHold(float duration)
    {
        ClearLandTriggers();
        isHolding = false;
        canStartClimb = false;
        climbTimer = 0f;
        climbDuration = duration;

        SetClimbCollisionEnabled(false);

        climbStartPos = transform.position;
        climbStartRot = transform.rotation;

        float climbHeight = Mathf.Clamp(detectedWallHeight + 0.15f, 1f, 3.5f);

        climbTargetPos = holdingStartPos + Vector3.up * climbHeight;
        climbTargetRot = transform.rotation;

        animator.SetBool("Hold", false);
        animator.SetTrigger("Climb");

        isClimbing = true;
        rb.useGravity = false;

        SoundManager.i?.PlaySFX(PlayerSfx.ClimbStart, SfxBus.Effect, 1f, 1.3f);
    }

    void SetClimbCollisionEnabled(bool enabled)
    {
        if (!box) return;

        if (enabled)
        {
            _colDisableDepth = Mathf.Max(0, _colDisableDepth - 1);
            if (_colDisableDepth == 0) box.enabled = true;
        }
        else
        {
            _colDisableDepth++;
            box.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DonRun")) { donRunZoneCount++; UpdateRunLock(); }
        if (other.CompareTag("ClimbZone")) canClimbZone = true;
        if (other.CompareTag("Boss"))
        {
            canAttack = true;
            _gameClearArmed = false;
            _gameClearShown = false;
        }

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

        if (other.CompareTag("Door")) BindDoor(other, +1);
        if (other.CompareTag("MinDoor")) BindDoor(other, -1);
        if (other.CompareTag("BedRoomDoor")) BindDoor(other, +1, true);
        if (other.CompareTag("BedRoomMinDoor")) BindDoor(other, -1, true);

        if (other.CompareTag("WeaponBox")) 
        {
            nearNPC.gameObject.SetActive(true);
            choiceWeapon = true;
            boxObject = other.gameObject;
        }

        if (other.CompareTag("laundryRange"))
        {
            nearNPC.gameObject.SetActive(true);
            hasLaundryMission = true;
            inLaundryRange = true;
        }

        if (other.CompareTag("foyerRange"))
        {
            nearNPC.gameObject.SetActive(true);
            hasFoyerMission = true;
            inFoyerRange = true;
        }

        if (other.CompareTag("StudyRange"))
        {
            nearNPC.gameObject.SetActive(true);
            hasStudyMission = true;
            inStudyRange = true;
        }

        if (other.CompareTag("StudyResultRange"))
        {
            nearNPC.gameObject.SetActive(true);
            hasStudyResult = true;
            inStudyResult = true;
        }

        if (other.CompareTag("DressRange"))
        {
            nearNPC.gameObject.SetActive(true);
            inJewelryRange = true;
        }

        if (other.CompareTag("GuestRoomRange"))
        {
            nearNPC.gameObject.SetActive(true);
            hasGuestMission = true;
            inGuestRange = true;
        }

        if (other.CompareTag("GuestBox1"))
        {
            nearNPC.gameObject.SetActive(true);
            inGuestBox1 = true;
        }
        if (other.CompareTag("GuestBox2"))
        {
            nearNPC.gameObject.SetActive(true);
            inGuestBox2 = true;
        }
        if (other.CompareTag("GuestBox3"))
        {
            nearNPC.gameObject.SetActive(true);
            inGuestBox3 = true;
        }

        if (other.CompareTag("StairRange"))
        {
            Physics.gravity = new Vector3(0, -100f, 0);
        }

        var footstepProxy = GetComponent<PlayerFootstepProxy>();
        if (other.CompareTag("IndoorZone"))
            footstepProxy.SetEnvironment(FootEnv.Indoor);
        else if (other.CompareTag("OutdoorZone"))
            footstepProxy.SetEnvironment(FootEnv.Outdoor);
        else if (other.CompareTag("SerwerZone"))
            footstepProxy.SetEnvironment(FootEnv.InSerwer);

        if (other.CompareTag("SecretRange"))
        {
            inSecretRange = true;
            animator?.SetBool("InSecret", true);
        }

        if (other.CompareTag("Manhole")) nearNPC.gameObject.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("DonRun")) { donRunZoneCount = Mathf.Max(0, donRunZoneCount - 1); UpdateRunLock(); }
        if (other.CompareTag("ClimbZone")) canClimbZone = false;
        if (other.CompareTag("Boss"))
        {
            canAttack = false;
            _gameClearArmed = false;
            _gameClearShown = false;
        }

        if (other.CompareTag("NPC")) { nearNPC.gameObject.SetActive(false); canTakeMission = false; }

        if (other.CompareTag("Attack"))
        {
            var enemy = other.GetComponentInParent<EnemyMov>() ?? other.GetComponent<EnemyMov>();
            if (enemy == killTarget) { killTarget = null; canKill = false; }
        }

        if (other.CompareTag("Door") || other.CompareTag("MinDoor") ||
        other.CompareTag("BedRoomDoor") || other.CompareTag("BedRoomMinDoor"))
        {
            var leaf = FirstLeafChild(other.transform);
            if (leaf) nearDoorLeaves.Remove(leaf);
        }

        if (other.CompareTag("WeaponBox")) { choiceWeapon = false; nearNPC.gameObject.SetActive(false); }

        if (other.CompareTag("laundryRange"))
        {
            hasLaundryMission = false;
            inLaundryRange = false;
            nearNPC.gameObject.SetActive(false);
        }

        if (other.CompareTag("foyerRange"))
        {
            hasFoyerMission = false;
            inFoyerRange = false;
            nearNPC.gameObject.SetActive(false);
        }

        if (other.CompareTag("StudyRange"))
        {
            hasStudyMission = false;
            inStudyRange = false;
            nearNPC.gameObject.SetActive(false);
        }

        if (other.CompareTag("StudyResultRange"))
        {
            hasStudyResult = false;
            inStudyResult = false;
            nearNPC.gameObject.SetActive(false);
        }

        if (other.CompareTag("DressRange"))
        {
            inJewelryRange = false;
            nearNPC.gameObject.SetActive(false);
        }

        if (other.CompareTag("GuestRoomRange"))
        {
            hasGuestMission = false;
            inGuestRange = false;
            nearNPC.gameObject.SetActive(false);
        }

        if (other.CompareTag("GuestBox1"))
        {
            inGuestBox1 = false;
            nearNPC.gameObject.SetActive(false);
        }
        if (other.CompareTag("GuestBox2"))
        {
            inGuestBox2 = false;
            nearNPC.gameObject.SetActive(false);
        }
        if (other.CompareTag("GuestBox3"))
        {
            inGuestBox3 = false;
            nearNPC.gameObject.SetActive(false);
        }

        if (other.CompareTag("StairRange"))
        {
            Physics.gravity = new Vector3(0, -9.81f, 0);
        }

        if (other.CompareTag("SecretRange"))
        {
            inSecretRange = false;
            animator?.SetBool("InSecret", false);
        }

        if(other.CompareTag("Manhole")) nearNPC.gameObject.SetActive(false);
    }

    // 문열기
    static Transform FirstLeafChild(Transform t)
    {
        var cur = t;
        cur = cur.GetChild(0);
        return cur;
    }

    void BindDoor(Collider other, int sign, bool requiresBedroomKey = false)
    {
        var leaf = FirstLeafChild(other.transform);
        if (!leaf) return;

        nearDoorLeaves.Add(leaf);

        if (!doors.TryGetValue(leaf, out var data))
        {
            var e = leaf.localEulerAngles;
            data = new DoorData
            {
                closed = leaf.localRotation,
                open = Quaternion.Euler(e.x, e.y, e.z + sign * doorZDelta),
                isOpen = Mathf.Abs(Mathf.DeltaAngle(e.z, e.z + sign * doorZDelta)) < 5f,
                sign = sign,
                requiresBedroomKey = requiresBedroomKey
            };
        }
        else
        {
            if (data.sign != sign)
            {
                var ce = data.closed.eulerAngles;
                data.open = Quaternion.Euler(ce.x, ce.y, ce.z + sign * doorZDelta);
                data.sign = sign;
            }

            data.requiresBedroomKey |= requiresBedroomKey;
        }

        doors[leaf] = data;
    }

    IEnumerator RotateLocal_Smooth(Transform tr, Quaternion from, Quaternion to, float duration)
    {
        float t = 0f;
        duration = Mathf.Max(0.0001f, duration);
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            tr.localRotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }
        tr.localRotation = to;
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

        SetClimbCollisionEnabled(true);
        ApplyPostClimbPose();

        SoundManager.i?.PlaySFX(PlayerSfx.ClimbEnd, SfxBus.Effect, 1f);
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

    // 어디서 벽을 타냐 기준
    void ApplyPostClimbPose()
    {
        if (inSecretRange)
        {
            // 엎드린 상태
            if (!isCrawling)
            {
                EnterCrawlSilently();
            }
        }
        else
        {
            // 앉은 상태
            if (isCrawling)
            {
                if (CanCrawlToCrouch())
                {
                    SwitchCrawlToCrouch();
                }
            }
            else
            {
                isCrouching = true;
                animator.SetBool("IsCrouching", true);
                animator.SetBool("IsCrawling", false);

                ApplyCrouchCollider(true);
                SetCrawlCamByState(false);
            }
        }
    }

    void StartBoxJump(Vector3 wallPoint, Vector3 wallNormal, float height)
    {
        if (!_boxJumpSfxPlayed)
        {
            SoundManager.i?.PlaySFX(PlayerSfx.ClimbStart, SfxBus.Effect, 1f, 1.5f);
            _boxJumpSfxPlayed = true;
        }
        ClearLandTriggers();
        isCrouching = false;
        animator.SetBool("IsCrouching", false);

        blockInput = true;
        isHolding = false;
        isClimbing = false;
        rb.useGravity = false;

        _boxJumpWantsCrawl = inSecretRange;

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
        SetClimbCollisionEnabled(false);

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

        if (_boxJumpWantsCrawl)
            EnterCrawlSilently();

        _boxJumpWantsCrawl = false;
        blockInput = false;
        SetClimbCollisionEnabled(true);
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

            upOffset = Mathf.Clamp(wallHeight * 0.6f, 0.6f, 1.2f);
        }

        EnterCrawlSilently();
        Vector3 targetPos = transform.position + Vector3.up * upOffset + transform.forward * 0.1f;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    public void MoveToBoxTopRemaining(float duration)
    {
        float upOffset = Mathf.Clamp(lastBoxWallRemainingHeight * 0.6f, 0.4f, 1.2f);
        Vector3 targetPos = transform.position + Vector3.up * upOffset;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    public void OnBoxJumpEnd()
    {
        rb.useGravity = true;
        rb.isKinematic = false;
        blockInput = false;

        ApplyPostClimbPose();

        _boxJumpSfxPlayed = false;
        SetClimbCollisionEnabled(true);
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

    // 월드 좌표로 변환
    Vector3 WorldDownToLocal(float dist)
    {
        Vector3 wDelta = Vector3.down * Mathf.Abs(dist); // 월드 기준 아래로
        if (camT && camT.parent)
            return camT.parent.InverseTransformVector(wDelta); // 부모 로컬축으로 변환
        return wDelta; // 부모 없으면 로컬==월드
    }

    // 엎드릴때 캠 변화 상태 스위처(중복 호출 방지)
    void SetCrawlCamByState(bool on)
    {
        if (!camT) return;
        if (_crawlCamOn == on) return;
        _crawlCamOn = on;

        if (camYCo != null) StopCoroutine(camYCo);

        Vector3 targetLocal = on
            ? camLocalStart + WorldDownToLocal(crawlCamDown)
            : camLocalStart;

        camYCo = StartCoroutine(LerpCamLocalPos(targetLocal));
    }

    IEnumerator LerpCamLocalPos(Vector3 to)
    {
        Vector3 from = camT.localPosition;
        float t = 0f, d = Mathf.Max(0.01f, crawlCamLerp);
        while (t < 1f)
        {
            t += Time.deltaTime / d;
            camT.localPosition = Vector3.Lerp(from, to, t);
            yield return null;
        }
        camT.localPosition = to;
        camYCo = null;
    }

    bool HasHeadroomFor(Vector3 targetSize, Vector3 targetCenter)
    {
        if (!box) return true;

        float curTop = box.center.y + box.size.y * 0.5f;
        float targetTop = targetCenter.y + targetSize.y * 0.5f;
        float deltaTop = targetTop - curTop;
        if (deltaTop <= 0.001f) return true; // 더 낮아지거나 같으면 OK

        // 현재 top~목표 top 사이 슬라이스만 검사(겹치면 막힘)
        float sliceCenterLocalY = curTop + deltaTop * 0.5f;
        Vector3 localCenter = new Vector3(box.center.x, sliceCenterLocalY, box.center.z);
        Vector3 worldCenter = transform.TransformPoint(localCenter);

        Vector3 half = new Vector3(
            box.size.x * 0.5f * transform.lossyScale.x,
            deltaTop * 0.5f * transform.lossyScale.y,
            box.size.z * 0.5f * transform.lossyScale.z
        );

        var hits = Physics.OverlapBox(worldCenter, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (!h || h.isTrigger) continue;
            if (h.transform.IsChildOf(transform)) continue; // 자기 자신 무시
            return false; // 뭔가 걸림
        }
        return true;
    }

    // 엎드림→앉기 가능 여부(크라우치 목표 치수로 검사)
    bool CanCrawlToCrouch() => HasHeadroomFor(boxSizeCrouch, boxCenterCrouch);

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
        PlayerMov.LockControls(showCursor: true);

        // EventSystem이 없으면 생성
        if (!EventSystem.current)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
            PlayerMov.UnlockControls(hideCursor: true);
            _weaponPickFlowActive = false;
        }
    }

    // 무기별 공격 모션
    void PlayAttackByWeapon()
    {
        // 진행 중엔 무시
        if (blockInput || isAssassinating || isHolding || isClimbing) return;
        if (!CanAttackWithWeapon()) return;
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

    public void BindCameraPivot(Transform pivot)
    {
        cameraPivot = pivot;
        if (cameraPivot)
        {
            camT = cameraPivot;
            camLocalStart = camT.localPosition;
        }
    }

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

        animator.speed = GetAttackAnimSpeed();
    }

    float GetAttackAnimSpeed()
    {
        if (WeaponManager.i == null) return 1f;

        switch (WeaponManager.i.SelectedWeapon)
        {
            case WeaponManager.WeaponType.Bat: return 0.7f;
            case WeaponManager.WeaponType.Crowbar: return 0.85f;
            case WeaponManager.WeaponType.Gun: return 1.0f;
            default: return 1.0f;
        }
    }

    // 공격 끝 시 (클립 마지막 프레임 근처)
    public void AE_AttackEnd()
    {
        rightArmMaxWeight = rightArmDefaultWeight;
        FadeRightArmLayer(rightArmDefaultWeight, 0.08f);

        animator.speed = 1.0f;
    }

    void RefreshInteractionHint()
    {
        if (!nearNPC) return;
        var keyName = KeyBindings.ToDisplay(KeyBindings.Get(GameAction.Interaction));
        nearNPC.text = $"'{keyName}' 를 눌러 상호작용.";
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

    // 퍼즐 전용 입력 잠금 (플레이어 & 카메라)
    public static void LockControls(bool showCursor = true)
    {
        blockInput = true;
        if (CameraMov.i) CameraMov.i.enabled = false;

        Cursor.visible = showCursor;
        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public static void UnlockControls(bool hideCursor = true)
    {
        blockInput = false;
        if (CameraMov.i) CameraMov.i.enabled = true;

        Cursor.visible = !hideCursor ? true : false;
        Cursor.lockState = hideCursor ? CursorLockMode.Locked : CursorLockMode.None;
    }

    // 공용 콜라이더 보간(기존 LerpCollider 활용)
    void ApplyColliderPose(Vector3 toSize, Vector3 toCenter, float dur)
    {
        if (!box) return;
        if (crouchColRoutine != null) StopCoroutine(crouchColRoutine);
        crouchColRoutine = StartCoroutine(LerpCollider(box, toSize, toCenter, dur));
    }

    // 엎드리기 끝 애니메이션
    public void AE_OnCrawlUpEnd()
    {
        if (!CanStandUp()) return;
        ApplyColliderPose(boxSizeCrawl, boxCenterCrawl, 0.05f);
        isCrawlAnimating = false;
    }

    public void BeginAnimLock()
    {
        _animLockDepth++;
        blockInput = true;
        currentMoveInput = Vector3.zero;
        if (rb)
        {
            rb.velocity = Vector3.zero;
        }
        animator?.SetFloat("MoveX", 0f);
        animator?.SetFloat("MoveY", 0f);
        animator?.SetFloat("Speed", 0f);
    }

    public void EndAnimLock()
    {
        _animLockDepth = Mathf.Max(0, _animLockDepth - 1);
        if (_animLockDepth == 0)
        {
            blockInput = false;
            if (rb)
            {
                // rb.isKinematic = false; // 위에서 켰다면 다시 끄기
            }
        }
    }

    // 무기 들기 애니메이션
    public void AE_OnEquipStart()
    {
        if (_putAwayViaEquip) return;

        if (takeWeaponLayer >= 0) animator.SetLayerWeight(takeWeaponLayer, 1f);

        switch (pendingEquipType)
        {
            case WeaponManager.WeaponType.Gun:
                if (gripLayer >= 0) animator.CrossFade(gripGunPoseHash, 0.08f, gripLayer, 0f);
                if (rightArmLayer >= 0) FadeRightArmLayer(rightArmDefaultWeight, 0.10f);
                break;
            case WeaponManager.WeaponType.Crowbar:
            case WeaponManager.WeaponType.Bat:
                if (gripLayer >= 0) animator.CrossFade(gripBatPoseHash, 0.08f, gripLayer, 0f);
                if (pendingEquipType == WeaponManager.WeaponType.Bat)
                    rightArmDefaultWeight = rightArmMaxWeight = 0.85f;
                if (rightArmLayer >= 0) FadeRightArmLayer(rightArmDefaultWeight, 0.10f);
                break;
        }
    }

    // 잡는 프레임 실제 무기 활성화
    public void AE_OnEquipWeapon()
    {
        if (_putAwayViaEquip) { return; }

        if (!weapon || _selectedWeaponChildIndex < 0) { equipInProgress = false; return; }

        weapon.SetActive(true);
        for (int i = 0; i < weapon.transform.childCount; i++)
            weapon.transform.GetChild(i).gameObject.SetActive(i == _selectedWeaponChildIndex);

        equipInProgress = false;
    }

    // 무기 집어넣기
    public void AE_OnEquipHide()
    {
        if (!_putAwayViaEquip) return;

        if (!weapon) return;
        for (int i = 0; i < weapon.transform.childCount; i++)
            weapon.transform.GetChild(i).gameObject.SetActive(false);
        weapon.SetActive(false);
    }

    public void EquipEndCleanup()
    {
        if (takeWeaponLayer >= 0) animator.SetLayerWeight(takeWeaponLayer, 0f);
        equipInProgress = false;

        if (_putAwayViaEquip)
        {
            // AE_OnEquipHide가 못 들어갔을 상황 대비 세이프가드
            if (weapon && weapon.activeSelf)
            {
                for (int i = 0; i < weapon.transform.childCount; i++)
                    weapon.transform.GetChild(i).gameObject.SetActive(false);
                weapon.SetActive(false);
            }

            _putAwayViaEquip = false;
            FadeRightArmLayer(0f, 0.08f); // 맨손 포즈 복귀
        }
    }

    public void AE_GameClearMoment()
    {
        if (!_gameClearArmed || _gameClearShown) return;

        _gameClearShown = true;   // 중복 방지
        ShowPausePanel(gameClearUI);  // 여기서 Time.timeScale=0, 일시정지 + 커서 표시
    }

    public void AE_PlayAttackSfx()
    {
        // 전이 중/같은 프레임 중복 재생 방지
        if (animator && animator.IsInTransition(0)) return;
        if (Time.frameCount == _lastAttackSfxFrame) return;
        _lastAttackSfxFrame = Time.frameCount;

        var wm = WeaponManager.i;
        var type = wm ? wm.SelectedWeapon : WeaponManager.WeaponType.Crowbar;

        switch (type)
        {
            case WeaponManager.WeaponType.Gun:
                SoundManager.i?.PlaySFX(PlayerSfx.AttackGun, SfxBus.Effect, 1f);
                break;
            case WeaponManager.WeaponType.Bat:
                SoundManager.i?.PlaySFX(PlayerSfx.AttackBat, SfxBus.Effect, 1f);
                break;
            case WeaponManager.WeaponType.Crowbar:
            default:
                SoundManager.i?.PlaySFX(PlayerSfx.AttackCrowbar, SfxBus.Effect, 1f);
                break;
        }
    }

    // 공격이 가능한 상태인가
    bool CanAttackWithWeapon()
    {
        if (!IsWeaponShown()) return false;
        if (WeaponManager.i == null) return false;
        if (WeaponManager.i.SelectedWeapon == WeaponManager.WeaponType.None) return false;
        return true;
    }

    void ToggleGhostMode()
    {
        ghostMode = !ghostMode;
        if (ghostMode) EnableGhostMode();
        else DisableGhostMode();
    }

    void EnableGhostMode()
    {
        ClearAllTriggerStates();

        ghostColliderStates = new List<GhostColliderState>();
        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (!c) continue;
            ghostColliderStates.Add(new GhostColliderState { col = c, enabled = c.enabled });
            c.enabled = false;
        }

        if (rb)
        {
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
        }
    }

    void DisableGhostMode()
    {
        if (ghostColliderStates != null)
        {
            foreach (var st in ghostColliderStates)
            {
                if (st.col) st.col.enabled = st.enabled;
            }
            ghostColliderStates = null;
        }

        if (rb)
        {
            rb.useGravity = true;
            rb.velocity = Vector3.zero;
        }
    }

    void HandleGhostMovement()
    {
        if (!rb) return;

        float h = KeyBindings.GetAxisHorizontal();
        float v = KeyBindings.GetAxisVertical();

        Vector3 camForward = cameraPivot ? cameraPivot.forward : transform.forward;
        Vector3 camRight = cameraPivot ? cameraPivot.right : transform.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * v + camRight * h);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        if (Input.GetKey(KeyCode.Space))
            moveDir += Vector3.up * (ghostVerticalSpeed / Mathf.Max(ghostMoveSpeed, 0.01f));

        if (Input.GetKey(KeyCode.LeftControl))
            moveDir += Vector3.down * (ghostVerticalSpeed / Mathf.Max(ghostMoveSpeed, 0.01f));

        float dt = Time.unscaledDeltaTime;
        transform.position += moveDir * ghostMoveSpeed * dt;

        rb.velocity = Vector3.zero;

        Vector3 flat = new Vector3(moveDir.x, 0f, moveDir.z);
        if (flat.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotSpeed * 200f * dt
            );
        }

        if (animator)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsCrouching", false);
            animator.SetBool("IsCrawling", false);
            animator.SetBool("IsFalling", false);
        }
    }

    void ClearAllTriggerStates()
    {
        // 세탁실
        hasLaundryMission = false;
        inLaundryRange = false;

        // 휴게실
        hasFoyerMission = false;
        inFoyerRange = false;

        // 서재
        hasStudyMission = false;
        inStudyRange = false;
        hasStudyResult = false;
        inStudyResult = false;

        // 드레스룸
        inJewelryRange = false;

        // 게스트룸
        hasGuestMission = false;
        inGuestRange = false;
        inGuestBox1 = false;
        inGuestBox2 = false;
        inGuestBox3 = false;

        // NPC/미션 상호작용
        canTakeMission = false;
        choiceWeapon = false;

        // 암살 관련
        canKill = false;
        killTarget = null;

        // 문 상호작용
        nearDoorLeaves.Clear();

        // 시크릿 구역
        inSecretRange = false;
        animator?.SetBool("InSecret", false);

        // UI 힌트
        if (nearNPC) nearNPC.gameObject.SetActive(false);
    }
}
