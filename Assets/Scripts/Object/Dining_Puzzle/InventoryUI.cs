using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public Image[] topSlotImages;
    public Button[] topSlotButtons;
    public Sprite emptySprite;
    public Image selectedPreview;
    [SerializeField] private Sprite defaultPreviewSprite;

    private Sprite originalPreviewSprite;

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

        // 상단 슬롯들 갱신
        for (int i = 0; i < topSlotImages.Length; i++)
        {
            if (!topSlotImages[i]) continue;

            if (i < inv.Items.Count && inv.Items[i] && inv.Items[i].icon)
                topSlotImages[i].sprite = inv.Items[i].icon;
            else
                topSlotImages[i].sprite = emptySprite;
        }

        // 아래 미리보기 갱신
        if (selectedPreview)
        {
            if (selectedIndex >= 0 &&
                selectedIndex < inv.Items.Count &&
                inv.Items[selectedIndex] &&
                inv.Items[selectedIndex].icon)
            {
                selectedPreview.sprite = inv.Items[selectedIndex].icon;
            }
            else
            {
                // 선택 해제 또는 범위 밖 → 원래 기본 이미지로 복귀
                selectedPreview.sprite = originalPreviewSprite != null
                    ? originalPreviewSprite
                    : defaultPreviewSprite; // 혹시 백업도 null이면 기본으로
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
        var inv = PlayerInventory.Instance;
        if (!inv) return null;
        if (selectedIndex < 0 || selectedIndex >= inv.Items.Count) return null;
        return inv.Items[selectedIndex];
    }
}
