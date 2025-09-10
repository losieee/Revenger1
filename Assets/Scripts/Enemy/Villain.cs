using UnityEngine;

public class Villain : MonoBehaviour
{
    public static Villain i;

    public enum VillainState { Idle, Watching, Chasing }
    public VillainState state = VillainState.Idle;

    [Header("���� ����(����)")]
    public bool reactToSound = true;          // �Ҹ� ��������
    public float soundWatchDuration = 1.5f;   // �Ҹ� ��� ? ǥ�� ���� �ð�
    float _soundTimer = 0f;

    [Header("��ũ UI (EnemyMov�� ���� �̸�)")]
    public GameObject questionMark;                 // Watching: ?
    public GameObject answerMarkexclamationMark;    // Chasing: !
    public GameObject miniQuestionMark;             // �̴ϸ� ?
    public GameObject miniAnswerMark;               // �̴ϸ� !

    [Header("���� �߰� �ɼ�")]
    public bool triggerGlobalAggroOnChase = true;   // Chasing ���� �� ��� Enemy �߰�
    public Transform chaseTarget;                   // ���� �߰� ��ǥ(������ �� ��ġ/�÷��̾� ��ġ)

    // �÷��̾� �ڵ� Ž�� + FOV ���� ��Ʈ
    [Header("�÷��̾� Ž��/����")]
    public bool autoDetectPlayer = true;            // �ڵ� ���� �ѱ�
    public Transform player;                        // ���� �Ҵ絵 ����
    public float rebindPlayerInterval = 0.5f;       // ����ε� �ֱ�
    private float _baseViewAngle;

    [Tooltip("Watching���� Chasing���� �Ѿ����� �� �ð�(��)")]
    public float watchConfirmTime = 2f;

    // ��� ������ �� Watching�� ������ ���� �ð�(0�̸� ��� Idle)
    [Tooltip("Watching �� �þ߿��� ����� �� Idle�� �������� �� ����(��)")]
    public float watchLoseGrace = 0.1f;

    [Header("�þ� ����ȭ(������ ����)")]
    [Tooltip("Idle �� Watching ��, �������� �� �ּ� �ð�")]
    public float watchEnterGrace = 0.1f;    // ��¦�� ���� Watching
    float _seenAccum = 0f;                  // ���� ���� Ÿ�̸�
    float _lostAccum = 0f;

    [Header("���º� �þ߰�")]
    public float idleViewAngle = 320f;
    public float watchingViewAngle = 360f;
    public float chasingViewAngle = 360f;

    [Header("�þ�(FOV)")]
    public float viewDistance = 18f;                // �⺻ �þ� �Ÿ�
    public float chaseViewDistance = 60f;           // Chasing ��(���ϸ� ���)
    public float viewAngle = 70f;                   // ���� FOV
    public float eyeHeight = 1.7f;                  // ���� ������
    public float targetHeight = 1.0f;               // Ÿ��(�÷��̾�) ���� ������
    public float verticalFovUp = 35f;               // ���� FOV
    public float verticalFovDown = 50f;             // �Ʒ��� FOV
    public float maxDetectUp = 1.5f;                // ������ �� ���
    public float maxDetectDown = 3.0f;              // ������ �Ʒ� ���
    public LayerMask occluderMask;                  // ������ ���̾�

    float _rebindTick = 0f;

    void Awake() => i = this;

    void Start()
    {
        _baseViewAngle = idleViewAngle;
        viewAngle = _baseViewAngle;
        TryBindPlayer(true);
        UpdateMark();
    }

    // �÷��̾� �Ҹ��� ����� �� �ܺο��� ȣ��
    public void PlayerDetectedBySound(Vector3 playerPos)
    {
        if (!reactToSound) return;
        if (state == VillainState.Chasing) return; // �̹� �߰� ���̸� ����

        EnterWatching();               // ? / �̴ϸ� ? �ѱ�
        _soundTimer = soundWatchDuration;
    }

    void Update()
    {
        if (autoDetectPlayer && !player) TryBindPlayer();

        bool inSight = false;
        if (autoDetectPlayer && player)
            inSight = IsTargetInSight(player, state == VillainState.Chasing);

        // ---- ����/���� ���� ----
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
                // �ʿ� �� �߰� ���� ���� �߰�
                break;
        }

        // �Ҹ��� ������ Watching �ڵ� ����
        if (_soundTimer > 0f && state == VillainState.Watching)
        {
            _soundTimer -= Time.deltaTime;
            if (_soundTimer <= 0f) EnterIdle();
        }

        UpdateMark();
    }


    // === �ܺο��� ���� ȣ���ص� OK ===
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
        // Idle���� ���� �������� �ʰ�(�Ǽ� ����)
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

    // --- FOV ���� ---
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

    // �÷��̾� �ڵ� ���ε�
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

    // --- ��ũ ǥ�� ---
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

    // ���� �����
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
