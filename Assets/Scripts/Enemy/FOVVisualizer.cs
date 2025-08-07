using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FOVVisualizer : MonoBehaviour
{
    public int segments = 50;

    public EnemyMov enemy;

    private float viewAngle;    // 시야각
    private float viewRadius;   // 시야 거리

    private Mesh mesh;

    void Start()
    {
        viewAngle = enemy.viewAngle;
        viewRadius = enemy.viewDistance;
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
    }
    void LateUpdate()
    {
        CreateViewMesh();
    }

    void CreateViewMesh()
    {
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = -viewAngle / 2f + viewAngle * i / segments;
            Vector3 dir = DirFromAngle(angle);

            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 worldDir = transform.rotation * dir;

            float distance = viewRadius;
            if (Physics.Raycast(rayOrigin, worldDir, out RaycastHit hit, viewRadius))
            {
                distance = hit.distance;
            }

            // Raycast 방향을 로컬로 변환해서 정확히 Mesh 정점 위치로 반영
            Vector3 localPos = transform.InverseTransformPoint(rayOrigin + worldDir * distance);
            vertices[i + 1] = localPos;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    Vector3 DirFromAngle(float angleInDegrees)
    {
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}