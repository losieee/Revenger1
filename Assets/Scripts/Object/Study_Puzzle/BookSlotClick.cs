using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BookSlotClick : MonoBehaviour
{
    public BookSlot slot;
    BookPlaceController placer;

    void Awake()
    {
        if (!slot) slot = GetComponent<BookSlot>();
        placer = FindObjectOfType<BookPlaceController>();
    }

    void OnMouseDown()
    {
        if (!placer || !slot) return;

        // 슬롯 선택
        placer.SelectSlot(slot);

        // 이미 책이 꽂혀 있다면 다시 클릭 시 인벤토리로 반환
        if (slot.IsFilled && slot.current != null)
        {
            var book = slot.Take();
            if (book != null)
            {
                PlayerInventory.Instance.Return(book);
            }
        }
    }
}
