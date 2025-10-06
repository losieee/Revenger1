using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SlotPlate : MonoBehaviour
{
    [Header("이 슬롯이 요구하는 아이템 ID")]
    public string requiredId;

    [Header("스냅될 위치/회전(없으면 자신 Transform)")]
    public Transform anchor;

    [Header("배치 시 옵션")]
    public bool freezePhysics = true;

    private ItemInfo placed;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    // PlayerMov에서 호출
    public bool TryPlace()
    {
        if (placed) return false;
        if (PlayerInventory.Instance != null &&
            PlayerInventory.Instance.TryTakeById(requiredId, out var item))
        {
            Place(item);
            return true;
        }
        Debug.Log($"[Slot] {requiredId} 필요!");
        return false;
    }

    void Place(ItemInfo item)
    {
        placed = item;
        var a = anchor ? anchor : transform;
        item.transform.SetPositionAndRotation(a.position, a.rotation);

        var rb = item.GetComponent<Rigidbody>();
        if (rb && freezePhysics)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        var col = item.GetComponent<Collider>();
        if (col) col.isTrigger = true;

        item.gameObject.SetActive(true);
        GetComponentInParent<DiningPuzzleGroup>()?.NotifyChanged();
    }

    public void Clear()
    {
        if (!placed) return;
        PlayerInventory.Instance?.Return(placed);
        placed = null;
    }

    public bool IsCorrectFilled => placed && placed.itemId == requiredId;
    public bool IsFilled => placed != null;
}
