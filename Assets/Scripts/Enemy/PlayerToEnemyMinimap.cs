using System.Collections;
using UnityEngine;

public class PlayerToEnemyMinimap : MonoBehaviour
{
    [Header("미니맵 전용 오브젝트")]
    public GameObject miniIcon;   // EnemyMini
    public GameObject miniFov;    // 미니맵용 FOV 오브젝트(표시 Mesh 등)

    [Header("가시성 판정")]
    public Renderer[] bodyRenderers;      // 적의 Renderer들(바운즈용)
    public Transform losPoint;            // 가시선 체크 기준(머리 위치 등), 비우면 transform
    public LayerMask occluderMask = ~0;   // 벽/기둥 등 가림막
    public float checkInterval = 0.1f;    // 성능을 위한 주기
    public int losSamples = 3;            // 바운즈 샘플 개수(센터+코너 일부)

    Camera cam;
    WaitForSeconds wait;

    void OnEnable()
    {
        cam = Camera.main;
        wait = new WaitForSeconds(checkInterval);
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        while (true)
        {
            bool visible = IsVisibleFromMainCamera();
            if (miniIcon && miniIcon.activeSelf != visible) miniIcon.SetActive(visible);
            if (miniFov && miniFov.activeSelf != visible) miniFov.SetActive(visible);
            yield return wait;
        }
    }

    bool IsVisibleFromMainCamera()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        // 1) 프러스텀 체크
        Bounds b = GetCombinedBounds();
        if (b.size == Vector3.zero) b = new Bounds(transform.position, Vector3.one * 0.5f);
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        if (!GeometryUtility.TestPlanesAABB(planes, b)) return false;

        // 2) 가림막 체크 - 샘플 중 하나라도 뚫려 있으면 "보인다"
        Vector3[] points = SampleBounds(b, losSamples);
        Vector3 camPos = cam.transform.position;

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 to = points[i] - camPos;
            float dist = to.magnitude;
            if (dist <= 0.001f) continue;
            Vector3 dir = to / dist;

            // 벽 등에 막히면 true가 아님
            bool blocked = Physics.Raycast(
                camPos, dir, dist, occluderMask, QueryTriggerInteraction.Ignore);

            if (!blocked)
                return true; // 한 점이라도 시야가 트여 있으면 보이는 것으로 처리
        }

        return false; // 전부 막혀 있으면 안 보임
    }

    Bounds GetCombinedBounds()
    {
        Bounds bounds = new Bounds();
        bool hasAny = false;
        if (bodyRenderers != null)
        {
            foreach (var r in bodyRenderers)
            {
                if (r == null) continue;
                if (!hasAny) { bounds = r.bounds; hasAny = true; }
                else bounds.Encapsulate(r.bounds);
            }
        }
        if (!hasAny)
        {
            // 대체: 트랜스폼 기준 작은 바운즈
            bounds = new Bounds((losPoint ? losPoint : transform).position, Vector3.one * 0.5f);
        }
        return bounds;
    }

    Vector3[] SampleBounds(Bounds b, int samples)
    {
        // 최소: 중심 1개
        if (samples <= 1)
            return new[] { b.center };

        // 중심 + 4코너(위/아래) 중 일부
        var list = new System.Collections.Generic.List<Vector3> { b.center };
        list.Add(b.center + new Vector3(+b.extents.x, 0, +b.extents.z));
        list.Add(b.center + new Vector3(+b.extents.x, 0, -b.extents.z));
        list.Add(b.center + new Vector3(-b.extents.x, 0, +b.extents.z));
        list.Add(b.center + new Vector3(-b.extents.x, 0, -b.extents.z));

        // 필요하면 위쪽도 약간 포함
        list.Add(b.center + new Vector3(0, +b.extents.y * 0.6f, 0));

        // 요청 개수만큼 자르기
        if (list.Count > samples) list.RemoveRange(samples, list.Count - samples);
        return list.ToArray();
    }
}
