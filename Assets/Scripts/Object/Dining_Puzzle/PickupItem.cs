using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    private ItemInfo info;

    void Awake()
    {
        info = GetComponent<ItemInfo>();
    }

    public bool TryPickupOrReturn(PlayerMov player)
    {
        if (!player || !info) return false;
        var inv = PlayerInventory.Instance;
        if (!inv) return false;

        // 인벤토리에 이미 있으면 → 되돌리기
        if (inv.ContainsId(info.itemId))
        {
            return inv.TryDropToWorld(info);
        }

        // 줍기
        bool ok = inv.TryAdd(info);
        if (ok && info.returnSpot != null)
        {
            info.returnSpot.ActivateSpot(info.itemId);
        }
        return ok;
    }
}
