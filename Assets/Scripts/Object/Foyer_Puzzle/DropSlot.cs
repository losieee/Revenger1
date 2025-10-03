using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class DropSlot : MonoBehaviour, IDropHandler
{
    public int slotId = 1;
    DraggablePiece current;

    public static readonly List<DropSlot> All = new List<DropSlot>();
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public bool IsEmpty => current == null;
    public DraggablePiece Current => current;         // ★ 현재 꽂힌 조각 조회용

    public Vector3 WorldCenter()
    {
        var rt = (RectTransform)transform;
        return rt.TransformPoint(rt.rect.center);
    }

    // ★ 정답 여부와 상관없이 '빈 슬롯이면' 끼워 넣음
    public bool TryLock(DraggablePiece drag)
    {
        if (!IsEmpty) return false;

        drag.LockTo(transform);
        current = drag;

        FoyerPuzzleManager.i?.OnSlotChanged();       // 매번 상태 재평가
        return true;
    }

    // ★ 조각을 집어 들면 슬롯 비우기 (이동 허용)
    public void ReleaseCurrent(DraggablePiece who = null)
    {
        if (current == null) return;
        if (who != null && current != who) return;
        current = null;
        FoyerPuzzleManager.i?.OnSlotChanged();
    }

    public void OnDrop(PointerEventData e)
    {
        var drag = e.pointerDrag ? e.pointerDrag.GetComponent<DraggablePiece>() : null;
        if (drag == null || drag.isLocked) return;

        // 이미 차 있으면 무시(교체 금지) + 되돌리지 않음
        if (current != null)
        {
            return;
        }

        // 빈 슬롯이면 수락(조각 ID와 무관)
        TryLock(drag);
    }
}
