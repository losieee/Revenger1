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

    void OnMouseDown()   // 간단: 3D 오브젝트 클릭
    {
        if (placer && slot) placer.SelectSlot(slot);
        // 선택 시 하이라이트 켜기 등은 여기서
    }
}
