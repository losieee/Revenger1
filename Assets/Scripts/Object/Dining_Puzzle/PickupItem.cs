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

    // PlayerMov에서 호출
    public bool TryPickup(PlayerMov who)
    {
        if (!PlayerInventory.Instance || !info) return false;
        PlayerInventory.Instance.Add(info);
        return true;
    }
}
