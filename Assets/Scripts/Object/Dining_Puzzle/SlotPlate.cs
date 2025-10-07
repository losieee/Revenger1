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

    public bool IsLocked { get; private set; }
    public ItemInfo CurrentPlaced => placed;

    public bool HasPlaced => placed != null;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    // 잠금
    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        // 잠그면 콜라이더도 꺼서 상호작용 자체가 안 열리게
        var col = GetComponent<Collider>();
        if (col) col.enabled = !locked;
    }

    // 이미 채워져 있으면 열지 않음
    public bool TryOpenUI()
    {
        if (IsLocked) return false;
        // 항상 UI 열기 (이미 들어있어도 UI는 열 수 있음)
        PlacementUI.i?.Open(this, requiredId);
        return true;
    }

    // 실제 아이템을 슬롯에 배치
    public void Place(ItemInfo item)
    {
        if (IsLocked) return;

        if (HasPlaced) return;

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
        Debug.Log($"[SlotPlate] 배치 완료: {item.displayName} → {name}");

        GetComponentInParent<DiningPuzzleGroup>()?.NotifyChanged();
    }

    // 이미 올려둔 아이템을 인벤토리로 되돌림
    public void TakeBack()
    {
        if (IsLocked) return;
        if (!placed) return;

        PlayerInventory.Instance?.Return(placed);
        placed = null;

        Debug.Log($"[SlotPlate] '{name}'에서 아이템을 되돌림");
        GetComponentInParent<DiningPuzzleGroup>()?.NotifyChanged();
    }

    // 슬롯을 완전히 비움 (퍼즐 리셋용)
    public void Clear()
    {
        if (IsLocked) return;
        if (!placed) return;

        PlayerInventory.Instance?.Return(placed);
        placed = null;
    }

    public bool IsCorrectFilled => placed && placed.itemId == requiredId;
    public bool IsFilled => placed != null;
}
