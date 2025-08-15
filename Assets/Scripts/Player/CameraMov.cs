using UnityEngine;
using System.Collections;

public class CameraMov : MonoBehaviour
{
    public Transform target; // 플레이어
    [Header("카메라 옵션")]
    public float mouseSensitivity = 3f;
    public float distance = 5f;        // 목표(최대) 거리
    public float minDistance = 0.5f;   // 너무 가까워지는 것 방지
    public float heightOffset = 1.5f;
    public float pitchMin = -40f;
    public float pitchMax = 80f;

    [Header("충돌 감지")]
    public float collisionRadius = 0.25f;       // 구(SphereCast) 반지름
    public float collisionOffset = 0.15f;       // 벽에서 살짝 띄우는 양
    public LayerMask collisionLayers = ~0;      // 충돌 감지 레이어 (플레이어나 카메라 제외 권장)

    [Header("카메라 거리 조절 스무스")]
    public float pullInSmoothTime = 0.05f;      // 벽에 부딪혀 당길 때 스무딩(빠르게)
    public float relaxOutSmoothTime = 0.15f;    // 벽이 사라져 멀어질 때 스무딩(천천히)

    private float yaw = 0f;
    private float pitch = 15f;

    private float currentDistance;              // 현재 카메라-타겟 거리
    private float distanceVelocity;             // SmoothDamp용 내부 속도

    private Coroutine recenterCoroutine;

    void Start()
    {
        currentDistance = Mathf.Clamp(distance, minDistance, Mathf.Max(minDistance, distance));
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f || target == null) return;

        // 1) 입력 → 각도 계산
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // 2) 타겟 기준점
        Vector3 targetPos = target.position + Vector3.up * heightOffset;

        // 3) 충돌 감지: SphereCast로 목표 거리 내 장애물 확인
        float targetDistance = distance; // 기본은 최대 거리
        Vector3 camDir = (rotation * Vector3.back).normalized; // 뒤(-Z) 방향

        Ray ray = new Ray(targetPos, camDir);
        if (Physics.SphereCast(ray, collisionRadius, out RaycastHit hit, distance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Clamp(hit.distance - collisionOffset, minDistance, distance);
        }

        // 4) 히스테리시스: 당길 때는 빠르게, 풀릴 때는 느리게
        float smoothTime = (targetDistance < currentDistance) ? pullInSmoothTime : relaxOutSmoothTime;
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, smoothTime);

        // 5) 실제 위치 적용
        Vector3 desiredPos = targetPos + camDir * currentDistance;

        // (안전장치) 이동 중 미세한 클리핑이 보이면 한번 더 라인 체크
        if (Physics.Linecast(targetPos, desiredPos, out RaycastHit hit2, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            float safeDist = Mathf.Clamp(hit2.distance - collisionOffset, minDistance, currentDistance);
            currentDistance = safeDist;
            desiredPos = targetPos + camDir * currentDistance;
        }

        transform.position = desiredPos;
        transform.LookAt(targetPos);
    }

    /// <summary>즉시 리센터</summary>
    public void RecenterToPlayer()
    {
        Vector3 forward = target.forward;
        forward.y = 0;
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

    private IEnumerator RecenterCoroutine(float duration)
    {
        Vector3 forward = target.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < 0.001f) yield break;

        forward.Normalize();
        float targetYaw = Quaternion.LookRotation(forward).eulerAngles.y;

        float startYaw = yaw;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            yaw = Mathf.LerpAngle(startYaw, targetYaw, t);
            yield return null;
        }

        yaw = targetYaw;
        recenterCoroutine = null;
    }
}
