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

    // 같은 층 판정용 임계값
    public float sameFloorHeightEpsilon = 0.6f;

    Camera cam;
    WaitForSeconds wait;

    // 재사용 버퍼 (할당 줄이기)
    static readonly RaycastHit[] hits = new RaycastHit[8];
    Collider[] selfCols;

    void OnEnable()
    {
        cam = Camera.main;
        wait = new WaitForSeconds(checkInterval);
        selfCols = GetComponentsInChildren<Collider>();
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

        // 2) 샘플 포인트 구성: 머리(또는 상체) 위주
        var points = SamplePointsHigh(b, losSamples);

        // 3) 라인오브사이트
        Vector3 camPos = cam.transform.position;
        for (int i = 0; i < points.Length; i++)
        {
            if (HasLineOfSight(camPos, points[i]))
                return true;
        }
        return false;
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

    // 머리/상체 쪽을 우선 샘플
    Vector3[] SamplePointsHigh(Bounds b, int samples)
    {
        if (losPoint) return new[] { losPoint.position }; // 지정돼 있으면 그 지점만

        var list = new System.Collections.Generic.List<Vector3>();
        Vector3 chest = b.center + Vector3.up * (b.extents.y * 0.8f);
        list.Add(chest); // 상체
        list.Add(chest + new Vector3(+b.extents.x * 0.6f, 0, 0));
        list.Add(chest + new Vector3(-b.extents.x * 0.6f, 0, 0));
        list.Add(chest + new Vector3(0, 0, +b.extents.z * 0.6f));
        list.Add(chest + new Vector3(0, 0, -b.extents.z * 0.6f));

        if (list.Count > samples) list.RemoveRange(samples, list.Count - samples);
        return list.ToArray();
    }

    // 첫 충돌체가 '적 자신'이 아니면 가려진 것으로 간주
    bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        dir /= dist;

        // 같은 층처럼 높이 차가 작으면 바닥에 스치지 않도록 샘플 포인트가 높고,
        // 레이는 Ground를 맞을 확률이 크게 줄어듭니다.
        int hitCount = Physics.RaycastNonAlloc(from, dir, hits, dist, occluderMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            var h = hits[i];
            var c = h.collider;
            if (c == null) continue;

            // 적 자신의 콜라이더는 무시
            if (c.transform.IsChildOf(transform)) continue;

            // (선택) 같은 층이면 Ground에 스친 히트는 무시하고 지나가도 됨
            if (Mathf.Abs(from.y - to.y) < sameFloorHeightEpsilon && c.gameObject.layer == LayerMask.NameToLayer("Ground"))
                continue;

            // 여기까지 왔으면 진짜 가림막에 막힘
            return false;
        }

        return true;
    }
}
