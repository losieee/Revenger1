using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GunEnemyMov : MonoBehaviour
{
    [Header("경로 이동 관련")]
    public AudioClip[] enemySounds;                 // 소리
    public Transform[] waypoints;                   // 순찰 할 경로
    public float walkSpeed = 2f;                    // 걷는 속도 (순찰 중)
    public float runSpeed = 5f;                     // 달리는 속도 (추격 중)
    public float waitTimeAtEnds = 1.5f;             // 경로 끝에 도달했을 때 대기 시간
    public float rotationSpeed = 5f;                // 필요 시 수동 회전 속도

    [Header("시야 관련")]
    public float viewDistance = 20f;                // 감지 가능한 최대 거리
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
    public float lostAfterPlayer = 2f;              // 플레이어로 시작
    public float lostAfterCorpse = 6f;              // 시체로 시작
    public bool infiniteChaseAfterCorpse = false;   // 시체로 추격 시작 시 무한 유지

    [Header("추격 시 가시 거리 보정")]
    public float chaseViewDistance = 50f;           // Chasing에서만 적용할 넓은 시야거리
    public float watchingViewDistance = 25f;

    [Header("시체 상태")]
    private bool isFrozen = false;
    public bool IsFrozen => isFrozen;
    public static readonly int Hash_DieTrigger = Animator.StringToHash("DieTrigger");
    static readonly int Hash_ShootTrigger = Animator.StringToHash("Shoot");
    static readonly int Hash_Speed = Animator.StringToHash("Speed");
    static readonly int Hash_IsShooting = Animator.StringToHash("IsShooting");

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
    bool _isInShootRange = false;

    private static readonly List<Transform> Corpses = new List<Transform>();    // 시야각 안에 시체가 있는지 검사
    private bool chasingFromCorpse = false;
    private bool sawCorpse = false;

    // 소리 감지 이동 관련
    private bool isSoundTriggered = false;      // 소리 감지가 발생했는지 여부
    private Vector3 firstHeardPosition;         // 처음 들린 소리의 위치
    private bool hasHeardPlayer = false;        // 소리 감지로 플레이어 최초 위치 기록 여부
    bool _lockMoveForAction = false;            // 이동 잠금 (사격 중)

    [Header("사운드")]
    public float footstepVolume = 0.7f;
    public float QuestionVolume = 0.7f;
    public float chaseVolume = 0.7f;
    private int currentSoundIndex = 0;

    // 컴포넌트
    private Animator animator;
    private NavMeshAgent agent;
    private AudioSource audioSource;
    private AudioSource chaseAudio;     // 추격 루프 전용(추격 전용)
    private AudioSource footstepAudio;  // 평소 발소리 전용

    // 동시 추격음 제한
    public static int ActiveChaseVoices = 0;
    public const int MaxChaseVoices = 1;
    private bool hasChaseVoice = false;

    // Enemy 상태 정의
    private enum EnemyState { Patrol, Watching, Investigating, Chasing, Dead }       // 순찰, 경고, 정찰, 추격, 죽음
    private EnemyState state = EnemyState.Patrol;

    public static event Action<Transform> OnAnyEnemyKilled;
    private static readonly List<GunEnemyMov> Instances = new List<GunEnemyMov>();

    [Header("원거리 공격 설정")]
    public Transform firePoint;              // 총알 발사 위치
    public float fireCooldown = 1.0f;        // 발사 쿨타임
    public float fireRange = 15f;            // 사거리
    public float keepDistance = 10f;         // 유지하고 싶은 거리
    float _lastFireTimeRanged = -999f;
    public int rayDamage = 10;          // 인스펙터에서 조절할 데미지
    public LayerMask hitMask = ~0;      // 맞출 레이어(기본은 전체) - 헤더에 추가해도 좋음

    public bool IsChasingPublic => state == EnemyState.Chasing;

    void OnEnable()
    {
        OnAnyEnemyKilled += HandleCorpseCreated;
        if (!Instances.Contains(this)) Instances.Add(this);
    }

    // 에이전트 안전 체크
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
        chaseAudio.maxDistance = 5f;
        chaseAudio.priority = 180;                    // 필요시 조정

        // 발소리 전용
        footstepAudio = gameObject.AddComponent<AudioSource>();
        footstepAudio.playOnAwake = false;
        footstepAudio.loop = false;
        footstepAudio.spatialBlend = 1f;                        // 3D 공간감
        footstepAudio.dopplerLevel = 0f;                        // 왜곡 방지
        footstepAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        footstepAudio.minDistance = 0.5f;                       // 들리는 최소 거리
        footstepAudio.maxDistance = 1f;                         // 발소리 가청 거리
        footstepAudio.priority = 200;                           // 우선순위 (낮을수록 높음)

        originalViewAngle = viewAngle;
        lostPlayerTimer = 0f;
        if (viewFov) viewFov.SetActive(true);

        if (SoundManager.i && SoundManager.i.EffectGroup != null)
        {
            if (audioSource) audioSource.outputAudioMixerGroup = SoundManager.i.EffectGroup;
            if (chaseAudio) chaseAudio.outputAudioMixerGroup = SoundManager.i.EffectGroup;
            if (footstepAudio) footstepAudio.outputAudioMixerGroup = SoundManager.i.EffectGroup;
        }

        // 처음 목적지 설정
        if (agent)
        {
            agent.speed = walkSpeed;
            agent.acceleration = 40f;
            agent.angularSpeed = 720f;
            agent.updateRotation = false;
            agent.autoBraking = true;
            agent.stoppingDistance = waypointStopDist;
            agent.autoBraking = false;
            agent.stoppingDistance = 0f;
        }

        if (waypoints != null && waypoints.Length > 0 && AgentReady())
            agent.SetDestination(waypoints[currentIndex].position);

        NavMeshHit hit;
        if (agent && (!agent.isOnNavMesh) &&
            NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position); // NavMesh 위로 스냅
        }

        if (waypoints != null && waypoints.Length > 0 && AgentReady())
            agent.SetDestination(waypoints[currentIndex].position);

        player = TryFindPlayer();
    }

    Transform TryFindPlayer()
    {
        return GameBootstrap.i?.PlayerTr ?? GameObject.FindWithTag("Player")?.transform;
    }

    void Update()
    {
        if (PlayerMov.IsDeadGlobal)
        {
            // 상태/타이머 리셋
            state = EnemyState.Patrol;
            lostPlayerTimer = 0f;
            chasingFromCorpse = false;
            sawCorpse = false;
            ResetSoundDetection();
            _isInShootRange = false;
            _lockMoveForAction = false;
            if (animator)
            {
                animator.SetFloat(Hash_Speed, 0f);
                animator.SetBool(Hash_IsShooting, false);
                animator.ResetTrigger(Hash_ShootTrigger);
            }

            StopChaseLoopCapped();

            if (AgentReady())
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }

            if (questionMark) questionMark.SetActive(false);
            if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(false);
            if (miniQuestionMark) miniQuestionMark.SetActive(false);
            if (miniAnswerMark) miniAnswerMark.SetActive(false);

            return;
        }

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

        if (agent && agent.velocity.magnitude < 0.1f)
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
                Patrol();
                if (miniAnswerMark) miniAnswerMark.SetActive(false);        // 다시 초기화
                if (miniQuestionMark) miniQuestionMark.SetActive(false);

                // 추격 루프, 슬롯 정리
                StopChaseLoopCapped();
                if (audioSource) audioSource.Stop();

                if (playerInSight || corpseInSightNow)
                {
                    sawCorpse = corpseInSightNow;       // 시체로 본 경우 플래그

                    // 시체를 본 경우 전역 어그로
                    if (corpseInSightNow)
                        TriggerGlobalAggro(player ? player.position : transform.position);

                    BeginWatching(playerInSight && player ? player.position : (Vector3?)null);
                    if (audioSource && enemySounds != null && enemySounds.Length > 2)
                        audioSource.PlayOneShot(enemySounds[2], QuestionVolume);
                    break;
                }
                break;

            case EnemyState.Watching:

                viewAngle = 360f;

                if (playerInSight)
                {
                    // 플레이어를 일정 시간 동안 계속 보고 있을 때만 Chasing으로
                    escalateSightTimer += Time.deltaTime;

                    // 보는 동안 플레이어 방향으로만 살짝 회전
                    Vector3 dir = player.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
                    }

                    if (escalateSightTimer >= escalateToChaseDuration)
                    {
                        // 시체 때문에 본 거였다면 플래그 유지
                        chasingFromCorpse = sawCorpse;
                        sawCorpse = false;

                        state = EnemyState.Chasing;
                        StartChaseLoopCapped();
                    }
                }
                else
                {
                    // 시야에서 놓치면 누적 시간 리셋
                    escalateSightTimer = 0f;

                    watchPauseTimer -= Time.deltaTime;
                    if (watchPauseTimer <= 0f)
                        state = EnemyState.Investigating;
                }

                animator.SetFloat(Hash_Speed, 0f);
                break;

            case EnemyState.Investigating:
                viewAngle = 360f;

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

                investigateTimer += Time.deltaTime;
                if (investigateTimer >= investigateDuration)
                {
                    ReturnToPatrolFromInvestigate();
                    break;
                }

                animator.SetFloat(Hash_Speed, 0f);
                break;

            case EnemyState.Chasing:
                viewAngle = 360f;       // 시야 확대
                ChasePlayer();
                if (miniQuestionMark) miniQuestionMark.SetActive(false);
                if (miniAnswerMark) miniAnswerMark.SetActive(true);     // 미니맵에 마크 표시

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
        /*if (agent && agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }*/

        UpdateMark();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Weapon")) return;

        PlayHitSfxByCurrentWeapon();
        Kill();
    }

    void PlayHitSfxByCurrentWeapon()
    {
        if (SoundManager.i == null) return;

        var wm = WeaponManager.i;
        if (wm == null) return;

        switch (wm.SelectedWeapon)
        {
            case WeaponManager.WeaponType.Gun:
                SoundManager.i.PlaySFX(PlayerSfx.AttackGun, SfxBus.Effect, 1f);
                break;

            case WeaponManager.WeaponType.Crowbar:
                SoundManager.i.PlaySFX(PlayerSfx.AttackCrowbar, SfxBus.Effect, 1f);
                break;

            case WeaponManager.WeaponType.Bat:
                SoundManager.i.PlaySFX(PlayerSfx.AttackBat, SfxBus.Effect, 1f);
                break;
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
        escalateSightTimer = 0f;

        if (AgentReady())
        {
            agent.isStopped = true;     // 0.5초 멈칫
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        animator.SetFloat("Speed", 0f);
        miniQuestionMark?.SetActive(true);
        miniAnswerMark?.SetActive(false);

        if (audioSource && enemySounds != null && enemySounds.Length > 2)
            audioSource.PlayOneShot(enemySounds[2], QuestionVolume);
    }

    // 추적 종료
    void EndChase()
    {
        StopChaseLoopCapped();
        if (audioSource) audioSource.Stop();
        ResetSoundDetection();

        chasingFromCorpse = false;
        lostPlayerTimer = 0f;
        escalateSightTimer = 0f;

        state = EnemyState.Patrol;

        if (AgentReady())
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
            agent.speed = 0f;
        }
    }

    void LateUpdate()
    {
        if (isDead || state == EnemyState.Dead) return;

        // 항상 월드 Z+ 방향을 향하도록 미니맵 마크 회전 고정
        if (miniQuestionMark != null)
        {
            miniQuestionMark.transform.rotation = Quaternion.Euler(90f, -90f, 0f);
        }

        if (miniAnswerMark != null)
        {
            miniAnswerMark.transform.rotation = Quaternion.Euler(90f, -90f, 0f);
        }
    }

    void Patrol()
    {
        animator.SetFloat(Hash_Speed, 0f);

        if (AgentReady())
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }
    }

    // 추격 행동
    void ChasePlayer()
    {
        if (!player) return;

        float dist = Vector3.Distance(transform.position, player.position);

        // 사격 범위 안인지
        bool inShootRange = dist <= fireRange;

        if (_isInShootRange != inShootRange)
        {
            _isInShootRange = inShootRange;
            animator.SetBool(Hash_IsShooting, _isInShootRange);

            if (!_isInShootRange)
            {
                // 사거리 밖으로 나가면 다시 이동 잠금 해제 & 트리거 리셋
                SetMoveLocked(false);
                animator.ResetTrigger(Hash_ShootTrigger);
            }
        }

        // 사격 모션 중일 땐 제자리 유지
        if (_lockMoveForAction)
        {
            animator.SetFloat(Hash_Speed, 0f);
            return;
        }

        // 플레이어 쪽으로만 회전
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }

        // 사거리 안이면 총 발사 시도
        TryShootAtPlayer(dist);

        // 이동 애니메이션은 항상 0 (제자리)
        animator.SetFloat(Hash_Speed, 0f);
    }

    // 잠시 정지
    IEnumerator WaitBeforeMoving()
    {
        animator.SetFloat("Speed", 0f);
        isWaiting = true;

        if (AgentReady())
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
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

        float maxDist;
        switch (state)
        {
            case EnemyState.Chasing:
                maxDist = chaseViewDistance;
                break;

            case EnemyState.Watching:
            case EnemyState.Investigating:
                maxDist = watchingViewDistance;
                break;

            default:
                maxDist = viewDistance;
                break;
        }

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
                return false;
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

    // 다른 Enemy가 죽었을 때 호출
    private void HandleCorpseCreated(Transform corpse)
    {
        if (isDead || state == EnemyState.Dead || corpse == null || corpse == transform) return;

        if (IsTargetVisible(corpse, viewDistance, viewAngle, corpseRequiresLineOfSight))
        {
            sawCorpse = true;

            if (audioSource && enemySounds != null && enemySounds.Length > 2)
                audioSource.PlayOneShot(enemySounds[2], QuestionVolume);
            BeginWatching(corpse.position);

            miniQuestionMark?.SetActive(true);
            miniAnswerMark?.SetActive(false);
        }
    }

    // AI 머리위에 뜨는 마크 (?, !)
    void UpdateMark()
    {
        if (questionMark) questionMark.SetActive(false);
        if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(false);
        if (miniQuestionMark) miniQuestionMark.SetActive(false);
        if (miniAnswerMark) miniAnswerMark.SetActive(false);

        if (isDead || state == EnemyState.Dead) return;

        bool showQuestion = (state == EnemyState.Watching || state == EnemyState.Investigating);
        bool showExclamation = (state == EnemyState.Chasing);

        if (questionMark) questionMark.SetActive(showQuestion);
        if (miniQuestionMark) miniQuestionMark.SetActive(showQuestion);

        if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(showExclamation);
        if (miniAnswerMark) miniAnswerMark.SetActive(showExclamation);

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
            firstHeardPosition = playerPos;
            hasHeardPlayer = true;
            isSoundTriggered = true;

            BeginWatching(playerPos);
            if (audioSource && enemySounds != null && enemySounds.Length > 2)
                audioSource.PlayOneShot(enemySounds[2], QuestionVolume);
        }
    }

    void ResetSoundDetection()
    {
        isSoundTriggered = false;
        hasHeardPlayer = false;
    }

    public void PlayFootstep()
    {
        if (isDead || state == EnemyState.Dead) return;

        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        if (agent.isStopped || agent.velocity.magnitude < 0.1f) return;

        if (footstepAudio == null || enemySounds == null || enemySounds.Length < 2) return;

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

    void StartChaseLoopCapped()
    {
        if (hasChaseVoice) { StartChaseLoop(); return; }
        if (ActiveChaseVoices >= MaxChaseVoices) return;

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

    void OnDisable()
    {
        OnAnyEnemyKilled -= HandleCorpseCreated;
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

    public static void TriggerGlobalAggro(Vector3 targetPos)
    {
        foreach (var e in Instances)
        {
            if (!e || e.isDead) continue;
            if (e.state == EnemyState.Chasing) continue;
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

        if (AgentReady())
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        miniQuestionMark?.SetActive(false);
        miniAnswerMark?.SetActive(true);

        if (audioSource && enemySounds != null && enemySounds.Length > 2)
            audioSource.PlayOneShot(enemySounds[2], QuestionVolume);
    }

    // 사망
    public void Kill()
    {
        if (isDead) return;
        isDead = true;
        state = EnemyState.Dead;
        if (viewFov) viewFov.SetActive(false);

        StopAllCoroutines();

        Corpses.Add(transform);

        OnAnyEnemyKilled?.Invoke(transform);

        StopChaseLoopCapped();
        audioSource?.Stop();

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

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        if (questionMark) questionMark.SetActive(false);
        if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(false);
        if (miniQuestionMark) miniQuestionMark.SetActive(false);
        if (miniAnswerMark) miniAnswerMark.SetActive(false);

        if (animator)
        {
            animator.SetFloat("Speed", 0f);
            animator.ResetTrigger("Grab");
            animator.ResetTrigger("Drop");
            animator.SetBool("IsGrabbed", false);

            animator.SetTrigger(Hash_DieTrigger);
        }

        var drag = GetComponent<DraggableCorpse>();

        foreach (var col in GetComponentsInChildren<Collider>())
        {
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

    // 공격 받으면 행동 정지
    public void FreezeForAttack(bool on)
    {
        isFrozen = on;

        if (AgentReady())
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        animator.SetFloat("Speed", 0f);

        if (on)
        {
            animator.SetBool(Hash_IsShooting, false);
            animator.ResetTrigger(Hash_ShootTrigger);
        }

        StopChaseLoopCapped();
        audioSource?.Stop();
        if (miniQuestionMark) miniQuestionMark.SetActive(false);
        if (miniAnswerMark) miniAnswerMark.SetActive(false);
    }

    // 공격(암살) 당함
    public void PrepareForAssassination(bool on)
    {
        if (on)
        {
            state = EnemyState.Watching;
            if (AgentReady())
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }
            animator.SetFloat("Speed", 0f);
            miniQuestionMark?.SetActive(false);
            miniAnswerMark?.SetActive(false);
        }
        else
        {
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
    }

    void TryShootAtPlayer(float dist)
    {
        if (PlayerMov.IsDeadGlobal) return;

        if (dist > fireRange) return;

        if (Time.time - _lastFireTimeRanged < fireCooldown) return;
        if (!firePoint || !player) return;

        _lastFireTimeRanged = Time.time;

        SetMoveLocked(true);
        animator.SetFloat(Hash_Speed, 0f);
        animator.SetTrigger(Hash_ShootTrigger);
    }

    // 총알 발사
    public void ShootProjectile()
    {
        if (!firePoint || !player) return;

        if (audioSource && enemySounds != null && enemySounds.Length > 4 && enemySounds[4])
        {
            audioSource.PlayOneShot(enemySounds[4], 1f);
        }

        if (PlayerMov.IsDeadGlobal) return;

        Vector3 origin = firePoint.position;
        Vector3 targetPos = player.position + Vector3.up * targetHeight;
        Vector3 dir = (targetPos - origin).normalized;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, fireRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            Debug.DrawLine(origin, hit.point, Color.red, 0.3f);
            Debug.Log(hit.collider);

            if (hit.collider.CompareTag("BodyFront"))
            {
                var player = hit.collider.GetComponentInParent<PlayerMov>();
                if (player != null)
                {
                    player.DieByBulletBack();
                }
            }
            else if (hit.collider.CompareTag("BodyBack"))
            {
                var player = hit.collider.GetComponentInParent<PlayerMov>();
                if (player != null)
                {
                    player.DieByBulletFront();
                }
            }
        }
        else
        {
            Debug.DrawRay(origin, dir * fireRange, Color.yellow, 0.3f);
        }
    }

    void ReturnToPatrolFromInvestigate()
    {
        state = EnemyState.Patrol;

        ResetSoundDetection();
        lostPlayerTimer = 0f;
        sawCorpse = false;
        chasingFromCorpse = false;
        escalateSightTimer = 0f;
        investigateTimer = 0f;

        if (AgentReady())
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
            agent.speed = 0f;
        }

        miniQuestionMark?.SetActive(false);
        miniAnswerMark?.SetActive(false);
    }

    void SetMoveLocked(bool on)
    {
        _lockMoveForAction = on;

        if (AgentReady())
        {
            agent.isStopped = on;
            agent.velocity = Vector3.zero;
            if (on) agent.ResetPath();
        }

        if (on)
        {
            animator.SetFloat("Speed", 0f);
        }
    }

    // Shoot 애니 끝 이벤트
    public void OnShootAnimationEnd()
    {
        SetMoveLocked(false);
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
