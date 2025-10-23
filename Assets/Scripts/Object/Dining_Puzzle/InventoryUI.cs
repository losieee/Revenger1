using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public Image[] topSlotImages;
    public Button[] topSlotButtons;
    public Sprite emptySprite;
    public Image selectedPreview;
    [SerializeField] private Sprite defaultPreviewSprite;

    [SerializeField] private bool useTypeFilter = true;
    [SerializeField] private ItemInfo.ItemType filterType = ItemInfo.ItemType.Generic;

    private Sprite originalPreviewSprite;

    private List<ItemInfo> _visible = new();

    int selectedIndex = -1;

    void Awake()
    {
        if (selectedPreview != null)
        {
            if (selectedPreview.sprite == null && defaultPreviewSprite != null)
                selectedPreview.sprite = defaultPreviewSprite;

            originalPreviewSprite = selectedPreview.sprite; // 백업
        }
    }

    void OnEnable()
    {
        if (selectedPreview != null)
        {
            if (selectedPreview.sprite == null && defaultPreviewSprite != null)
                selectedPreview.sprite = defaultPreviewSprite;

            originalPreviewSprite = selectedPreview.sprite;
        }

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnChanged += Refresh;

        for (int i = 0; i < topSlotButtons.Length; i++)
        {
            int idx = i;
            if (!topSlotButtons[i]) continue;
            topSlotButtons[i].onClick.RemoveAllListeners();
            topSlotButtons[i].onClick.AddListener(() => Select(idx));
        }

        // 미리보기 클릭 시 선택 해제
        var previewBtn = selectedPreview ? selectedPreview.GetComponent<Button>() : null;
        if (previewBtn)
        {
            previewBtn.onClick.RemoveAllListeners();
            previewBtn.onClick.AddListener(ClearSelection);
        }

        Refresh();
    }

    public void Refresh()
    {
        var inv = PlayerInventory.Instance;
        if (!inv) return;

        // 1) 필터링
        if (useTypeFilter)
            _visible = inv.Items.Where(it => it && it.type == filterType).ToList();
        else
            _visible = inv.Items.Where(it => it).ToList();

        if (selectedIndex >= _visible.Count)
            selectedIndex = -1;

        for (int i = 0; i < topSlotImages.Length; i++)
        {
            var img = topSlotImages[i];
            var btn = (i < topSlotButtons.Length) ? topSlotButtons[i] : null;

            bool show = (i < _visible.Count);
            if (btn) btn.gameObject.SetActive(show);

            if (!img) continue;
            if (show)
                img.sprite = _visible[i].icon ? _visible[i].icon : emptySprite;
            else
                img.sprite = emptySprite;
        }

        // 3) 미리보기 갱신
        if (selectedPreview)
        {
            if (selectedIndex >= 0 &&
                selectedIndex < _visible.Count &&
                _visible[selectedIndex] &&
                _visible[selectedIndex].icon)
            {
                selectedPreview.sprite = _visible[selectedIndex].icon;
            }
            else
            {
                selectedPreview.sprite = originalPreviewSprite != null
                    ? originalPreviewSprite
                    : defaultPreviewSprite;
            }
        }
    }

    void Select(int index)
    {
        selectedIndex = index;
        Refresh();
    }

    // 선택 해제
    public void ClearSelection()
    {
        selectedIndex = -1;
        Refresh();
    }

    public ItemInfo GetSelected()
    {
        if (selectedIndex < 0 || selectedIndex >= _visible.Count) return null;
        return _visible[selectedIndex];
    }
}
