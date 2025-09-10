using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FOVVisualizer : MonoBehaviour
{
    public int segments = 50;

    [Header("대상 선택 (둘 중 하나 지정)")]
    public EnemyMov enemy;
    public Villain villain;

    [Header("레이어(가림막) - 선택")]
    public LayerMask occluderMask = ~0; // 기본은 전 레이어 충돌

    [Header("대상 위치/회전 동기화")]
    public bool syncToTargetTransform = true;

    [Header("EnemyMov 옵션")]
    // EnemyMov에 IsChasingPublic을 안 넣어도 동작하도록 토글로 처리
    public bool enemyUseChaseDistance = false; // 추격 표시 원하면 true

    [Header("Villain용 FOV (Villain이 마크 전용일 때 사용)")]
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
            eyeH = 1.5f; // 필요하면 EnemyMov에 눈높이를 public으로 노출해서 쓰세요
        }
        else if (villain != null)
        {
            targetTr = villain.transform;
            angle = villainViewAngle;
            // Villain은 마크 전용이므로 상태만 보고 거리 스위칭
            bool villainIsChasing = (villain.state == Villain.VillainState.Chasing);
            radius = villainIsChasing ? villainChaseViewDistance : villainViewDistance;
            eyeH = villainEyeHeight;
        }
        else
        {
            return; // 대상 없으면 그리지 않음
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
