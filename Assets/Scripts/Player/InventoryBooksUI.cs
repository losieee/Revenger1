using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryBooksUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Transform content;       // Grid or Scroll content
    [SerializeField] private GameObject bookButtonPrefab;

    [Header("Logic")]
    [SerializeField] private BookPlaceController placer; // 씬의 BookPlaceController

    void OnEnable()
    {
        if (!placer) placer = FindObjectOfType<BookPlaceController>(true);
        if (PlayerInventory.Instance) PlayerInventory.Instance.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (PlayerInventory.Instance) PlayerInventory.Instance.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        // 기존 버튼 정리
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        if (!PlayerInventory.Instance) return;

        foreach (var it in PlayerInventory.Instance.Items)
        {
            if (!it) continue;
            if (it.type != ItemInfo.ItemType.Book) continue; // 책만 표시

            var go = Instantiate(bookButtonPrefab, content);
            var btn = go.GetComponent<Button>();
            var icon = go.GetComponentInChildren<Image>(true);
            var label = go.GetComponentInChildren<TMP_Text>(true);

            if (icon) icon.sprite = it.icon;
            if (label) label.text = string.IsNullOrEmpty(it.displayName) ? it.itemId : it.displayName;

            string id = it.itemId; // 캡처
            btn.onClick.AddListener(() =>
            {
                // 현재 선택된 슬롯(placer.currentTargetSlot)에 이 책을 꽂는다
                if (!placer || placer.currentTargetSlot == null) return;
                bool ok = placer.PlaceById(id);

                // 꽂기에 성공했으면 목록 갱신(인벤토리에서 빠지므로 UI 업데이트)
                if (ok) Refresh();
            });
        }
    }

    // 패널 열고/닫기 편의 함수
    public void Open()
    {
        gameObject.SetActive(true);
        PlayerMov.LockControls(showCursor: true); // 커서 보이게 + 조작 잠금
    }

    public void Close()
    {
        gameObject.SetActive(false);
        PlayerMov.UnlockControls(hideCursor: true);
    }
}
