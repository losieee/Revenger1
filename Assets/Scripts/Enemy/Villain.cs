using UnityEngine;

public class Villain : MonoBehaviour
{
    public static Villain i;

    public enum VillainState { Idle, Watching, Chasing }
    public VillainState state = VillainState.Idle;

    [Header("사운드 인지(선택)")]
    public bool reactToSound = true;          // 소리 반응할지
    public float soundWatchDuration = 1.5f;   // 소리 듣고 ? 표시 유지 시간
    float _soundTimer = 0f;

    [Header("마크 UI (EnemyMov와 동일 이름)")]
    public GameObject questionMark;                 // Watching: ?
    public GameObject answerMarkexclamationMark;    // Chasing: !
    public GameObject miniQuestionMark;             // 미니맵 ?
    public GameObject miniAnswerMark;               // 미니맵 !

    [Header("전역 추격 옵션")]
    public bool triggerGlobalAggroOnChase = true;   // Chasing 진입 시 모든 Enemy 추격
    public Transform chaseTarget;                   // 전역 추격 목표(없으면 내 위치/플레이어 위치)

    // 플레이어 자동 탐색 + FOV 감지 파트
    [Header("플레이어 탐색/감지")]
    public bool autoDetectPlayer = true;            // 자동 감지 켜기
    public Transform player;                        // 수동 할당도 가능
    public float rebindPlayerInterval = 0.5f;       // 재바인딩 주기
    private float _baseViewAngle;

    [Tooltip("Watching에서 Chasing으로 넘어가기까지 본 시간(초)")]
    public float watchConfirmTime = 2f;

    // 잠깐 놓쳤을 때 Watching을 끝내는 유예 시간(0이면 즉시 Idle)
    [Tooltip("Watching 중 시야에서 벗어났을 때 Idle로 떨어지기 전 유예(초)")]
    public float watchLoseGrace = 0.1f;

    [Header("시야 안정화(깜빡임 방지)")]
    [Tooltip("Idle → Watching 전, 연속으로 본 최소 시간")]
    public float watchEnterGrace = 0.1f;    // 살짝만 봐도 Watching
    float _seenAccum = 0f;                  // 내부 누적 타이머
    float _lostAccum = 0f;

    [Header("상태별 시야각")]
    public float idleViewAngle = 320f;
    public float watchingViewAngle = 360f;
    public float chasingViewAngle = 360f;

    [Header("시야(FOV)")]
    public float viewDistance = 18f;                // 기본 시야 거리
    public float chaseViewDistance = 60f;           // Chasing 시(원하면 사용)
    public float viewAngle = 70f;                   // 수평 FOV
    public float eyeHeight = 1.7f;                  // 빌런 눈높이
    public float targetHeight = 1.0f;               // 타겟(플레이어) 높이 기준점
    public float verticalFovUp = 35f;               // 위쪽 FOV
    public float verticalFovDown = 50f;             // 아래쪽 FOV
    public float maxDetectUp = 1.5f;                // 눈보다 위 허용
    public float maxDetectDown = 3.0f;              // 눈보다 아래 허용
    public LayerMask occluderMask;                  // 가림막 레이어

    float _rebindTick = 0f;

    void Awake() => i = this;

    void Start()
    {
        _baseViewAngle = idleViewAngle;
        viewAngle = _baseViewAngle;
        TryBindPlayer(true);
        UpdateMark();
    }

    // 플레이어 소리를 들었을 때 외부에서 호출
    public void PlayerDetectedBySound(Vector3 playerPos)
    {
        if (!reactToSound) return;
        if (state == VillainState.Chasing) return; // 이미 추격 중이면 무시

        EnterWatching();               // ? / 미니맵 ? 켜기
        _soundTimer = soundWatchDuration;
    }

    void Update()
    {
        if (autoDetectPlayer && !player) TryBindPlayer();

        bool inSight = false;
        if (autoDetectPlayer && player)
            inSight = IsTargetInSight(player, state == VillainState.Chasing);

        // ---- 보임/유실 누적 ----
        if (inSight) { _seenAccum += Time.deltaTime; _lostAccum = 0f; }
        else { _lostAccum += Time.deltaTime; _seenAccum = 0f; }

        switch (state)
        {
            case VillainState.Idle:
                if (_seenAccum >= watchEnterGrace)
                    EnterWatching();
                break;

            case VillainState.Watching:
                if (_seenAccum >= watchConfirmTime)
                    EnterChasing();
                else if (_lostAccum >= watchLoseGrace)
                    EnterIdle();
                break;

            case VillainState.Chasing:
                // 필요 시 추격 포기 로직 추가
                break;
        }

        // 소리로 진입한 Watching 자동 해제
        if (_soundTimer > 0f && state == VillainState.Watching)
        {
            _soundTimer -= Time.deltaTime;
            if (_soundTimer <= 0f) EnterIdle();
        }

        UpdateMark();
    }


    // === 외부에서 수동 호출해도 OK ===
    public void EnterChasing()
    {
        state = VillainState.Chasing;
        viewAngle = Mathf.Max(watchingViewAngle, chasingViewAngle);
        ResetTimers();
        UpdateMark();

        if (triggerGlobalAggroOnChase)
        {
            Vector3 pos = chaseTarget ? chaseTarget.position :
                            (player ? player.position : transform.position);
            EnemyMov.TriggerGlobalAggro(pos);
        }
    }

    public void EnterWatching()
    {
        state = VillainState.Watching;
        // Idle보다 절대 좁아지지 않게(실수 방지)
        viewAngle = Mathf.Max(idleViewAngle, watchingViewAngle);
        ResetTimers();
        UpdateMark();
    }

    public void EnterIdle()
    {
        state = VillainState.Idle;
        viewAngle = idleViewAngle;
        ResetTimers();
        UpdateMark();
    }

    void ResetTimers()
    {
        _seenAccum = 0f; _lostAccum = 0f;
    }

    // --- FOV 감지 ---
    bool IsTargetInSight(Transform t, bool chasing)
    {
        if (!t) return false;

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = t.position + Vector3.up * targetHeight;
        Vector3 to = targetPos - eyePos;

        float dist = to.magnitude;
        float maxDist = chasing ? chaseViewDistance : viewDistance;
        if (dist > maxDist) return false;

        float dy = targetPos.y - eyePos.y;
        if (dy > maxDetectUp || dy < -maxDetectDown) return false;

        Vector3 toFlat = new Vector3(to.x, 0f, to.z);
        if (toFlat.sqrMagnitude < 0.0001f) return false;

        float hAngle = Vector3.Angle(transform.forward, toFlat.normalized);
        if (hAngle > viewAngle * 0.5f) return false;

        float vAngle = Mathf.Atan2(dy, toFlat.magnitude) * Mathf.Rad2Deg;
        if (vAngle > verticalFovUp || vAngle < -verticalFovDown) return false;

        if (Physics.Raycast(eyePos, to.normalized, dist, occluderMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    // 플레이어 자동 바인딩
    Transform TryFindPlayer()
    {
        return GameBootstrap.i?.PlayerTr ?? GameObject.FindWithTag("Player")?.transform;
    }
    void TryBindPlayer(bool immediately = false)
    {
        _rebindTick -= Time.deltaTime;
        if (!immediately && _rebindTick > 0f) return;

        var p = TryFindPlayer();
        if (p) player = p;
        _rebindTick = rebindPlayerInterval;
    }

    // --- 마크 표시 ---
    void UpdateMark()
    {
        if (questionMark) questionMark.SetActive(false);
        if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(false);
        if (miniQuestionMark) miniQuestionMark.SetActive(false);
        if (miniAnswerMark) miniAnswerMark.SetActive(false);

        switch (state)
        {
            case VillainState.Watching:
                if (questionMark) questionMark.SetActive(true);
                if (miniQuestionMark) miniQuestionMark.SetActive(true);
                break;
            case VillainState.Chasing:
                if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(true);
                if (miniAnswerMark) miniAnswerMark.SetActive(true);
                break;
        }
    }

    void LateUpdate()
    {
        if (miniQuestionMark) miniQuestionMark.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        if (miniAnswerMark) miniAnswerMark.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (Camera.main)
        {
            if (questionMark)
            {
                questionMark.transform.LookAt(Camera.main.transform);
                questionMark.transform.Rotate(0f, 180f, 0f);
            }
            if (answerMarkexclamationMark)
            {
                answerMarkexclamationMark.transform.LookAt(Camera.main.transform);
                answerMarkexclamationMark.transform.Rotate(0f, 180f, 0f);
            }
        }
    }

    // 편한 디버그
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 f = transform.forward;
        Vector3 l = Quaternion.Euler(0, -viewAngle / 2f, 0) * f;
        Vector3 r = Quaternion.Euler(0, viewAngle / 2f, 0) * f;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, l * viewDistance);
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, r * viewDistance);
    }
}
