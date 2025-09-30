using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RebindKeyButton : MonoBehaviour
{
    [SerializeField] GameAction action;        // 액션 지정 (Forward/Back/Left/Right/Run/Crouch/Climb)
    [SerializeField] TMP_Text label;
    [SerializeField] Button button;
    [Header("Behavior")]
    [SerializeField] string waitingText = "Press any key..\n(ESC to cancel)";
    [SerializeField] string conflictText = "Already in use!";

    bool listening = false;
    Coroutine listenCo;

    static readonly HashSet<RebindKeyButton> _active = new HashSet<RebindKeyButton>();
    public static bool IsAnyListening => _active.Count > 0;
    public static void CancelAll()
    {
        // 복사본으로 돌려야 순회 중 변경 안전
        foreach (var b in new List<RebindKeyButton>(_active))
            b.CancelListening();
    }


    void Reset() { button = GetComponent<Button>(); label = GetComponentInChildren<TMP_Text>(); }
    void OnEnable() { KeyBindings.OnChanged += RefreshLabel; RefreshLabel(); }
    void OnDisable() { KeyBindings.OnChanged -= RefreshLabel; CancelListening(); }

    void Start() { if (button) button.onClick.AddListener(BeginListen); RefreshLabel(); }

    public void BeginListen()
    {
        if (listening) return;
        listening = true;
        _active.Add(this);

        if (button)
        {
            button.interactable = false;
            button.enabled = false;
        }
        if (label) label.text = waitingText;
        if (listenCo != null) StopCoroutine(listenCo);
        listenCo = StartCoroutine(ListenRoutine());
    }

    IEnumerator ListenRoutine()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        while (listening)
        {
            // ESC로 취소 (패널 닫히기 전에 우리가 먼저 처리)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelListening();
                yield break;
            }

            if (Input.GetMouseButtonDown(0)) { TryApply(KeyCode.Mouse0); yield break; }
            if (Input.GetMouseButtonDown(1)) { TryApply(KeyCode.Mouse1); yield break; }

            foreach (KeyCode c in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (c == KeyCode.None) continue;
                if (c >= KeyCode.Mouse0 && c <= KeyCode.Mouse6) continue; // Mouse2~6 무시
                if (Input.GetKeyDown(c))
                {
                    TryApply(c);
                    yield break;
                }
            }
            yield return null;
        }
    }

    void TryApply(KeyCode code)
    {
        bool ok = KeyBindings.TrySet(action, code, swapIfUsed: true);
        if (!ok && label)
        {
            // Mouse2~6 등 허용 안 되는 키거나 충돌 처리 실패 시
            label.text = conflictText;
        }
        CancelListening();
    }

    void CancelListening()
    {
        if (!listening)
        {
            _active.Remove(this);
            listenCo = null;
            return;
        }

        listening = false;
        _active.Remove(this);

        if (listenCo != null) { StopCoroutine(listenCo); listenCo = null; }

        RefreshLabel();
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        StartCoroutine(ReenableButtonAfterRelease());
    }

    IEnumerator ReenableButtonAfterRelease()
    {
        // 모든 마우스 버튼이 올라갈 때까지 대기
        while (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            yield return null;

        // 안전하게 한 프레임 더 대기
        yield return new WaitForEndOfFrame();

        if (button)
        {
            button.enabled = true;
            button.interactable = true;
        }
    }

    void RefreshLabel()
    {
        if (label) label.text = KeyBindings.ToDisplay(KeyBindings.Get(action));
    }

    static bool IsMouse(KeyCode c) => c >= KeyCode.Mouse0 && c <= KeyCode.Mouse6;
    static KeyCode DetectPressedKey()
    {
        foreach (KeyCode c in System.Enum.GetValues(typeof(KeyCode)))
            if (c != KeyCode.None && Input.GetKeyDown(c)) return c;
        return KeyCode.None;
    }
}
