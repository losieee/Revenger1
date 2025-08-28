using UnityEngine;

public class PlayerCorpseDrag : MonoBehaviour
{
    [Header("설정")]
    public KeyCode toggleKey = KeyCode.E;   // 토글 키
    public float grabRange = 2f;            // 시체 잡는 범위
    public LayerMask corpseHandleMask;      // 잡을 수 있는 Handle 전용 Layer
    public Transform dragAnchor;            // 플레이어 뒤쪽 앵커 (없으면 자동 생성)

    private PlayerMov playerMov;            // 플레이어 이동 스크립트 참조
    private DraggableCorpse grabbedCorpse;  // 현재 잡은 시체
    private bool isDragging = false;        // 드래그 상태

    void Start()
    {
        playerMov = GetComponent<PlayerMov>();

        // DragAnchor가 없으면 자동 생성 (플레이어 뒤쪽에 위치)
        if (!dragAnchor)
        {
            GameObject go = new GameObject("DragAnchor");
            dragAnchor = go.transform;
            dragAnchor.SetParent(transform);
            dragAnchor.localPosition = new Vector3(0, 0.1f, -0.8f);
        }
    }

    void Update()
    {
        // 토글키 입력
        if (Input.GetKeyDown(toggleKey))
        {
            if (isDragging) ReleaseCorpse();
            else TryGrabNearestCorpse();
        }
    }

    // 주변 시체 핸들 찾고 잡기 시도
    void TryGrabNearestCorpse()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, grabRange, corpseHandleMask);
        DraggableCorpse best = null;
        float bestDist = float.MaxValue;

        foreach (var h in hits)
        {
            var corpse = h.GetComponentInParent<DraggableCorpse>();
            if (corpse && corpse.IsDeadAndDraggable)
            {
                float d = Vector3.Distance(transform.position, h.ClosestPoint(transform.position));
                if (d < bestDist)
                {
                    best = corpse;
                    bestDist = d;
                }
            }
        }

        if (best != null && best.TryGrab(dragAnchor))
        {
            grabbedCorpse = best;
            isDragging = true;
            playerMov.OnDragStart();
        }
    }

    // 시체 놓기
    void ReleaseCorpse()
    {
        if (grabbedCorpse) { grabbedCorpse.Release(); grabbedCorpse = null; }
        isDragging = false;
        playerMov.OnDragStop();
    }
}
