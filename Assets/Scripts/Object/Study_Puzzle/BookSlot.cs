using UnityEngine;

public class BookSlot : MonoBehaviour
{
    [Header("스냅 지점(없으면 자기 Transform)")]
    public Transform snapPoint;

    [HideInInspector] public ItemInfo current;

    // BookPlaceController에서 쓰는 프로퍼티들
    public Transform SnapPoint => snapPoint ? snapPoint : transform;
    public bool IsFilled => current != null;

    // 인벤토리에서 꺼낸 책을 슬롯에 꽂기
    public bool Place(ItemInfo item)
    {
        if (!item || current != null) return false;

        current = item;

        var go = item.gameObject;
        go.SetActive(true);

        // 스냅 포즈로 배치
        var t = go.transform;
        t.SetParent(SnapPoint, worldPositionStays: false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;

        // 충돌/물리 정리(원하면 조정)
        var col = go.GetComponent<Collider>(); if (col) col.enabled = false;
        var rb = go.GetComponent<Rigidbody>();
        if (rb) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }

        return true;
    }

    // 슬롯에서 책을 빼서 반환 (부모 해제)
    public ItemInfo Take()
    {
        if (!current) return null;

        var it = current;
        current = null;

        var go = it.gameObject;
        it.transform.SetParent(null, true);

        var col = go.GetComponent<Collider>(); if (col) col.enabled = true;

        return it;
    }
}
