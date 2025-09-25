using UnityEngine;
using System;

public enum GameAction { Forward, Back, Left, Right, Run, Crouch, Climb }

public static class KeyBindings
{
    public static event Action OnChanged;

    // 기본값
    static readonly KeyCode DEF_FORWARD = KeyCode.W;
    static readonly KeyCode DEF_BACK = KeyCode.S;
    static readonly KeyCode DEF_LEFT = KeyCode.A;
    static readonly KeyCode DEF_RIGHT = KeyCode.D;
    static readonly KeyCode DEF_RUN = KeyCode.LeftShift;
    static readonly KeyCode DEF_CROUCH = KeyCode.C;
    static readonly KeyCode DEF_CLIMB = KeyCode.Space;

    static KeyCode _forward = DEF_FORWARD;
    static KeyCode _back = DEF_BACK;
    static KeyCode _left = DEF_LEFT;
    static KeyCode _right = DEF_RIGHT;
    static KeyCode _run = DEF_RUN;
    static KeyCode _crouch = DEF_CROUCH;
    static KeyCode _climb = DEF_CLIMB;

    const string PF_PREFIX = "KEYBIND_";
    static bool _loaded = false;

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _forward = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Forward), (int)DEF_FORWARD);
        _back = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Back), (int)DEF_BACK);
        _left = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Left), (int)DEF_LEFT);
        _right = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Right), (int)DEF_RIGHT);
        _run = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Run), (int)DEF_RUN);
        _crouch = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Crouch), (int)DEF_CROUCH);
        _climb = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Climb), (int)DEF_CLIMB);
        _loaded = true;
    }

    public static KeyCode Get(GameAction a)
    {
        EnsureLoaded();
        switch (a)
        {
            case GameAction.Forward: return _forward;
            case GameAction.Back: return _back;
            case GameAction.Left: return _left;
            case GameAction.Right: return _right;
            case GameAction.Run: return _run;
            case GameAction.Crouch: return _crouch;
            case GameAction.Climb: return _climb;
        }
        return KeyCode.None;
    }

    // 충돌 시 스왑(기본 true)
    public static bool TrySet(GameAction a, KeyCode code, bool swapIfUsed = true)
    {
        EnsureLoaded();
        if (code == KeyCode.None) return false;
        if (Get(a) == code) return true;

        if (TryFindByKey(code, out GameAction usedBy))
        {
            if (usedBy == a) return true;
            if (!swapIfUsed) return false;

            KeyCode temp = Get(a);
            Assign(a, code);
            Assign(usedBy, temp);
            SaveAll();
            return true;
        }

        Assign(a, code);
        SaveAll();
        return true;
    }

    public static void Set(GameAction a, KeyCode code) => TrySet(a, code, true);

    static void Assign(GameAction a, KeyCode code)
    {
        switch (a)
        {
            case GameAction.Forward: _forward = code; break;
            case GameAction.Back: _back = code; break;
            case GameAction.Left: _left = code; break;
            case GameAction.Right: _right = code; break;
            case GameAction.Run: _run = code; break;
            case GameAction.Crouch: _crouch = code; break;
            case GameAction.Climb: _climb = code; break;
        }
    }

    static bool TryFindByKey(KeyCode code, out GameAction action)
    {
        if (_forward == code) { action = GameAction.Forward; return true; }
        if (_back == code) { action = GameAction.Back; return true; }
        if (_left == code) { action = GameAction.Left; return true; }
        if (_right == code) { action = GameAction.Right; return true; }
        if (_run == code) { action = GameAction.Run; return true; }
        if (_crouch == code) { action = GameAction.Crouch; return true; }
        if (_climb == code) { action = GameAction.Climb; return true; }
        action = default;
        return false;
    }

    static void SaveAll()
    {
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Forward), (int)_forward);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Back), (int)_back);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Left), (int)_left);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Right), (int)_right);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Run), (int)_run);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Crouch), (int)_crouch);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Climb), (int)_climb);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void ResetToDefaults()
    {
        EnsureLoaded();
        _forward = DEF_FORWARD; _back = DEF_BACK; _left = DEF_LEFT; _right = DEF_RIGHT;
        _run = DEF_RUN; _crouch = DEF_CROUCH; _climb = DEF_CLIMB;
        SaveAll();
    }

    public static bool GetKey(GameAction a) => Input.GetKey(Get(a));
    public static bool GetKeyDown(GameAction a) => Input.GetKeyDown(Get(a));

    public static float GetAxisHorizontal()
    {
        EnsureLoaded();
        float v = 0f;
        if (Input.GetKey(_left)) v -= 1f;
        if (Input.GetKey(_right)) v += 1f;
        return v;
    }
    public static float GetAxisVertical()
    {
        EnsureLoaded();
        float v = 0f;
        if (Input.GetKey(_back)) v -= 1f;
        if (Input.GetKey(_forward)) v += 1f;
        return v;
    }

    public static string ToDisplay(KeyCode code)
    {
        switch (code)
        {
            case KeyCode.LeftControl: return "L-Ctrl";
            case KeyCode.RightControl: return "R-Ctrl";
            case KeyCode.LeftShift: return "L-Shift";
            case KeyCode.RightShift: return "R-Shift";
            case KeyCode.LeftAlt: return "L-Alt";
            case KeyCode.RightAlt: return "R-Alt";
            default: return code.ToString();
        }
    }
}
