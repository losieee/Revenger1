using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlacementUI : MonoBehaviour
{
    public static PlacementUI i; // 싱글턴 간단히
    void Awake() 
    {
        i = this;
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton) cancelButton.onClick.AddListener(Close);
        if (removeButton) removeButton.onClick.AddListener(OnRemovePlaced);
    }

    [Header("Root / Buttons / Preview")]
    public GameObject panelRoot;        // Panel
    public Transform gridParent;        // TopBar
    public Button itemButtonPrefab;     // ItemButton_Template (자식에 RawImage 포함)
    public Image previewImage;          // Result/Image (RawImage)
    public Button confirmButton;        // Con
    public Button cancelButton;         // Cancel
    public Button removeButton;         // Back

    SlotPlate currentSlot;
    InventoryUI invUI;
    string requiredId;
    ItemInfo selected;

    readonly List<Button> spawned = new();

    public void Open(SlotPlate slot, string reqId)
    {
        currentSlot = slot;
        requiredId = reqId;
        selected = null;
        if (previewImage) previewImage.sprite = null;

        // 슬롯에 이미 올라가 있으면 Remove 버튼 활성
        if (removeButton) removeButton.interactable = currentSlot && currentSlot.HasPlaced;

        BuildGrid(PlayerInventory.Instance?.Items);
        ShowOverlay_NoPause();
    }

    void BuildGrid(IReadOnlyList<ItemInfo> items)
    {
        // 기존 버튼 정리
        foreach (var b in spawned) if (b) Destroy(b.gameObject);
        spawned.Clear();

        if (items != null)
        {
            foreach (var item in items)
            {
                var btn = Instantiate(itemButtonPrefab, gridParent);
                spawned.Add(btn);

                var img = btn.GetComponentInChildren<Image>();
                if (img) img.sprite = item.icon ? item.icon : null;

                btn.onClick.AddListener(() =>
                {
                    selected = item;
                    if (previewImage) previewImage.sprite = item.icon ? item.icon : null;
                    if (confirmButton) confirmButton.interactable = true; // 선택되면 가능
                });
            }
        }

        // 아이템 없으면 Confirm 비활성
        if (confirmButton) confirmButton.interactable = (selected != null);
    }

    void OnConfirm()
    {
        if (currentSlot == null || selected == null) return;

        // 이미 채워진 슬롯이면 배치 금지 + UI 유지
        if (currentSlot.HasPlaced)
        {
            Debug.Log("[PlacementUI] 이미 채워진 슬롯입니다. 먼저 되돌리세요.");
            if (removeButton) removeButton.interactable = true; // 되돌리기 유도
            return; // 닫지 않음
        }

        if (PlayerInventory.Instance.TryTake(selected, out var taken))
            currentSlot.Place(taken);

        Close();
    }

    // 올려둔거 되돌리기
    void OnRemovePlaced()
    {
        if (currentSlot == null || !currentSlot.HasPlaced) return;
        currentSlot.TakeBack();
        // 그리드 갱신 (돌아온 아이템 보이도록)
        BuildGrid(PlayerInventory.Instance ? PlayerInventory.Instance.Items : null);
        if (removeButton) removeButton.interactable = currentSlot.HasPlaced;
    }

    void Close()
    {
        // UI 닫기
        HideOverlay_NoPause();
        currentSlot = null; requiredId = null; selected = null;
        if (previewImage) previewImage.sprite = null;
    }

    // ====== 보이는/숨김 유틸 ======
    void ShowOverlay_NoPause()
    {
        if (!panelRoot) { Debug.LogError("[PlacementUI] Panel Root 미지정"); return; }

        // 1) 반드시 EventSystem 존재 보장
        if (!EventSystem.current)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            // (선택) 입력 지연 이슈 회피용 옵션 조절 가능
            var sim = es.GetComponent<StandaloneInputModule>();
            sim.forceModuleActive = true;
        }

        // 2) Canvas & GraphicRaycaster 보장 (월드/스크린 상관없이 필요)
        var canvas = panelRoot.GetComponentInParent<Canvas>();
        if (!canvas) Debug.LogWarning("[PlacementUI] Canvas가 없습니다. Canvas 하위에 Panel을 두세요.");
        else if (!canvas.GetComponent<GraphicRaycaster>())
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        // 3) 패널 활성 + 클릭 가능 설정
        var cg = panelRoot.GetComponent<CanvasGroup>() ?? panelRoot.AddComponent<CanvasGroup>();
        panelRoot.SetActive(true);
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;

        // 4) 커서/입력
        PlayerMov.LockControls(showCursor: true);
        Time.timeScale = 1f;

        // 5) 첫 포커스(패드/키보드 내비게이션도 바로 먹게)
        var firstButton = gridParent ? gridParent.GetComponentsInChildren<Button>(true).FirstOrDefault() : null;
        EventSystem.current.SetSelectedGameObject(null);
        if (firstButton) EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
    }

    void HideOverlay_NoPause()
    {
        if (!panelRoot) return;
        var cg = panelRoot.GetComponent<CanvasGroup>();
        if (cg) { cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; }
        panelRoot.SetActive(false);

        PlayerMov.UnlockControls(hideCursor: true);
    }

    public void CloseIfOpenFor(SlotPlate[] groupSlots)
    {
        if (currentSlot == null) return;
        if (groupSlots != null && groupSlots.Contains(currentSlot))
            Close(); // 기존 Close() 호출
    }
}
