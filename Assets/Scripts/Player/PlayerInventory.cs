using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [SerializeField] private List<ItemInfo> items = new();  
    public int Count => items.Count;
    public IReadOnlyList<ItemInfo> Items => items;

    public static bool PickupsLocked { get; set; } = false;

    // UI가 구독할 이벤트
    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // 필요하면 유지
        //DontDestroyOnLoad(gameObject);
    }

    void NotifyChanged() => OnChanged?.Invoke();

    public bool TryAdd(ItemInfo info)
    {
        if (PickupsLocked)
        {
            return false;
        }
        if (!info) return false;

        if (!items.Contains(info)) items.Add(info);
        info.gameObject.SetActive(false);
        NotifyChanged();
        return true;
    }

    public void Add(ItemInfo info)
    {
        if (!TryAdd(info)) return;
    }

    // id로 꺼내기
    public bool TryTakeById(string id, out ItemInfo taken)
    {
        taken = null;
        int idx = items.FindIndex(i => i && i.itemId == id);
        if (idx < 0) return false;

        taken = items[idx];
        items.RemoveAt(idx);
        NotifyChanged();                              
        return true;
    }

    // 참조로 꺼내기 (동적 UI 선택용)
    public bool TryTake(ItemInfo target, out ItemInfo taken)
    {
        taken = null;
        int idx = items.FindIndex(i => i == target);
        if (idx < 0) return false;

        taken = items[idx];
        items.RemoveAt(idx);
        NotifyChanged();                               
        return true;
    }

    public void Return(ItemInfo info)
    {
        if (!items.Contains(info)) items.Add(info);
        if (info) info.gameObject.SetActive(false);
        NotifyChanged();                              
    }

    // (선택) 편의 함수들
    public bool ContainsId(string id) => items.Exists(i => i && i.itemId == id);
    public void Clear()
    {
        items.Clear();
        NotifyChanged();
    }
}
