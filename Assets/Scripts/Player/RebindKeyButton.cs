using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RebindKeyButton : MonoBehaviour
{
    [SerializeField] GameAction action;        // 액션 지정 (Forward/Back/Left/Right/Run/Crouch/Climb)
    [SerializeField] TMP_Text label;
    [SerializeField] Button button;
    [Header("Behavior")]
    [SerializeField] bool swapOnConflict = true;
    [SerializeField] string waitingText = "Press any key...";
    [SerializeField] string conflictText = "Already in use!";

    bool listening = false;
    Coroutine listenCo;

    void Reset() { button = GetComponent<Button>(); label = GetComponentInChildren<TMP_Text>(); }
    void OnEnable() { KeyBindings.OnChanged += RefreshLabel; RefreshLabel(); }
    void OnDisable() { KeyBindings.OnChanged -= RefreshLabel; }

    void Start() { if (button) button.onClick.AddListener(BeginListen); RefreshLabel(); }

    public void BeginListen()
    {
        if (listening) return;
        if (label) label.text = waitingText;
        listening = true;
        if (listenCo != null) StopCoroutine(listenCo);
        listenCo = StartCoroutine(ListenRoutine());
    }

    IEnumerator ListenRoutine()
    {
        yield return null; // 클릭 입력 흘리기
        while (listening)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { listening = false; break; }

            KeyCode code = DetectPressedKey();
            if (code != KeyCode.None && !IsMouse(code))
            {
                bool ok = KeyBindings.TrySet(action, code, swapIfUsed: swapOnConflict);
                if (!ok) { if (label) label.text = conflictText; yield return new WaitForSecondsRealtime(0.8f); }
                listening = false;
            }
            yield return null;
        }
        RefreshLabel();
        listenCo = null;
    }

    void RefreshLabel() { if (label) label.text = KeyBindings.ToDisplay(KeyBindings.Get(action)); }
    static bool IsMouse(KeyCode c) => c >= KeyCode.Mouse0 && c <= KeyCode.Mouse6;
    static KeyCode DetectPressedKey() { foreach (KeyCode c in System.Enum.GetValues(typeof(KeyCode))) if (c != KeyCode.None && Input.GetKeyDown(c)) return c; return KeyCode.None; }
}
