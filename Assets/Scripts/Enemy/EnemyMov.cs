using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class EnemyMov : MonoBehaviour
{
    [Header("경로 이동 관련")]
    public AudioClip[] enemySounds;                 // 소리
    public Transform[] waypoints;                   // 순찰 할 경로
    public float walkSpeed = 2f;                    // 걷는 속도 (순찰 중)
    public float runSpeed = 5f;                     // 달리는 속도 (추격 중)
    public float waitTimeAtEnds = 1.5f;             // 경로 끝에 도달했을 때 대기 시간
    public float rotationSpeed = 5f;                // 필요 시 수동 회전 속도

    [Header("시야 관련")]
    public float viewDistance = 10f;                // 감지 가능한 최대 거리
    public float viewAngle = 60f;                   // 기본 시야각 (수평 방향)
    public Transform player;                        // 추적 대상 (플레이어)
    public GameObject questionMark;                 // 물음표 (AI가 플레이어를 인식했을 때)
    public GameObject answerMarkexclamationMark;    // 느낌표 (AI가 플레이어를 추격할 때)
    public GameObject miniQuestionMark;             // 미니맵에서 물음표
    public GameObject miniAnswerMark;               // 미니맵에서 느낌표
    public GameObject viewFov;                      // 죽었을 때 비활성화 할 시야각
    public float nearDetectDistance = 0.7f;         // 가까이 있으면 무조건 보이게

    [Header("수직 시야 관련")]
    public float eyeHeight = 1.5f;                  // 적 눈 높이
    public float targetHeight = 0.9f;               // 플레이어 기준 높이
    public float maxDetectUp = 1.0f;                // 눈보다 위로 허용 높이
    public float maxDetectDown = 2.5f;              // 눈보다 아래로 허용 높이
    public float verticalFovUp = 30f;               // 위쪽 수직 FOV(도)
    public float verticalFovDown = 45f;             // 아래쪽 수직 FOV(도)

    [Header("경고/정찰 타이밍")]
    public float watchPauseDuration = 0.5f;         // Watching 진입 직후 멈칫 시간
    public float investigateDuration = 5f;          // 마지막 좌표로 이동해보는 시간
    public float escalateToChaseDuration = 2f;      // 보이면 2초 후 Chasing

    // 내부 타이머 / 좌표
    private float watchPauseTimer = 0f;
    private float investigateTimer = 0f;
    private float escalateSightTimer = 0f;          // 보이는 시간 누적
    private Vector3 lastKnownPosition;
    private bool hasLastKnownPosition = false;

    [Header("추적 관련")]
    public bool moveTowardPlayerWhileWatching = true;   // Watching일 때 플레이어 위치로 이동
    public float lostPlayerGraceTime = 2f;          // 플레이어를 놓친 뒤 몇 초까지 추적 유지할지
    public float lostAfterPlayer = 2f;              // 플레이어로 시작
    public float lostAfterCorpse = 6f;              // 시체로 시작
    public bool infiniteChaseAfterCorpse = false;    // 시체로 추격 시작 시 무한 유지

    [Header("추격 시 가시 거리 보정")]
    public float chaseViewDistance = 50f;           // Chasing에서만 적용할 넓은 시야거리

    [Header("시체 상태")]
    private bool isFrozen = false;
    public bool IsFrozen => isFrozen;
    public static readonly int Hash_DieTrigger = Animator.StringToHash("DieTrigger");

    [Header("시체 인지 설정")]
    public bool corpseRequiresLineOfSight = true;   // 시체도 가림막 체크할지

    [Header("시야 가림막 레이어")]
    public LayerMask occluderMask;

    [Header("도착/정지 튜닝")]
    [SerializeField] float arriveSlack = 0.15f;         // 남은 거리 여유치
    [SerializeField] float waypointStopDist = 0.1f;     // 에이전트 자체 정지 거리

    // 내부 상태
    private int currentIndex = 0;               // 현재 이동 중인 waypoint 인덱스
    private int direction = 1;                  // 방향: 1 = 순방향, -1 = 역방향
    private bool isWaiting = false;             // 경로 중 정지 중인지
    private float lostPlayerTimer = 0f;         // 플레이어를 놓친 후 경과 시간
    private float originalViewAngle;            // 원래 시야각
    private float destinationUpdateRate = 0.2f; // 추격 중 목표 위치 갱신 간격
    private float destinationUpdateTimer = 0f;  // 현재 추격 위치 갱신 타이머
    private bool isDead = false;                // 사망
    private float _rebindTick = 0f;

    private static readonly List<Transform> Corpses = new List<Transform>();    // 시야각안에 시체가 있는지 검사
    private bool chasingFromCorpse = false;
    private bool sawCorpse = false;

    // 소리 감지 이동 관련
    private bool isSoundTriggered = false;      // 소리 감지가 발생했는지 여부
    private Vector3 firstHeardPosition;         // 처음 들린 소리의 위치
    private bool hasHeardPlayer = false;        // 소리 감지로 플레이어 최초 위치 기록 여부

    // 볼륨, 사운드
    public float footstepVolume = 0.7f;
    public float QuestionVolume = 0.7f;
    public float chaseVolume = 0.7f;
    private int currentSoundIndex = 0;

    // 컴포넌트
    private Animator animator;
    private NavMeshAgent agent;
    [SerializeField] private BoxCollider catchBox;
    [SerializeField] private BoxCollider attackBox;
    [SerializeField] private BoxCollider discover;
    [SerializeField] private BoxCollider discoverClose;
    private AudioSource audioSource;
    private AudioSource chaseAudio;     // 추격 루프 전용(추격 전용)
    private AudioSource footstepAudio;  // 평소 발소리 전용

    // 동시 추격음 제한
    public static int ActiveChaseVoices = 0;
    public const int MaxChaseVoices = 1;
    private bool hasChaseVoice = false;

    // Enemy 상태 정의
    private enum EnemyState { Patrol, Watching, Investigating, Chasing, Dead }       // 순찰 중, 경고(?) - 플레이어 최초 발각 시, 경고 후 안보일 시 정찰, 추격(!) - 플레이어 추적, 죽음
    private EnemyState state = EnemyState.Patrol;

    public static event Action<Transform> OnAnyEnemyKilled;
    private static readonly List<EnemyMov> Instances = new List<EnemyMov>();

    // 공격 애니메이션
    static readonly int HashState_NPC_Punch = Animator.StringToHash("NPC_Punch");
    int _attackLayer = -1;
    [SerializeField] float attackCooldown = 1.0f; // 펀치 쿨다운
    [SerializeField] float minDistanceToPunch = 2.0f; // 너무 멀면 무시
    bool _isAttacking = false;
    float _lastAttackTime = -999f;
    Coroutine _attackLayerRoutine;
    int _hashFull_NPC_Punch;

    public bool IsChasingPublic => state == EnemyState.Chasing;

    void OnEnable()
    {
        OnAnyEnemyKilled += HandleCorpseCreated; // 시체 알림 구독
        if (!Instances.Contains(this)) Instances.Add(this);
    }

    // 에러 가드
    bool AgentReady()
    {
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();

        // 추격 전용 오디오 소스 생성
        chaseAudio = gameObject.AddComponent<AudioSource>();
        chaseAudio.playOnAwake = false;
        chaseAudio.loop = true;
        chaseAudio.volume = chaseVolume;
        chaseAudio.spatialBlend = 1f;                 // 3D
        chaseAudio.dopplerLevel = 0f;                 // 도플러 왜곡 방지
        chaseAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        chaseAudio.minDistance = 2f;
        chaseAudio.maxDistance = 20f;
        chaseAudio.priority = 180;                    // 필요시 조정

        // 발소리 전용
        footstepAudio = gameObject.AddComponent<AudioSource>(); 
        footstepAudio.playOnAwake = false;                      
        footstepAudio.loop = false;                             
        footstepAudio.spatialBlend = 1f;                        // 3D 공간감
        footstepAudio.dopplerLevel = 0f;                        // 왜곡 방지
        footstepAudio.rolloffMode = AudioRolloffMode.Logarithmic; 
        footstepAudio.minDistance = 2f;                         // 들리는 최소 거리
        footstepAudio.maxDistance = 15f;                        // 발소리 가청 거리
        footstepAudio.priority = 200;                           // 우선순위 (낮을수록 높음)


        originalViewAngle = viewAngle;
        lostPlayerTimer = 0f;
        viewFov.SetActive(true);

        if (SoundManager.i && SoundManager.i.EffectGroup != null)
        {
            if (audioSource) audioSource.outputAudioMixerGroup = SoundManager.i.EffectGroup;
            if (chaseAudio) chaseAudio.outputAudioMixerGroup = SoundManager.i.EffectGroup;
            if (footstepAudio) footstepAudio.outputAudioMixerGroup = SoundManager.i.EffectGroup;
        }

        if (catchBox) catchBox.enabled = false;
        if (attackBox) attackBox.enabled = true;

        // 처음 목적지 설정
        agent.speed = walkSpeed;
        agent.acceleration = 40f;
        agent.angularSpeed = 720f;
        agent.updateRotation = false;
        agent.autoBraking = true;
        agent.stoppingDistance = waypointStopDist;
        agent.autoBraking = false;
        agent.stoppingDistance = 0f;

        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentIndex].position);

        NavMeshHit hit;
        if (agent && (!agent.isOnNavMesh) &&
            NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position); // NavMesh 위로 스냅
        }

        if (waypoints != null && waypoints.Length > 0 && AgentReady())
            agent.SetDestination(waypoints[currentIndex].position);

        _attackLayer = animator.GetLayerIndex("Attack");
        _hashFull_NPC_Punch = Animator.StringToHash("Attack.NPC_Punch");

        player = TryFindPlayer();
    }

    Transform TryFindPlayer()
    {
        return GameBootstrap.i?.PlayerTr ?? GameObject.FindWithTag("Player")?.transform;
    }

    void Update()
    {
        if (!player)
        {
            _rebindTick -= Time.deltaTime;
            if (_rebindTick <= 0f)
            {
                player = TryFindPlayer();
                _rebindTick = 0.5f; // 0.5초마다 재시도
            }
            return; // player 없으면 시야/추격 로직 스킵
        }

        if (isDead || state == EnemyState.Dead || isFrozen)
        {
            animator?.SetFloat("Speed", 0f);
            return;
        }

        if (agent.velocity.magnitude < 0.1f)
        {
            animator.SetFloat("Speed", 0f);
            currentSoundIndex = 0;          // 멈추면 다음 발소리는 무조건 index 0부터
        }

        bool playerInSight = IsPlayerInSight();
        bool corpseInSightNow = IsAnyCorpseVisible();

        // 상태에 따른 동작 처리
        switch (state)
        {
            case EnemyState.Patrol:
                viewAngle = originalViewAngle;  // 시야각 복원
                catchBox.enabled = false;       // 잡는 범위 비활성화
                attackBox.enabled = true;       // 공격(암살) 범위 활성화
                Patrol();
                miniAnswerMark.SetActive(false);        // 다시 초기화
                miniQuestionMark.SetActive(false);

                // 추격 루프, 슬롯 정리
                StopChaseLoopCapped();
                audioSource.Stop();

                if (playerInSight || corpseInSightNow)
                {
                    sawCorpse = corpseInSightNow;       // 시체로 본 경우 플래그

                    // 시체를 본 경우 전역 어그로
                    if (corpseInSightNow)
                        TriggerGlobalAggro(player ? player.position : transform.position);

                    BeginWatching(playerInSight && player ? player.position : (Vector3?)null);
                    audioSource.PlayOneShot(enemySounds[2], QuestionVolume);
                    break;
                }
                break;

            case EnemyState.Watching:
            
                viewAngle = 360f;
                catchBox.enabled = false;
                attackBox.enabled = true;

                // 보이면 마지막 좌표 갱신
                if (playerInSight && player)
                {
                    lastKnownPosition = player.position;
                    hasLastKnownPosition = true;
                }
                if (corpseInSightNow) sawCorpse = true;

                // 보이는 시간 누적 / 리셋
                if (playerInSight) escalateSightTimer += Time.deltaTime;
                else escalateSightTimer = 0f;

                // 2초동안 보였으면 바로 Chasing
                if (escalateSightTimer >= escalateToChaseDuration)
                {
                    chasingFromCorpse = sawCorpse;
                    sawCorpse = false;
                    state = EnemyState.Chasing;
                    StartChaseLoopCapped();
                    break;
                }

                // 0.5초 멈칫
                watchPauseTimer -= Time.deltaTime;
                if (watchPauseTimer > 0f)
                {
                    if (AgentReady()) { agent.isStopped = true; animator.SetFloat("Speed", 0f); }
                }
                else
                {
                    // Investigating으로 전환
                    state = EnemyState.Investigating;
                    if (AgentReady())
                    {
                        agent.isStopped = false;
                        agent.speed = walkSpeed;
                         agent.acceleration = 20f;
                        if (hasLastKnownPosition) agent.SetDestination(lastKnownPosition);
                    }
                }
                break;

            case EnemyState.Investigating:
                viewAngle = 360f;
                catchBox.enabled = false;
                attackBox.enabled = true;

                if (playerInSight) escalateSightTimer += Time.deltaTime;
                else escalateSightTimer = 0f;

                if (escalateSightTimer >= escalateToChaseDuration)
                {
                    chasingFromCorpse = sawCorpse;
                    sawCorpse = false;
                    state = EnemyState.Chasing;
                    StartChaseLoopCapped();
                    break;
                }

                if (AgentReady())
                {
                    animator.SetFloat("Speed", agent.velocity.magnitude);

                    bool arrived = !agent.pathPending && agent.remainingDistance <= 0.3f;
                    investigateTimer += Time.deltaTime;
                    bool timeUp = (investigateTimer >= investigateDuration);

                    if (timeUp || arrived)
                    {
                        // Patrol 복귀
                        state = EnemyState.Patrol;

                        ResetSoundDetection();
                        lostPlayerTimer = 0f;
                        sawCorpse = false;
                        chasingFromCorpse = false;
                        escalateSightTimer = 0f;

                        if (waypoints != null && waypoints.Length > 0)
                        {
                            agent.isStopped = false;
                            agent.speed = walkSpeed;
                            agent.SetDestination(waypoints[currentIndex].position);
                        }
                        miniQuestionMark?.SetActive(false);
                        miniAnswerMark?.SetActive(false);
                    }
                }
                break;

            case EnemyState.Chasing:
                viewAngle = 360f;       // 시야 확대
                ChasePlayer();
                miniQuestionMark.SetActive(false);
                miniAnswerMark.SetActive(true);     // 미니맵에 마크 표시

                if (playerInSight)
                {
                    // 추격 루프 유지
                    StartChaseLoopCapped();
                    lostPlayerTimer = 0f;
                }
                else
                {
                    // 시체로 시작된 추격이고, 무한 옵션이 켜져 있으면 절대 포기하지 않음
                    if (chasingFromCorpse && infiniteChaseAfterCorpse)
                    {
                        // 끝없이 Chasing 유지
                        lostPlayerTimer = 0f;
                    }
                    else
                    {
                        lostPlayerTimer += Time.deltaTime;
                        float limit = chasingFromCorpse ? lostAfterCorpse : lostAfterPlayer;
                        if (lostPlayerTimer >= limit) EndChase();
                    }
                }
                break;
        }

        // 현재 이동 중일 때만 회전
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }

        UpdateMark();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("WeaponGun"))
        {
            SoundManager.i?.PlaySFX(PlayerSfx.AttackGun, SfxBus.Effect, 1f);
            Kill();
        }

        if (other.CompareTag("WeaponCrowbar"))
        {
            SoundManager.i?.PlaySFX(PlayerSfx.AttackCrowbar, SfxBus.Effect, 1f);
            Kill();
        }

        if (other.CompareTag("WeaponBat"))
        {
            SoundManager.i?.PlaySFX(PlayerSfx.AttackBat, SfxBus.Effect, 1f);
            Kill();
        }
    }

    // Watching 진입
    void BeginWatching(Vector3? triggerPos = null)
    {
        state = EnemyState.Watching;

        if (triggerPos.HasValue) { lastKnownPosition = triggerPos.Value; hasLastKnownPosition = true; }
        else if (player) { lastKnownPosition = player.position; hasLastKnownPosition = true; }
        else if (isSoundTriggered && hasHeardPlayer)
        { lastKnownPosition = firstHeardPosition; hasLastKnownPosition = true; }

        watchPauseTimer = watchPauseDuration;
        investigateTimer = 0f;

        if (AgentReady())
        {
            agent.isStopped = true;     // 0.5초 멈칫
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        animator.SetFloat("Speed", 0f);
        miniQuestionMark?.SetActive(true);
        miniAnswerMark?.SetActive(false);

        PlayOneShotSafe(enemySounds, 2, QuestionVolume);
    }

    // 추적 종료
    void EndChase()
    {
        StopChaseLoopCapped();
        audioSource.Stop();
        ResetSoundDetection();

        chasingFromCorpse = false;
        lostPlayerTimer = 0f;
        escalateSightTimer = 0f;

        state = EnemyState.Patrol;

        if (AgentReady() && waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentIndex].position);
        if (AgentReady())
            agent.isStopped = false;
    }

    void LateUpdate()
    {
        if (isDead || state == EnemyState.Dead) return;

        // 항상 월드 Z+ 방향을 향하도록 미니맵 마크 회전 고정
        if (miniQuestionMark != null)
        {
            miniQuestionMark.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        if (miniAnswerMark != null)
        {
            miniAnswerMark.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    void Patrol()
    {
        if (waypoints == null || waypoints.Length < 2 || isWaiting) return;
        if (!AgentReady()) return;

        agent.speed = walkSpeed;

        bool arrived = !agent.pathPending && agent.remainingDistance <= (agent.stoppingDistance + arriveSlack);

        if (arrived)
        {
            // 끝/시작 지점에 도달했다면 방향 전환 전 '완전 정지'
            if ((currentIndex == 0 && direction == -1) || (currentIndex == waypoints.Length - 1 && direction == 1))
            {
                direction *= -1;

                // 확실히 멈추기
                if (AgentReady())
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }

                StartCoroutine(WaitBeforeMoving());
                return;
            }

            // 일반 중간 지점 도착 -> 다음 인덱스로 진행
            currentIndex += direction;

            if (AgentReady())
            {
                agent.isStopped = false;
                agent.SetDestination(waypoints[currentIndex].position);
            }
        }

        // 속도 보간을 통해 애니메이션 부드럽게 처리
        float smoothSpeed = Mathf.Lerp(animator.GetFloat("Speed"), agent.velocity.magnitude, Time.deltaTime * 10f);
        animator.SetFloat("Speed", smoothSpeed);
    }

    // 추격 행동
    void ChasePlayer()
    {
        if (!AgentReady()) return;
        agent.speed = runSpeed;

        destinationUpdateTimer += Time.deltaTime;
        if (destinationUpdateTimer >= destinationUpdateRate)
        {
            if (AgentReady())
                agent.SetDestination(player.position);
            destinationUpdateTimer = 0f;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= minDistanceToPunch)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.Angle(transform.forward, toPlayer.normalized);

                if (angle < 60f)
                {
                    TryPlayPunch(player);
                }
            }
            else
            {
                TryPlayPunch(player);
            }
        }

        catchBox.enabled = true;        // 플레이어를 쫒아갈때 잡는 범위 활성화
        attackBox.enabled = false;

        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    // 잠시 정지
    IEnumerator WaitBeforeMoving()
    {
        animator.SetFloat("Speed", 0f);
        isWaiting = true;

        if (AgentReady())
        {
            agent.isStopped = true;       // 대기 동안 완전 정지
            agent.velocity = Vector3.zero;
            agent.ResetPath();            // 남아있는 경로/회피 정보 제거
        }

        yield return new WaitForSeconds(waitTimeAtEnds);
        isWaiting = false;

        if (!AgentReady()) yield break;

        agent.isStopped = false;
        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentIndex].position);
    }

    // 플레이어가 시야 내에 있는지 검사
    bool IsPlayerInSight()
    {
        if (isDead || state == EnemyState.Dead) return false;
        if (player == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = player.position + Vector3.up * targetHeight;

        Vector3 to = targetPos - eyePos;
        float dist = to.magnitude;

        float maxDist = (state == EnemyState.Chasing) ? chaseViewDistance : viewDistance;
        if (dist > maxDist) return false;

        // 너무 가까우면 시야각 상관없이 본 걸로 처리
        if (dist < nearDetectDistance)
        {
            if (!Physics.Raycast(eyePos, to.normalized, dist, occluderMask, QueryTriggerInteraction.Collide))
                return true;
        }

        // 수직 높이 차
        float dy = targetPos.y - eyePos.y;
        if (dy > maxDetectUp || dy < -maxDetectDown) return false;

        // 수평 FOV
        Vector3 toFlat = new Vector3(to.x, 0f, to.z);
        float sqrFlat = toFlat.sqrMagnitude;

        float hAngle = 0f;
        if (sqrFlat >= 0.0001f)
        {
            hAngle = Vector3.Angle(transform.forward, toFlat.normalized);
            if (hAngle > viewAngle * 0.5f) return false;
        }

        // 수직 FOV
        float vAngle = Mathf.Atan2(dy, Mathf.Sqrt(sqrFlat)) * Mathf.Rad2Deg;
        if (vAngle > verticalFovUp || vAngle < -verticalFovDown) return false;

        // 가림막
        if (Physics.Raycast(eyePos, to.normalized, dist, occluderMask, QueryTriggerInteraction.Collide))
            return false;

        return true;
    }

    // 시체 체크
    private bool IsTargetVisible(Transform target, float maxDistance, float fov, bool checkLOS)
    {
        if (!target) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 toTarget = (target.position + Vector3.up * 0.9f) - eyePos;
        float dist = toTarget.magnitude;

        if (dist > maxDistance) return false;

        float angle = Vector3.Angle(transform.forward, toTarget.normalized);
        if (angle > fov * 0.5f) return false;

        if (checkLOS)
        {
            if (Physics.Raycast(eyePos, toTarget.normalized, dist, occluderMask, QueryTriggerInteraction.Collide))
                return false; // 사이에 벽이 있으면 안보임
        }
        return true;
    }

    private bool IsAnyCorpseVisible()
    {
        if (isDead || state == EnemyState.Dead) return false;

        for (int i = Corpses.Count - 1; i >= 0; --i)
        {
            var c = Corpses[i];
            if (c == null) { Corpses.RemoveAt(i); continue; }
            if (c == transform) continue;

            if (IsTargetVisible(c, viewDistance, viewAngle, corpseRequiresLineOfSight))
                return true;
        }
        return false;
    }

    // 다른 Enemy가 죽었을 때(시체 생김) 호출되는 콜백
    private void HandleCorpseCreated(Transform corpse)
    {
        if (isDead || state == EnemyState.Dead || corpse == null || corpse == transform) return;

        // "평소 시야"로만 시체 발견 → Watching 진입
        if (IsTargetVisible(corpse, viewDistance, viewAngle, corpseRequiresLineOfSight))
        {
            sawCorpse = true;                 // Watching에서 1.5초 후 Chasing을 트리거하게 하는 플래그

            // 플레이어를 처음 본 것과 동일한 반응
            PlayOneShotSafe(enemySounds, 2, QuestionVolume);
            BeginWatching(corpse.position);

            miniQuestionMark?.SetActive(true);
            miniAnswerMark?.SetActive(false);
        }
    }

    // AI 머리위에 뜨는 마크 (?, !)
    void UpdateMark()
    {
        // 모두 기본 OFF
        if (questionMark) questionMark.SetActive(false);
        if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(false);
        if (miniQuestionMark) miniQuestionMark.SetActive(false);
        if (miniAnswerMark) miniAnswerMark.SetActive(false);

        if (isDead || state == EnemyState.Dead) return;

        // 상태별 표시 규칙
        bool showQuestion = (state == EnemyState.Watching || state == EnemyState.Investigating);
        bool showExclamation = (state == EnemyState.Chasing);

        if (questionMark) questionMark.SetActive(showQuestion);
        if (miniQuestionMark) miniQuestionMark.SetActive(showQuestion);

        if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(showExclamation);
        if (miniAnswerMark) miniAnswerMark.SetActive(showExclamation);

        // 카메라 바라보기(필요할 때만)
        if (Camera.main != null)
        {
            if (questionMark && questionMark.activeSelf)
            {
                questionMark.transform.LookAt(Camera.main.transform);
                questionMark.transform.Rotate(0f, 180f, 0f);
            }
            if (answerMarkexclamationMark && answerMarkexclamationMark.activeSelf)
            {
                answerMarkexclamationMark.transform.LookAt(Camera.main.transform);
                answerMarkexclamationMark.transform.Rotate(0f, 180f, 0f);
            }
        }
    }

    // 소리가 들리면 플레이어 방향으로 이동
    public void PlayerDetected(Vector3 playerPos)
    {
        if (isDead || state == EnemyState.Dead) return;
        if (state == EnemyState.Chasing) return;

        if (!hasHeardPlayer)
        {
            firstHeardPosition = playerPos; // 인자로 받은 위치 사용!
            hasHeardPlayer = true;
            isSoundTriggered = true;

            BeginWatching(playerPos);
            PlayOneShotSafe(enemySounds, 2, QuestionVolume);
        }
    }

    void ResetSoundDetection()
    {
        isSoundTriggered = false;
        hasHeardPlayer = false;
    }

    // 애니메이션 이벤트에서 호출할 함수
    public void PlayFootstep()
    {
        if (isDead || state == EnemyState.Dead) return;

        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        // 정지/대기 중이면 무음 (Watching이라도 이동하면 허용)
        if (agent.isStopped || agent.velocity.magnitude < 0.1f) return;

        // 발소리 재생은 footstepAudio 로
        if (footstepAudio == null || enemySounds == null || enemySounds.Length < 2) return;

        // 0,1 번만 번갈아 재생
        footstepAudio.pitch = 1f + UnityEngine.Random.Range(-0.05f, 0.05f);
        footstepVolume = Mathf.Clamp01(footstepVolume);
        footstepAudio.PlayOneShot(enemySounds[currentSoundIndex], footstepVolume);
        currentSoundIndex = (currentSoundIndex + 1) % 2;
    }
    void PlayOneShotSafe(AudioClip[] clips, int index, float vol)
    {
        if (audioSource == null || clips == null) return;
        if (index < 0 || index >= clips.Length) return;
        if (clips[index] == null) return;
        audioSource.PlayOneShot(clips[index], vol);
    }

    // 루프 시작 / 정지
    void StartChaseLoop()
    {
        if (chaseAudio == null) return;
        if (enemySounds == null || enemySounds.Length < 4 || enemySounds[3] == null) return;
        if (!chaseAudio.isPlaying)
        {
            chaseAudio.clip = enemySounds[3];
            chaseAudio.volume = chaseVolume;
            chaseAudio.Play();
        }
    }

    void StopChaseLoop()
    {
        if (chaseAudio == null) return;
        if (chaseAudio.isPlaying) chaseAudio.Stop();
        chaseAudio.clip = null;
    }

    // Chasing 동시 재생 제한
    void StartChaseLoopCapped()
    {
        if (hasChaseVoice) { StartChaseLoop(); return; }
        if (ActiveChaseVoices >= MaxChaseVoices) return; // 슬롯 없음

        StartChaseLoop();
        if (chaseAudio.isPlaying)
        {
            hasChaseVoice = true;
            ActiveChaseVoices++;
        }
    }

    void StopChaseLoopCapped()
    {
        if (hasChaseVoice)
        {
            hasChaseVoice = false;
            ActiveChaseVoices = Mathf.Max(0, ActiveChaseVoices - 1);
        }
        StopChaseLoop();
    }

    // 컴포넌트 비활성/파괴 시 카운터/사운드 정리 누락 방지
    void OnDisable()
    {
        OnAnyEnemyKilled -= HandleCorpseCreated; // 해제
        Instances.Remove(this);

        StopAllCoroutines();
        chasingFromCorpse = false;
        StopChaseLoopCapped();
        audioSource?.Stop();
    }
    void OnDestroy()
    {
        Instances.Remove(this);
        chasingFromCorpse = false;
        StopChaseLoopCapped();
        audioSource?.Stop();
    }

    // 모든 적에게 '시체로 유발된 추격'
    public static void TriggerGlobalAggro(Vector3 targetPos)
    {
        foreach (var e in Instances)
        {
            if (!e || e.isDead) continue;
            if (e.state == EnemyState.Chasing) continue;     // 이미 추격 중이면 손대지 않음
            e.ForceChaseFromCorpse(targetPos);
        }
    }

    public void ForceChaseFromCorpse(Vector3 targetPos)
    {
        if (isDead) return;

        bool wasChasing = (state == EnemyState.Chasing);

        if (!wasChasing)
        {
            chasingFromCorpse = true;
            lostPlayerTimer = 0f;
            state = EnemyState.Chasing;
            StartChaseLoopCapped();
        }

        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.SetDestination(targetPos);

        miniQuestionMark?.SetActive(false);
        miniAnswerMark?.SetActive(true);

        if (!wasChasing) PlayOneShotSafe(enemySounds, 2, QuestionVolume);
    }

    // 사망
    public void Kill()
    {
        if (isDead) return;
        isDead = true;
        state = EnemyState.Dead;
        if(viewFov) viewFov.SetActive(false);

        StopAllCoroutines();

        Corpses.Add(transform);

        OnAnyEnemyKilled?.Invoke(transform);

        // 소리 정리
        StopChaseLoopCapped();
        audioSource?.Stop();

        // 에이전트 정지
        if (agent != null)
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.velocity = Vector3.zero;
            agent.enabled = false;
        }

        // 콜라이더/표식 끄기
        if (catchBox) catchBox.enabled = false;
        if (attackBox) attackBox.enabled = false;

        // 길막 유발 콜라이더 전부 비활성 (시체 감지는 occluderMask 방식이므로 문제 없음)
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col == catchBox || col == attackBox) continue;
            col.enabled = false; // 또는 col.isTrigger = true;
        }

        if (questionMark) questionMark.SetActive(false);
        if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(false);
        if (miniQuestionMark) miniQuestionMark.SetActive(false);
        if (miniAnswerMark) miniAnswerMark.SetActive(false);

        // 애니메이터 정지(필요 시 사망 트리거로 교체 가능)
        if (animator)
        {
            animator.SetFloat("Speed", 0f);
            animator.ResetTrigger("Grab");
            animator.ResetTrigger("Drop");
            animator.SetBool("IsGrabbed", false);

            animator.SetTrigger(Hash_DieTrigger);       // 딱 한 번 Die로 진입
        }

        var drag = GetComponent<DraggableCorpse>();

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col == catchBox || col == attackBox) continue;
            if (drag && col == drag.handleTrigger) continue;
            col.enabled = false;
        }

        GetComponent<DraggableCorpse>()?.OnDeath();
    }

    // 애니메이션 끝에 모든 행동 비활성화
    public void OnDeathAnimationEnd()
    {
        enabled = false;
    }

    // 공격 받자마자 행동 정지
    public void FreezeForAttack(bool on)
    {
        isFrozen = on;

        // 네비 정지
        if (AgentReady())
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        // 애니/사운드/표식 정리
        animator.SetFloat("Speed", 0f);
        StopChaseLoopCapped();
        audioSource?.Stop();
        if (miniQuestionMark) miniQuestionMark.SetActive(false);
        if (miniAnswerMark) miniAnswerMark.SetActive(false);
        if (catchBox) catchBox.enabled = false;
        if (attackBox) attackBox.enabled = false;
    }

    // 공격(암살) 당함
    public void PrepareForAssassination(bool on)
    {
        if (on)
        {
            // 감지/추격 멈춤
            state = EnemyState.Watching;  // 또는 별도 Frozen 상태 만들어도 됨
            if (AgentReady())
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }
            animator.SetFloat("Speed", 0f);
            miniQuestionMark?.SetActive(false);
            miniAnswerMark?.SetActive(false);
            // 공격 박스/캐치 박스도 꺼두기
            if (catchBox) catchBox.enabled = false;
            if (attackBox) attackBox.enabled = false;
        }
        else
        {
            // 원상복구(시체라면 Kill()에서 이미 정리됨)
            if (!isDead && state != EnemyState.Dead)
            {
                state = EnemyState.Patrol;
                if (AgentReady() && waypoints != null && waypoints.Length > 0)
                {
                    agent.isStopped = false;
                    agent.SetDestination(waypoints[currentIndex].position);
                }
            }
        }
    }

    public void OnPlayerEnteredCatchBox(Transform playerTr)
    {
        TryPlayPunch(playerTr);
    }

    void TryPlayPunch(Transform playerTr)
    {
        if (_isAttacking) return;
        if (Time.time - _lastAttackTime < attackCooldown) return;
        if (!playerTr) return;

        // 거리/각도 같은 간단한 가드
        float d = Vector3.Distance(transform.position, playerTr.position);
        if (d > minDistanceToPunch) return;

        PlayPunchOnAttackLayer();
    }

    void SetAttackActive(bool on)
    {
        // 공격 중에 이동 로직에서 콜라이더/상태를 건드리지 않도록 보호가 필요하면 여기서 처리.
    }

    void PlayPunchOnAttackLayer()
    {
        if (_attackLayer < 0) return;

        _isAttacking = true;
        _lastAttackTime = Time.time;

        // Attack 레이어 가중치 보장
        animator.SetLayerWeight(_attackLayer, 1f);

        // 즉시 재생 (해당 레이어의 NPC_Punch 상태로)
        animator.Play(_hashFull_NPC_Punch, _attackLayer, 0f);

        SetAttackActive(true);

        if (_attackLayerRoutine != null) StopCoroutine(_attackLayerRoutine);
        _attackLayerRoutine = StartCoroutine(Co_LowerAttackLayerWhenDone());
    }

    IEnumerator Co_LowerAttackLayerWhenDone()
    {
        yield return null;

        int safety = 0;
        while (safety++ < 60)
        {
            var info = animator.GetCurrentAnimatorStateInfo(_attackLayer);
            if (info.fullPathHash == _hashFull_NPC_Punch) break;

            if (animator.IsInTransition(_attackLayer)) { yield return null; continue; }
            yield return null;
        }

        while (true)
        {
            var info = animator.GetCurrentAnimatorStateInfo(_attackLayer);

            if (info.fullPathHash == _hashFull_NPC_Punch && info.normalizedTime < 1f)
            {
                yield return null;
                continue;
            }

            if (animator.IsInTransition(_attackLayer))
            {
                yield return null;
                continue;
            }
            break;
        }

        animator.SetLayerWeight(_attackLayer, 0f);
        SetAttackActive(false);
        _isAttacking = false;
        _attackLayerRoutine = null;
    }

    public void EnableCatchBox() 
    { 
        if (discover) discover.enabled = true; 
        if (discoverClose) discoverClose.enabled = true; 
    }
    public void DisableCatchBox() 
    { 
        if (discover) discover.enabled = false; 
        if (discoverClose) discoverClose.enabled = false; 
    }

    // 시야 관련 기즈모
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        // 시야 거리 원
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        // 3D 시야각 방향 시각화
        Vector3 forward = transform.forward;
        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * forward;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, left * viewDistance);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, right * viewDistance);
    }
}
