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

    [Header("Limits")]
    [SerializeField] private int maxBooks = 4;

    // UI가 구독할 이벤트
    public event Action OnChanged;

    [SerializeField] private AudioClip dropReturnClip;      // 되돌리는 사운드

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    void NotifyChanged() => OnChanged?.Invoke();

    private int CountOf(ItemInfo.ItemType t)
    {
        int c = 0;
        for (int i = 0; i < items.Count; i++)
            if (items[i] && items[i].type == t) c++;
        return c;
    }

    private bool CanAdd(ItemInfo info)
    {
        if (!info) return false;
        if (PickupsLocked) return false;

        if (info.type == ItemInfo.ItemType.Generic && CountOf(ItemInfo.ItemType.Generic) >= 4)
            return false;

        // 책 4개 제한
        if (info.type == ItemInfo.ItemType.Book && CountOf(ItemInfo.ItemType.Book) >= maxBooks)
            return false;

        return true;
    }

    public bool TryAdd(ItemInfo info)
    {
        if (!CanAdd(info)) return false;

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

    // 책 되돌리기
    public bool TryDropToWorld(ItemInfo info, Vector3? worldPos = null, Quaternion? worldRot = null)
    {
        if (!info) return false;

        // 인벤토리에서 제거
        if (!TryTake(info, out var taken)) return false;

        // 되돌릴 위치/회전 결정
        Vector3 pos = worldPos ?? taken.startPos;
        Quaternion rot = worldRot ?? taken.startRot;

        // 안전하게 살짝 위로 띄워 충돌 방지
        pos += Vector3.up * 0.02f;

        // 월드에 복귀
        var go = taken.gameObject;
        go.transform.SetPositionAndRotation(pos, rot);

        var col = go.GetComponent<Collider>();
        if (col) col.enabled = true;

        go.SetActive(true);

        if (dropReturnClip)
        {
            // 리턴 스팟이 있다면 그 위치, 없으면 배치 위치에서 재생
            Vector3 sfxPos = (taken.returnSpot ? taken.returnSpot.transform.position : pos);
            AudioSource.PlayClipAtPoint(dropReturnClip, sfxPos, 1f);
        }

        // UI 갱신 이벤트는 TryTake 내부에서 이미 호출됨
        return true;
    }

    // id로 되돌리기 편의 함수
    public bool TryDropToWorldById(string id)
    {
        if (!TryTakeById(id, out var taken)) return false;
        return TryDropToWorld(taken);
    }
}
