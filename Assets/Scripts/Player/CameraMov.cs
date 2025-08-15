using UnityEngine;
using System.Collections;

public class CameraMov : MonoBehaviour
{
    public Transform target; // �÷��̾�
    [Header("ī�޶� �ɼ�")]
    public float mouseSensitivity = 3f;
    public float distance = 5f;        // ��ǥ(�ִ�) �Ÿ�
    public float minDistance = 0.5f;   // �ʹ� ��������� �� ����
    public float heightOffset = 1.5f;
    public float pitchMin = -40f;
    public float pitchMax = 80f;

    [Header("�浹 ����")]
    public float collisionRadius = 0.25f;       // ��(SphereCast) ������
    public float collisionOffset = 0.15f;       // ������ ��¦ ���� ��
    public LayerMask collisionLayers = ~0;      // �浹 ���� ���̾� (�÷��̾ ī�޶� ���� ����)

    [Header("ī�޶� �Ÿ� ���� ������")]
    public float pullInSmoothTime = 0.05f;      // ���� �ε��� ��� �� ������(������)
    public float relaxOutSmoothTime = 0.15f;    // ���� ����� �־��� �� ������(õõ��)

    private float yaw = 0f;
    private float pitch = 15f;

    private float currentDistance;              // ���� ī�޶�-Ÿ�� �Ÿ�
    private float distanceVelocity;             // SmoothDamp�� ���� �ӵ�

    private Coroutine recenterCoroutine;

    void Start()
    {
        currentDistance = Mathf.Clamp(distance, minDistance, Mathf.Max(minDistance, distance));
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f || target == null) return;

        // 1) �Է� �� ���� ���
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // 2) Ÿ�� ������
        Vector3 targetPos = target.position + Vector3.up * heightOffset;

        // 3) �浹 ����: SphereCast�� ��ǥ �Ÿ� �� ��ֹ� Ȯ��
        float targetDistance = distance; // �⺻�� �ִ� �Ÿ�
        Vector3 camDir = (rotation * Vector3.back).normalized; // ��(-Z) ����

        Ray ray = new Ray(targetPos, camDir);
        if (Physics.SphereCast(ray, collisionRadius, out RaycastHit hit, distance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Clamp(hit.distance - collisionOffset, minDistance, distance);
        }

        // 4) �����׸��ý�: ��� ���� ������, Ǯ�� ���� ������
        float smoothTime = (targetDistance < currentDistance) ? pullInSmoothTime : relaxOutSmoothTime;
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, smoothTime);

        // 5) ���� ��ġ ����
        Vector3 desiredPos = targetPos + camDir * currentDistance;

        // (������ġ) �̵� �� �̼��� Ŭ������ ���̸� �ѹ� �� ���� üũ
        if (Physics.Linecast(targetPos, desiredPos, out RaycastHit hit2, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            float safeDist = Mathf.Clamp(hit2.distance - collisionOffset, minDistance, currentDistance);
            currentDistance = safeDist;
            desiredPos = targetPos + camDir * currentDistance;
        }

        transform.position = desiredPos;
        transform.LookAt(targetPos);
    }

    /// <summary>��� ������</summary>
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

    /// <summary>�ε巴�� ������</summary>
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
