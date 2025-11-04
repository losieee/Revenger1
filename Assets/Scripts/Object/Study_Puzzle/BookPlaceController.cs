using UnityEngine;

public class BookPlaceController : MonoBehaviour
{
    [Header("퍼즐 참조")]
    public BookshelfPuzzle puzzle;

    [Header("선택된 슬롯(마우스 클릭 등으로 지정)")]
    public BookSlot currentTargetSlot;

    [SerializeField] private AudioClip placeClip;

    void Awake()
    {
        if (!puzzle) puzzle = FindObjectOfType<BookshelfPuzzle>();
    }

    // --- 슬롯 선택 (예: 슬롯 오브젝트 클릭 시 이 함수 호출) ---
    public void SelectSlot(BookSlot slot)
    {
        currentTargetSlot = slot;
    }

    public bool PlaceById(string id)
    {
        if (!currentTargetSlot) return false;
        var inv = PlayerInventory.Instance;
        if (!inv) return false;

        if (!inv.TryTakeById(id, out var item)) return false;

        bool placed = currentTargetSlot.Place(item);
        if (placed)
        {
            // 꽂았을 때
            SfxPlayer.Play2D(placeClip);

            // 4칸 다 찼으면 퍼즐이 Validate() 되도록 기존 로직 유지
            puzzle?.ValidateIfFull();
            return true;
        }

        // 실패 시 아이템을 인벤토리로 되돌려두는 기존 처리 유지
        inv.Return(item);
        return false;
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

        //var rb = book.GetComponent<Rigidbody>();
        //if (rb) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        return true;
    }
}
