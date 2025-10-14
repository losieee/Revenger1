using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class CameraMov : MonoBehaviour
{
    public static CameraMov i;

    [Header("Follow Target")]
    public Transform target;         // 플레이어 루트(또는 카메라 피벗)
    [SerializeField] Transform explicitPivot;  // 있으면 이걸 우선 사용(선택)

    [Header("카메라 옵션")]
    public float mouseSensitivity = 3f;
    public float distance = 5f;        // 최대 거리
    public float minDistance = 0.5f;   // 너무 가까워짐 방지
    public float heightOffset = 1.5f;
    public float pitchMin = -40f;
    public float pitchMax = 80f;

    // 민감도 범위 (옵션에서 슬라이더로 조절)
    public float minSensitivity = 1f;
    public float maxSensitivity = 6f;

    [Header("충돌 감지")]
    public float collisionRadius = 0.25f;      // SphereCast 반지름
    public float collisionOffset = 0.15f;      // 벽에서 살짝 띄우기
    public LayerMask collisionLayers = ~0;     // 카메라/플레이어 제외 권장

    [Header("카메라 거리 스무싱")]
    public float pullInSmoothTime = 0.05f;     // 벽에 막혀 당길 때 빠르게
    public float relaxOutSmoothTime = 0.15f;   // 다시 멀어질 때 천천히

    [Header("엎드렸을 때 뷰")]
    public float crawlDown = 0.35f;
    public float crawlLerp = 0.12f;
    float _crawlYTarget = 0f;
    float _crawlY = 0f;

    [Header("활성화 블렌드")]
    public bool smoothOnEnable = true;
    public float enableBlendDuration = 0.25f;

    float yaw = 0f;
    float pitch = 15f;

    // enable 블렌드
    bool _enableBlendActive = false;
    float _enableBlendT = 0f;
    float _enableBlendDur = 0.25f;
    Vector3 _enableBlendPos0;
    Quaternion _enableBlendRot0;

    // 외부에서 호출하는 블렌드 인
    bool _blendInActive;
    float _blendInT, _blendInDur;
    Vector3 _blendStartPos;
    Quaternion _blendStartRot;

    float currentDistance;      // 현재 실제 거리
    float distanceVelocity;     // SmoothDamp 내부속도

    Coroutine recenterCoroutine;
    Coroutine _recenterCo;

    public void SetTarget(Transform t) => BindPlayer(t);
    public Transform Target => GetPivot();

    void Awake()
    {
        if (i && i != this) { Destroy(gameObject); return; }
        i = this;
        DontDestroyOnLoad(gameObject);

        if (smoothOnEnable)
        {
            _enableBlendActive = true;
            _enableBlendT = 0f;
            _enableBlendDur = Mathf.Max(0.01f, enableBlendDuration);
            _enableBlendPos0 = transform.position;
            _enableBlendRot0 = transform.rotation;
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // PlayerSpawner가 BindPlayer를 호출하는 게 원칙.
        // 혹시 못 불린 케이스 대비 1회 자동 바인딩(최후 수단).
        if (!target)
        {
            var pm = FindObjectOfType<PlayerMov>(true);
            if (pm) BindPlayer(pm.transform);
        }

        // 부드럽게 활성화 블렌드
        if (smoothOnEnable)
        {
            BeginBlendIn(enableBlendDuration);
        }
    }

    void Start()
    {
        currentDistance = Mathf.Clamp(distance, minDistance, Mathf.Max(minDistance, distance));
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f || !GetPivot()) return;

        // 1) 입력 → 회전
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // 2) 타겟 기준점 (엎드리기 오프셋 포함)
        if (crawlLerp <= 0f) crawlLerp = 0.01f;
        float kCrawl = Mathf.Clamp01(Time.deltaTime / crawlLerp);
        _crawlY = Mathf.Lerp(_crawlY, _crawlYTarget, kCrawl);

        Transform pivot = GetPivot();
        Vector3 basePos = pivot.position; // pivot을 기준으로 삼는다(있으면)
        Vector3 targetPos = basePos + Vector3.up * (heightOffset - _crawlY);

        // 3) 충돌 감지 (벽에 막히면 거리 줄임)
        float targetDistance = distance;
        Vector3 camDir = (rotation * Vector3.back).normalized; // 뒤(-Z)

        Ray ray = new Ray(targetPos, camDir);
        if (Physics.SphereCast(ray, collisionRadius, out RaycastHit hit, distance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Clamp(hit.distance - collisionOffset, minDistance, distance);
        }

        // 4) 당길 땐 빠르게, 풀릴 땐 느리게
        float st = (targetDistance < currentDistance) ? pullInSmoothTime : relaxOutSmoothTime;
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, st);

        // 5) 실제 위치/회전 적용 (+ 라인캐스트 재확인)
        Vector3 desiredPos = targetPos + camDir * currentDistance;
        if (Physics.Linecast(targetPos, desiredPos, out RaycastHit hit2, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            float safeDist = Mathf.Clamp(hit2.distance - collisionOffset, minDistance, currentDistance);
            currentDistance = safeDist;
            desiredPos = targetPos + camDir * currentDistance;
        }
        Quaternion desiredRot = Quaternion.LookRotation((targetPos - desiredPos).normalized, Vector3.up);

        // enable 블렌드 처리
        if (_enableBlendActive)
        {
            _enableBlendT += Time.unscaledDeltaTime / Mathf.Max(0.01f, _enableBlendDur);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_enableBlendT));
            transform.position = Vector3.Lerp(_enableBlendPos0, desiredPos, k);
            transform.rotation = Quaternion.Slerp(_enableBlendRot0, desiredRot, k);
            if (k >= 1f) _enableBlendActive = false;
        }
        else
        {
            transform.position = desiredPos;
            transform.rotation = desiredRot;
        }

        // 외부 블렌드 인
        if (_blendInActive)
        {
            _blendInT += Time.deltaTime / _blendInDur;
            float kb = Mathf.SmoothStep(0f, 1f, _blendInT);
            transform.position = Vector3.Lerp(_blendStartPos, transform.position, kb);
            transform.rotation = Quaternion.Slerp(_blendStartRot, transform.rotation, kb);
            if (_blendInT >= 1f) _blendInActive = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>플레이어(또는 카메라 피벗)를 바인딩한다.</summary>
    public void BindPlayer(Transform playerRootOrPivot, Transform optionalExplicitPivot = null)
    {
        if (!playerRootOrPivot) return;
        target = playerRootOrPivot;
        explicitPivot = optionalExplicitPivot; // null이면 무시

        // 처음 물릴 때 플레이어 바라보도록 간단 리센터
        Vector3 fwd = GetPivot().forward; fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.001f)
        {
            yaw = Quaternion.LookRotation(fwd.normalized, Vector3.up).eulerAngles.y;
        }
    }

    /// <summary>외부에서 부드럽게 페이드 인(활성화 블렌드) 시작</summary>
    public void BeginBlendIn(float duration)
    {
        _blendInDur = Mathf.Max(0.01f, duration);
        _blendInT = 0f;
        _blendStartPos = transform.position;
        _blendStartRot = transform.rotation;
        _blendInActive = true;
    }

    /// <summary>즉시 리센터</summary>
    public void RecenterToPlayer()
    {
        var p = GetPivot();
        if (!p) return;
        Vector3 forward = p.forward; forward.y = 0;
        if (forward.sqrMagnitude > 0.001f)
        {
            forward.Normalize();
            yaw = Quaternion.LookRotation(forward).eulerAngles.y;
        }
    }

    /// <summary>부드럽게 리센터</summary>
    public void RecenterToPlayerSmooth(float duration = 0.5f)
    {
        if (recenterCoroutine != null) StopCoroutine(recenterCoroutine);
        recenterCoroutine = StartCoroutine(RecenterCoroutine(duration));
    }

    public void SetSensitivity01(float v) => mouseSensitivity = Mathf.Lerp(minSensitivity, maxSensitivity, v);
    public float GetSensitivity01() => Mathf.InverseLerp(minSensitivity, maxSensitivity, mouseSensitivity);

    /// <summary>엎드림 뷰 온/오프</summary>
    public void SetCrawl(bool on, float amount = -1f)
    {
        if (amount > 0f) crawlDown = amount;
        _crawlYTarget = on ? Mathf.Abs(crawlDown) : 0f;
    }

    /// <summary>마우스 입력이 일정 시간 없으면 자동 리센터</summary>
    public void RecenterIfNoMouseFor(float idleWindow, float duration)
    {
        if (_recenterCo != null) StopCoroutine(_recenterCo);
        _recenterCo = StartCoroutine(Co_RecenterIfIdle(idleWindow, duration));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────────

    Transform GetPivot()
    {
        if (explicitPivot) return explicitPivot;
        return target;
    }

    IEnumerator RecenterCoroutine(float duration)
    {
        var p = GetPivot(); if (!p) yield break;
        Vector3 forward = p.forward; forward.y = 0;
        if (forward.sqrMagnitude < 0.001f) yield break;

        forward.Normalize();
        float targetYaw = Quaternion.LookRotation(forward).eulerAngles.y;
        float startYaw = yaw;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            yaw = Mathf.LerpAngle(startYaw, targetYaw, t);
            yield return null;
        }
        yaw = targetYaw;
        recenterCoroutine = null;
    }

    IEnumerator Co_RecenterIfIdle(float idleWindow, float duration)
    {
        float t = 0f;
        const float eps = 0.01f;
        while (t < idleWindow)
        {
            if (Mathf.Abs(Input.GetAxis("Mouse X")) > eps || Mathf.Abs(Input.GetAxis("Mouse Y")) > eps)
                yield break; // 입력 있으면 취소
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return StartCoroutine(RecenterCoroutine_CancelOnInput(duration));
    }

    IEnumerator RecenterCoroutine_CancelOnInput(float duration)
    {
        var p = GetPivot(); if (!p) yield break;
        Vector3 fwd = p.forward; fwd.y = 0;
        if (fwd.sqrMagnitude < 0.001f) yield break;
        fwd.Normalize();

        float targetYaw = Quaternion.LookRotation(fwd).eulerAngles.y;
        float startYaw = yaw;
        float t = 0f;
        const float eps = 0.01f;

        while (t < 1f)
        {
            if (Mathf.Abs(Input.GetAxis("Mouse X")) > eps || Mathf.Abs(Input.GetAxis("Mouse Y")) > eps)
                yield break; // 도중 입력 시 취소

            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            yaw = Mathf.LerpAngle(startYaw, targetYaw, t);
            yield return null;
        }
        yaw = targetYaw;
    }
}
