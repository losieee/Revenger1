using UnityEngine;
using System.Collections;

public class CameraMov : MonoBehaviour
{
    public Transform target; // �÷��̾�
    public float mouseSensitivity = 3f;
    public float distance = 5f;
    public float heightOffset = 1.5f;
    public float pitchMin = -40f;
    public float pitchMax = 80f;

    private float yaw = 0f;
    private float pitch = 15f;

    private Coroutine recenterCoroutine;

    void Update()
    {
        if (Time.timeScale == 0f)
            return;

        // ���콺 �Է�
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // ȸ�� ���
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // ī�޶� ��ġ ���
        Vector3 offset = rotation * new Vector3(0, 0, -distance);
        Vector3 targetPos = target.position + Vector3.up * heightOffset;

        transform.position = targetPos + offset;
        transform.LookAt(targetPos);
    }

    /// <summary>
    /// ��� ������
    /// </summary>
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

    /// <summary>
    /// �ε巴�� ������
    /// </summary>
    public void RecenterToPlayerSmooth(float duration = 0.5f)
    {
        if (recenterCoroutine != null)
        {
            StopCoroutine(recenterCoroutine);
        }
        recenterCoroutine = StartCoroutine(RecenterCoroutine(duration));
    }

    private IEnumerator RecenterCoroutine(float duration)
    {
        Vector3 forward = target.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < 0.001f)
            yield break;

        forward.Normalize();
        float targetYaw = Quaternion.LookRotation(forward).eulerAngles.y;

        float startYaw = yaw;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;

            // �ε巴�� ���� (���� wrap-around ó��)
            yaw = Mathf.LerpAngle(startYaw, targetYaw, t);

            yield return null;
        }

        yaw = targetYaw;
        recenterCoroutine = null;
    }
}
