using UnityEngine;

public class BookPlaceController : MonoBehaviour
{
    [Header("퍼즐 참조")]
    public BookshelfPuzzle puzzle;

    [Header("선택된 슬롯(마우스 클릭 등으로 지정)")]
    public BookSlot currentTargetSlot;

    void Awake()
    {
        if (!puzzle) puzzle = FindObjectOfType<BookshelfPuzzle>();
    }

    // --- 슬롯 선택 (예: 슬롯 오브젝트 클릭 시 이 함수 호출) ---
    public void SelectSlot(BookSlot slot)
    {
        currentTargetSlot = slot;
    }

    // --- UI에서 “이 책(ID)을 꽂기” 버튼을 눌렀다고 가정 ---
    public bool PlaceById(string itemId)
    {
        if (!currentTargetSlot || string.IsNullOrEmpty(itemId)) return false;
        var inv = PlayerInventory.Instance;
        if (!inv) return false;

        // 인벤토리에서 해당 ID 책을 꺼내기
        if (!inv.TryTakeById(itemId, out var book)) return false;

        // 타입이 Book인지 체크(원하면 생략 가능)
        if (book.type != ItemInfo.ItemType.Book)
        {
            // 다시 인벤토리로 환원
            inv.Return(book);
            return false;
        }

        // 슬롯에 놓기
        if (!currentTargetSlot.Place(book))
        {
            // 슬롯이 이미 차있다면 환원
            inv.Return(book);
            return false;
        }

        // 4칸이 다 차면 자동 검증
        puzzle.Validate();
        return true;
    }

    // --- 편의: 인벤토리에서 "첫 번째 책"을 자동으로 꽂기 ---
    public bool PlaceFirstBook()
    {
        var inv = PlayerInventory.Instance;
        if (!currentTargetSlot || !inv) return false;

        // 첫 책 탐색
        ItemInfo book = null;
        foreach (var it in inv.Items)
        {
            if (it && it.type == ItemInfo.ItemType.Book) { book = it; break; }
        }
        if (!book) return false;

        // 꺼내서 꽂기
        if (!inv.TryTake(book, out var taken)) return false;
        if (!currentTargetSlot.Place(taken))
        {
            inv.Return(taken);
            return false;
        }

        puzzle.Validate();
        return true;
    }

    public bool EjectCurrentSlotToWorld()
    {
        if (!currentTargetSlot || !currentTargetSlot.current) return false;

        // 슬롯에서 책 빼기 (월드에 남겨주지만 부모/콜라이더만 복구됨)
        var book = currentTargetSlot.Take();
        if (!book) return false;

        // 인벤토리 거치지 않고 바로 시작 위치로 복귀시키고 싶다면 이렇게:
        book.transform.SetPositionAndRotation(book.startPos + Vector3.up * 0.02f, book.startRot);
        var col = book.GetComponent<Collider>(); if (col) col.enabled = true;
        book.gameObject.SetActive(true);

        var rb = book.GetComponent<Rigidbody>();
        if (rb) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        return true;
    }
}
