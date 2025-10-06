using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private readonly List<ItemInfo> items = new();
    public IReadOnlyList<ItemInfo> Items => items;

    public void Add(ItemInfo item)
    {
        if (!item || items.Contains(item)) return;
        items.Add(item);
        item.gameObject.SetActive(false); // ÁÖ¿üÀ¸´Ï ¼û±è
        Debug.Log($"[Inv] È¹µæ: {item.displayName} ({item.itemId})");
    }

    public bool TryTakeById(string id, out ItemInfo item)
    {
        int idx = items.FindIndex(i => i && i.itemId == id);
        if (idx >= 0)
        {
            item = items[idx];
            items.RemoveAt(idx);
            return true;
        }
        item = null;
        return false;
    }

    public void Return(ItemInfo item)
    {
        if (!item) return;
        item.transform.SetPositionAndRotation(item.startPos, item.startRot);
        item.gameObject.SetActive(false);
        if (!items.Contains(item))
            items.Add(item);
    }
}
