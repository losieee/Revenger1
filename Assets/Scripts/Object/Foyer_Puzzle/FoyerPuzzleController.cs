using UnityEngine;

public class FoyerPuzzleController : MonoBehaviour
{
    [Header("드래그 가능 구역")]
    public BoxCollider dragArea;                  // 드래그 허용 박스(필수)
    public float areaPadding = 0.01f;             // 가장자리 여유
    public bool includePieceSize = true;          // 조각 콜라이더 크기 고려

    [Header("입력/평면")]
    public Camera cam;
    public LayerMask pickMask;
    public Transform dragPlane;
    public float hoverHeight = 0.005f;

    DragPiece3D grabbed;
    Plane plane;
    public PuzzleGroup group;

    void Awake()
    {
        // dragArea 자동 바인딩(인스펙터에 안 넣어도 동작하도록)
        if (!dragArea) dragArea = GetComponentInChildren<BoxCollider>(true);
        if (!cam) cam = Camera.main;
    }

    void OnEnable()
    {
        // 평면 기준: dragPlane 없으면 dragArea를 기준으로 사용
        var basis = dragPlane ? dragPlane : (dragArea ? dragArea.transform : null);
        var n = basis ? basis.up : Vector3.up;
        var p = basis ? basis.position : Vector3.zero;
        plane = new Plane(n, p);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) TryPick();

        if (grabbed)
        {
            MoveGrabbed();
            if (Input.GetMouseButtonUp(0)) Drop();
        }
    }

    void TryPick()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 100f, pickMask))
        {
            var piece = hit.collider.GetComponentInParent<DragPiece3D>();
            if (piece != null)
            {
                grabbed = piece;
                piece.Release();
            }
        }
    }

    void MoveGrabbed()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (plane.Raycast(ray, out float t))
        {
            var posOnPlane = ray.GetPoint(t);

            // 영역 클램프 적용
            if (dragArea) posOnPlane = ClampToArea(posOnPlane, grabbed);

            grabbed.transform.position = posOnPlane + plane.normal * hoverHeight;
        }
    }

    void Drop()
    {
        SnapSocket3D best = null;
        float bestDist = float.MaxValue;

        foreach (var s in group.sockets)
        {
            if (!s.IsEmpty) continue;

            float d = Vector3.Distance(grabbed.transform.position, s.AnchorPos);
            if (d < s.snapRange && d < bestDist)
            {
                best = s;
                bestDist = d;
            }
        }

        if (best != null && best.TryLock(grabbed))
        {
            grabbed = null;
            return;
        }

        grabbed = null; // 실패 시 그대로 둠
    }

    // 영역 클램프 로직
    Vector3 ClampToArea(Vector3 worldPos, DragPiece3D piece)
    {
        var t = dragArea.transform;

        // 영역 로컬로 변환
        Vector3 local = t.InverseTransformPoint(worldPos);
        Vector3 half = dragArea.size * 0.5f;

        // 조각 절반 크기(옵션) - 영역 로컬 기준
        Vector3 halfPiece = Vector3.zero;
        if (includePieceSize && piece)
        {
            var col = piece.GetComponentInChildren<Collider>();
            if (col)
            {
                Vector3 minL = t.InverseTransformPoint(col.bounds.min);
                Vector3 maxL = t.InverseTransformPoint(col.bounds.max);
                Vector3 sizeL = new Vector3(
                    Mathf.Abs(maxL.x - minL.x),
                    Mathf.Abs(maxL.y - minL.y),
                    Mathf.Abs(maxL.z - minL.z)
                );
                halfPiece = sizeL * 0.5f;
            }
        }

        float minX = -half.x + areaPadding + halfPiece.x;
        float maxX = half.x - areaPadding - halfPiece.x;
        float minZ = -half.z + areaPadding + halfPiece.z;
        float maxZ = half.z - areaPadding - halfPiece.z;

        local.x = Mathf.Clamp(local.x, minX, maxX);
        local.z = Mathf.Clamp(local.z, minZ, maxZ);

        return t.TransformPoint(local);
    }
}
