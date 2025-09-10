using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FOVVisualizer : MonoBehaviour
{
    public int segments = 50;

    [Header("��� ���� (�� �� �ϳ� ����)")]
    public EnemyMov enemy;
    public Villain villain;

    [Header("���̾�(������) - ����")]
    public LayerMask occluderMask = ~0; // �⺻�� �� ���̾� �浹

    [Header("��� ��ġ/ȸ�� ����ȭ")]
    public bool syncToTargetTransform = true;

    [Header("EnemyMov �ɼ�")]
    // EnemyMov�� IsChasingPublic�� �� �־ �����ϵ��� ��۷� ó��
    public bool enemyUseChaseDistance = false; // �߰� ǥ�� ���ϸ� true

    [Header("Villain�� FOV (Villain�� ��ũ ������ �� ���)")]
    public float villainViewAngle = 70f;
    public float villainViewDistance = 18f;
    public float villainChaseViewDistance = 60f;
    public float villainEyeHeight = 1.7f;

    private Mesh mesh;

    void Start()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
    }

    void LateUpdate()
    {
        Transform targetTr = null;
        float angle, radius, eyeH;

        if (enemy != null)
        {
            targetTr = enemy.transform;
            angle = enemy.viewAngle;
            radius = enemyUseChaseDistance ? enemy.chaseViewDistance : enemy.viewDistance;
            eyeH = 1.5f; // �ʿ��ϸ� EnemyMov�� �����̸� public���� �����ؼ� ������
        }
        else if (villain != null)
        {
            targetTr = villain.transform;
            angle = villainViewAngle;
            // Villain�� ��ũ �����̹Ƿ� ���¸� ���� �Ÿ� ����Ī
            bool villainIsChasing = (villain.state == Villain.VillainState.Chasing);
            radius = villainIsChasing ? villainChaseViewDistance : villainViewDistance;
            eyeH = villainEyeHeight;
        }
        else
        {
            return; // ��� ������ �׸��� ����
        }

        if (syncToTargetTransform && targetTr)
        {
            transform.position = targetTr.position;
            transform.rotation = targetTr.rotation;
        }

        CreateViewMesh(angle, radius, eyeH);
    }

    void CreateViewMesh(float viewAngle, float viewRadius, float eyeHeight)
    {
        if (mesh == null) return;

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float a = -viewAngle / 2f + viewAngle * i / segments;
            Vector3 dir = DirFromAngle(a);

            Vector3 rayOrigin = transform.position + Vector3.up * eyeHeight;
            Vector3 worldDir = transform.rotation * dir;

            float distance = viewRadius;
            if (Physics.Raycast(rayOrigin, worldDir, out RaycastHit hit, viewRadius, occluderMask, QueryTriggerInteraction.Ignore))
                distance = hit.distance;

            Vector3 localPos = transform.InverseTransformPoint(rayOrigin + worldDir * distance);
            vertices[i + 1] = localPos;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    Vector3 DirFromAngle(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
    }
}
