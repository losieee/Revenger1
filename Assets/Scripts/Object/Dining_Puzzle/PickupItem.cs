using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    private ItemInfo info;

    void Awake()
    {
        info = GetComponent<ItemInfo>();
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    public bool TryPickup(PlayerMov player)
    {
        if (!player || !info) return false;
        var inv = PlayerInventory.Instance;
        if (!inv) return false;

        // 잠금 상태면 false 반환 (먹기 실패)
        bool ok = inv.TryAdd(info);
        return ok;
    }
}
