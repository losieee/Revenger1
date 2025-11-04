using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlacementUI : MonoBehaviour
{
    public static PlacementUI i; // 싱글턴 간단히

    [Header("Root / Buttons / Preview")]
    public GameObject panelRoot;        // Panel
    public Transform gridParent;        // TopBar
    public Button itemButtonPrefab;     // ItemButton_Template
    public Image previewImage;          // Result/Image
    public Sprite previewOriginImage;   // Result BackGround
    public Button confirmButton;        // Con
    public Button cancelButton;         // Cancel
    public Button removeButton;         // Back

    SlotPlate currentSlot;
    InventoryUI invUI;
    string requiredId;
    ItemInfo selected;
    string requiredTag = "Dining_Item";

    readonly List<Button> spawned = new();

    void OnEnable() => WireButtons();           // 씬 복귀 시 보강
    void OnDisable() => UnwireButtons();        // 중복 방지

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; }
        i = this;
        DontDestroyOnLoad(gameObject);

        // 필수 레퍼런스 보정
        if (itemButtonPrefab == null)
        {
            itemButtonPrefab = Resources.Load<Button>("Prefabs/ItemButton_Template"); // 경로 예시
            if (itemButtonPrefab == null)
                Debug.LogError("[PlacementUI] itemButtonPrefab null (에셋 지정/경로 확인)");
        }

        WireButtons();
    }

    void WireButtons()
    {
        if (confirmButton)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirm);
        }
        if (cancelButton)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(Close);
        }
        if (removeButton)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(OnRemovePlaced);
        }
    }

    void UnwireButtons()
    {
        if (confirmButton) confirmButton.onClick.RemoveAllListeners();
        if (cancelButton) cancelButton.onClick.RemoveAllListeners();
        if (removeButton) removeButton.onClick.RemoveAllListeners();
    }

    public void Open(SlotPlate slot, string reqId)
    {
        currentSlot = slot;
        requiredId = reqId;
        selected = null;
        if (previewImage) previewImage.sprite = previewOriginImage;

        // 슬롯에 이미 올라가 있으면 Remove 버튼 활성
        if (removeButton) removeButton.interactable = currentSlot && currentSlot.HasPlaced;

        BuildGrid(PlayerInventory.Instance?.Items);
        ShowOverlay_NoPause();
    }

    void BuildGrid(IReadOnlyList<ItemInfo> items)
    {
        foreach (var b in spawned) if (b) Destroy(b.gameObject);
        spawned.Clear();

        if (items != null)
        {
            // Generic만 필터 → 리스트로 캐시
            var filtered = items
                .Where(it => it != null && it.type == ItemInfo.ItemType.Generic)
                .ToList();

            // filtered를 순회
            foreach (var item in filtered)
            {
                var btn = Instantiate(itemButtonPrefab, gridParent);
                spawned.Add(btn);

                var img = btn.GetComponentInChildren<Image>();
                if (img) img.sprite = item.icon ? item.icon : null;

                btn.onClick.AddListener(() =>
                {
                    selected = item;
                    if (previewImage) previewImage.sprite = item.icon ? item.icon : previewOriginImage;
                    if (confirmButton) confirmButton.interactable = true;
                });
            }
        }

        if (confirmButton) confirmButton.interactable = (selected != null);
    }


    void OnConfirm()
    {
        if (currentSlot == null || selected == null) return;

        // 이미 채워진 슬롯이면 배치 금지 + UI 유지
        if (currentSlot.HasPlaced)
        {
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
        if (previewImage) previewImage.sprite = previewOriginImage;
    }

    // ====== 보이는/숨김 유틸 ======
    void ShowOverlay_NoPause()
    {
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
        
        if (!canvas.GetComponent<GraphicRaycaster>())
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
