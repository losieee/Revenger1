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
        if (!inv)   return false;

        // 인벤토리에 이미 있으면 → 되돌리기
        if (inv.ContainsId(info.itemId))
        {
            bool ok = inv.TryDropToWorld(info);

            if (ok)
            {
                if (info.returnSpot)
                    info.returnSpot.MarkFilled(info.itemId);
            }

            return ok;
        }

        // 줍기
        bool added = inv.TryAdd(info);
        if (added)
        {
            if (info.pickupClip)
                SoundManager.Play2D(info.pickupClip);
          
            if (info.returnSpot) info.returnSpot.ActivateSpot(info.itemId);
        }
        return added;
    }
}
