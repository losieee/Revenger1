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

    [Header("추적 관련")]
    public float lostPlayerGraceTime = 2f;          // 플레이어를 놓친 뒤 몇 초까지 추적 유지할지
    public bool infiniteChaseAfterCorpse = true;    // 시체로 추격 시작 시 무한 유지
    private bool chasingFromCorpse = false;         // 시체로 인해 추격 시작했는가

    [Header("추격 시 가시 거리 보정")]
    public float chaseViewDistance = 50f;           // Chasing에서만 적용할 넓은 시야거리

    [Header("시체 인지 설정")]
    public bool corpseRequiresLineOfSight = true;   // 시체도 가림막 체크할지
    private bool sawCorpse = false;                 // 시체 보고 Watching에 들어왔는지 플래그
    public static event Action<Transform> OnAnyEnemyKilled;     // 모든 적이 공유하는 시체 브로드캐스트 이벤트
    private static readonly List<EnemyMov> Instances = new List<EnemyMov>();    // 맵에 있는 Enemy들


    // 내부 상태
    private int currentIndex = 0;               // 현재 이동 중인 waypoint 인덱스
    private int direction = 1;                  // 방향: 1 = 순방향, -1 = 역방향
    private bool isWaiting = false;             // 경로 중 정지 중인지
    private float playerStayTime = 0f;          // 시야 안에 플레이어가 있었던 누적 시간
    private float lostPlayerTimer = 0f;         // 플레이어를 놓친 후 경과 시간
    private float originalViewAngle;            // 원래 시야각
    private float destinationUpdateRate = 0.2f; // 추격 중 목표 위치 갱신 간격
    private float destinationUpdateTimer = 0f;  // 현재 추격 위치 갱신 타이머
    private bool isDead = false;                // 사망
    private static readonly List<Transform> Corpses = new List<Transform>();    // 시야각안에 시체가 있는지 검사

    // 소리 감지 이동 관련
    private bool isSoundTriggered = false;      // 소리 감지가 발생했는지 여부
    private float soundDetectTimer = 0.5f;      // 소리 감지 후 경과 시간
    private float maxChaseBySoundTime = 3f;     // 소리 감지로 이동하는 최대 시간
    private float soundChaseTimer = 0f;         // 소리 감지로 추적 중인 시간 누적
    private Vector3 firstHeardPosition;         // 처음 들린 소리의 위치
    private bool hasHeardPlayer = false;        // 소리 감지로 플레이어 최초 위치 기록 여부
    private bool isSoundWaiting = false;        // 감지 직후 잠깐 멈춤 플래그

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
    private AudioSource audioSource;
    private AudioSource chaseAudio;     // 추격 루프 전용(추격 전용)
    private AudioSource footstepAudio;  // 평소 발소리 전용

    // 동시 추격음 제한
    public static int ActiveChaseVoices = 0;
    public const int MaxChaseVoices = 1;
    private bool hasChaseVoice = false;

    // Enemy 상태 정의
    private enum EnemyState { Patrol, Watching, Chasing }       // 순찰 중, 경고(?) - 플레이어 최초 발각 시, 추격(!) - 플레이어 추적
    private EnemyState state = EnemyState.Patrol;

    void OnEnable()
    {
        OnAnyEnemyKilled += HandleCorpseCreated; // 시체 알림 구독
        if (!Instances.Contains(this)) Instances.Add(this);
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


        originalViewAngle = viewAngle;      //시작할때는 기본 사야각 60으로
        lostPlayerTimer = 0f;

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
        agent.SetDestination(waypoints[currentIndex].position);

        agent.acceleration = 40f;
        agent.angularSpeed = 720f;
        agent.updateRotation = false;
        agent.autoBraking = false;
        agent.stoppingDistance = 0f;
    }

    void Update()
    {
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
                    playerStayTime = 0f;    // 본 순간 타이머 리셋
                    sawCorpse = corpseInSightNow;       // 시체로 본 경우 플래그

                    // 시체를 본 경우 전역 어그로
                    if (corpseInSightNow)
                        TriggerGlobalAggro(player ? player.position : transform.position);

                    state = EnemyState.Watching;
                    audioSource.PlayOneShot(enemySounds[2], QuestionVolume);
                    break;
                }
                break;

            case EnemyState.Watching:
                viewAngle = 360f;        // 시야각 확장
                catchBox.enabled = false;       // 잡는 범위 비활성화
                attackBox.enabled = true;
                animator.SetFloat("Speed", 0f); // 애니메이션 정지
                miniQuestionMark.SetActive(true);   // 미니맵에 마크 표시
                miniAnswerMark.SetActive(false);

                if (corpseInSightNow) sawCorpse = true;

                bool escalateTrigger = playerInSight || sawCorpse;

                if (escalateTrigger)
                {
                    agent.isStopped = true;
                    playerStayTime += Time.deltaTime;
                    if (playerStayTime >= 1.5f)       // 1.5초 이상 봤으면 추적 시작
                    {
                        agent.isStopped = false;
                        chasingFromCorpse = sawCorpse;
                        state = EnemyState.Chasing;
                        sawCorpse = false;
                        StartChaseLoopCapped();     // 추격 진입 시도
                    }
                }
                else if (isSoundTriggered)
                {
                    if (isSoundWaiting)
                    {
                        soundDetectTimer += Time.deltaTime;
                        if (soundDetectTimer >= 0.5f)
                        {
                            isSoundWaiting = false;
                            questionMark.SetActive(true);
                            agent.isStopped = false;
                            agent.SetDestination(firstHeardPosition);
                        }
                        else
                        {
                            agent.isStopped = true;
                            animator.SetFloat("Speed", 0f);
                        }
                    }
                    else
                    {
                        agent.isStopped = false;
                        animator.SetFloat("Speed", agent.velocity.magnitude);
                        soundChaseTimer += Time.deltaTime;

                        if (soundChaseTimer >= maxChaseBySoundTime)
                        {
                            ResetSoundDetection();

                            state = EnemyState.Patrol;
                            agent.SetDestination(waypoints[currentIndex].position);
                            agent.isStopped = false;

                            StopChaseLoopCapped();
                            audioSource.Stop();
                        }
                    }
                }
                else
                {
                    playerStayTime += Time.deltaTime;
                    if (playerStayTime >= 1f)
                    {
                        audioSource.Stop();
                        StopChaseLoopCapped();

                        state = EnemyState.Patrol;
                        agent.SetDestination(waypoints[currentIndex].position);
                        agent.isStopped = false;

                        // 사운드 관련 초기화 추가
                        ResetSoundDetection();
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
                    if (infiniteChaseAfterCorpse && chasingFromCorpse)
                    {
                        // 끝없이 Chasing 유지
                        lostPlayerTimer = 0f;
                    }
                    else
                    {
                        lostPlayerTimer += Time.deltaTime;
                        if (lostPlayerTimer >= lostPlayerGraceTime)
                        {
                            StopChaseLoopCapped();
                            audioSource.Stop();
                            ResetSoundDetection();

                            // 일반적으로 종료할 때만 플래그 해제
                            chasingFromCorpse = false;

                            state = EnemyState.Patrol;
                            agent.SetDestination(waypoints[currentIndex].position);
                        }
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

    void LateUpdate()
    {
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

        agent.speed = walkSpeed;

        // 목적지 도달 확인
        if (!agent.pathPending && agent.remainingDistance < 0.2f)
        {
            // 시작점 or 끝점에 도달했을 경우
            if ((currentIndex == 0 && direction == -1) || (currentIndex == waypoints.Length - 1 && direction == 1))
            {
                direction *= -1; // 반드시 방향 반전 필요
                StartCoroutine(WaitBeforeMoving());
                return;
            }

            // 다음 인덱스로 이동
            currentIndex += direction;

            // 다음 목적지 설정
            agent.SetDestination(waypoints[currentIndex].position);
        }

        // 속도 보간을 통해 애니메이션 부드럽게 처리
        float smoothSpeed = Mathf.Lerp(animator.GetFloat("Speed"), agent.velocity.magnitude, Time.deltaTime * 10f);
        animator.SetFloat("Speed", smoothSpeed);
    }

    // 추격 행동
    void ChasePlayer()
    {
        agent.speed = runSpeed;

        destinationUpdateTimer += Time.deltaTime;
        if (destinationUpdateTimer >= destinationUpdateRate)
        {
            agent.SetDestination(player.position);
            destinationUpdateTimer = 0f;
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
        yield return new WaitForSeconds(waitTimeAtEnds);
        isWaiting = false;
        agent.isStopped = false;
        agent.SetDestination(waypoints[currentIndex].position);
    }

    // 플레이어가 시야 내에 있는지 검사
    bool IsPlayerInSight()
    {
        if (player == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 dirToPlayer = player.position - eyePos;
        float distanceToPlayer = dirToPlayer.magnitude;

        float maxDist = (state == EnemyState.Chasing) ? chaseViewDistance : viewDistance;
        if (distanceToPlayer > maxDist)
            return false;

        // 시야각 확인 (3D 방향 포함)
        float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer.normalized);
        if (angleToPlayer > viewAngle / 2f)
            return false;

        // 장애물 Raycast 확인
        if (Physics.Raycast(eyePos, dirToPlayer.normalized, out RaycastHit hit, maxDist))
            return hit.transform == player;

        return false;
    }

    // 시체 체크
    private bool IsTargetVisible(Transform target, float maxDistance, float fov, bool checkLOS)
    {
        if (!target) return false;
        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 dir = target.position - eyePos;
        float dist = dir.magnitude;

        if (dist > maxDistance) return false;
        float angle = Vector3.Angle(transform.forward, dir.normalized);
        if (angle > fov * 0.5f) return false;

        if (checkLOS)
        {
            if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist))
            {
                if (!hit.transform.IsChildOf(target)) return false; // 가림막 있으면 false
            }
        }
        return true;
    }

    private bool IsAnyCorpseVisible()
    {
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
        if (isDead || corpse == null || corpse == transform) return;

        // "평소 시야"로만 시체 발견 → Watching 진입
        if (IsTargetVisible(corpse, viewDistance, viewAngle, corpseRequiresLineOfSight))
        {
            sawCorpse = true;                 // Watching에서 1.5초 후 Chasing을 트리거하게 하는 플래그
            playerStayTime = 0f;

            // 플레이어를 처음 본 것과 동일한 반응
            PlayOneShotSafe(enemySounds, 2, QuestionVolume);
            state = EnemyState.Watching;
            agent.isStopped = true;

            miniQuestionMark?.SetActive(true);
            miniAnswerMark?.SetActive(false);
        }
    }

    // AI 머리위에 뜨는 마크 (?, !)
    void UpdateMark()
    {
        // 모든 마크 비활성화
        questionMark.SetActive(false);
        answerMarkexclamationMark.SetActive(false);

        // 현재 상태에 따라 해당 마크 활성화
        GameObject activeMark = null;

        switch (state)
        {
            case EnemyState.Watching:
                activeMark = questionMark;
                break;
            case EnemyState.Chasing:
                activeMark = answerMarkexclamationMark;
                break;
        }

        if (activeMark != null)
        {
            activeMark.SetActive(true);

            // 마크가 항상 카메라를 바라보게
            if (activeMark != null)
            {
                activeMark.SetActive(true);

                // 마크가 항상 카메라를 바라보게
                if (Camera.main != null)
                {
                    activeMark.transform.LookAt(Camera.main.transform);
                    activeMark.transform.Rotate(0f, 180f, 0f);
                }
            }
        }
    }

    // 소리가 들리면 플레이어 방향으로 이동
    public void PlayerDetected(Vector3 _)
    {
        // 소리 감지 재시작을 허용(상태가 Chasing일 때만 무시)
        if (state == EnemyState.Chasing)
            return;

        if (!hasHeardPlayer) // 처음 소리 감지일 때만 위치 기록
        {
            firstHeardPosition = player.position;
            hasHeardPlayer = true;
            isSoundWaiting = true;

            // 소리 감지 초기화(중복 감지도 허용)
            isSoundTriggered = true;
            soundDetectTimer = 0f;
            soundChaseTimer = 0f;

            // 소리 들리면 바로 Watching 상태로 진입하고, 위치 설정
            state = EnemyState.Watching;
            audioSource.PlayOneShot(enemySounds[2], QuestionVolume);

            agent.isStopped = true; // 정지 해제
            animator.SetFloat("Speed", 0f);
        }
    }

    void ResetSoundDetection()
    {
        isSoundTriggered = false;
        hasHeardPlayer = false;
        isSoundWaiting = false;
        soundDetectTimer = 0f;
        soundChaseTimer = 0f;
    }

    // 애니메이션 이벤트에서 호출할 함수
    public void PlayFootstep()
    {
        if (agent == null) return;

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
    private static void TriggerGlobalAggro(Vector3 targetPos)
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            var e = Instances[i];
            if (!e || e.isDead) continue;
            e.ForceChaseFromCorpse(targetPos);
        }
    }

    public void ForceChaseFromCorpse(Vector3 targetPos)
    {
        if (isDead) return;

        bool wasChasing = (state == EnemyState.Chasing);

        chasingFromCorpse = true;
        lostPlayerTimer = 0f;
        state = EnemyState.Chasing;

        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.SetDestination(targetPos);

        StartChaseLoopCapped();

        miniQuestionMark?.SetActive(false);
        miniAnswerMark?.SetActive(true);

        if (!wasChasing) PlayOneShotSafe(enemySounds, 2, QuestionVolume);
    }

    // 사망
    public void Kill()
    {
        if (isDead) return;
        isDead = true;

        Corpses.Add(transform);

        OnAnyEnemyKilled?.Invoke(transform);

        // 소리 정리
        StopChaseLoopCapped();
        audioSource?.Stop();

        // 에이전트 정지
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        // 콜라이더/표식 끄기
        if (catchBox) catchBox.enabled = false;
        if (attackBox) attackBox.enabled = false;
        if (questionMark) questionMark.SetActive(false);
        if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(false);
        if (miniQuestionMark) miniQuestionMark.SetActive(false);
        if (miniAnswerMark) miniAnswerMark.SetActive(false);

        // 애니메이터 정지(필요 시 사망 트리거로 교체 가능)
        if (animator)
        {
            animator.SetFloat("Speed", 0f);
            // animator.SetTrigger("Die"); // 'Die' 애니메이션 있으면 사용
        }

        // 이 스크립트 비활성화해서 상태머신/Update 완전 정지
        enabled = false;
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
