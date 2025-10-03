using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePiece : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int pieceId = 1;
    public bool isLocked { get; private set; }

    RectTransform rect;
    Canvas canvas;
    CanvasGroup cg;
    Transform startParent;
    Vector2 startAnchoredPos;
    Vector2 startAnchorMin, startAnchorMax;
    Vector2 startOffsetMin, startOffsetMax;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    void Start()
    {
        startParent = transform.parent;
        startAnchoredPos = rect.anchoredPosition;
        startAnchorMin = rect.anchorMin;
        startAnchorMax = rect.anchorMax;
        startOffsetMin = rect.offsetMin;
        startOffsetMax = rect.offsetMax;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        // ★ 이미 슬롯에 고정돼 있어도 집어 들면 슬롯 비우고 이동 허용
        if (isLocked)
        {
            var slot = transform.parent ? transform.parent.GetComponent<DropSlot>() : null;
            if (slot) slot.ReleaseCurrent(this);
            isLocked = false;
        }

        cg.alpha = 0.9f;
        cg.blocksRaycasts = false;
        transform.SetParent(canvas.transform, true);
    }

    public void OnDrag(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, e.position, e.pressEventCamera, out var lp);
        rect.localPosition = lp;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (isLocked) return;
        cg.alpha = 1f;

        // 포인터 아래가 차있는 슬롯이면 그냥 그 자리 유지
        var hoveredSlot = e.pointerEnter ? e.pointerEnter.GetComponentInParent<DropSlot>() : null;
        if (hoveredSlot != null && !hoveredSlot.IsEmpty)
        {
            cg.blocksRaycasts = true;
            return;
        }

        // 슬롯 위로 정확히 드롭되어 부모가 바뀌었으면(=성공) 종료
        if (transform.parent != canvas.transform)
            return;

        // 근접 스냅(빈 슬롯만 대상) 시도
        Vector2 pieceScreen = RectTransformUtility.WorldToScreenPoint(
            e.pressEventCamera, rect.TransformPoint(rect.rect.center));
        var nearest = FoyerPuzzleManager.i?.FindNearestSnappableSlot(pieceScreen);
        if (nearest != null && nearest.TryLock(this)) return;

        // 스냅 실패: 원위치 복귀 대신 '그 자리에 머무름'
        cg.blocksRaycasts = true;
    }

    public void LockTo(Transform slot)
    {
        isLocked = true;
        cg.blocksRaycasts = true;

        transform.SetParent(slot, worldPositionStays: false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public void ResetToStart()
    {
        isLocked = false;
        transform.SetParent(startParent, true);
        rect.anchorMin = startAnchorMin;
        rect.anchorMax = startAnchorMax;
        rect.offsetMin = startOffsetMin;
        rect.offsetMax = startOffsetMax;
        rect.anchoredPosition = startAnchoredPos;
        cg.blocksRaycasts = true;
        FoyerPuzzleManager.i?.OnSlotChanged(); // 상태 갱신(선택)
    }
}
