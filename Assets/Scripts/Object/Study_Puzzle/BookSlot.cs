using UnityEngine;

public class BookSlot : MonoBehaviour
{
    [Header("스냅 포인트(책을 붙일 위치/회전)")]
    public Transform snapPoint;

    [Header("현재 꽂힌 책(없으면 null)")]
    public ItemInfo current;                 // 활성화된 월드 오브젝트

    // 이 슬롯에 책을 꽂는다. (월드 상에 활성화/부모설정/콜라이더 끔)
    public bool Place(ItemInfo book)
    {
        if (current || !book) return false;

        current = book;
        book.gameObject.SetActive(true);
        var t = book.transform;
        t.SetParent(snapPoint ? snapPoint : transform, worldPositionStays: false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;

        var col = book.GetComponent<Collider>();
        if (col) col.enabled = false;           // 꽂힌 뒤엔 클릭 방지

        return true;
    }

    // 슬롯에서 책을 뺀다(월드에 남길지, 인벤토리로 돌릴지는 호출자가 결정)
    public ItemInfo Take()
    {
        if (!current) return null;

        var b = current;
        current = null;

        b.transform.SetParent(null, true);
        var col = b.GetComponent<Collider>();
        if (col) col.enabled = true;

        return b;
    }

    public bool IsFilled => current != null;
}