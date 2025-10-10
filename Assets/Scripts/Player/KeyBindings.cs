using UnityEngine;
using System;

public enum GameAction { Forward, Back, Left, Right, Run, MiniMap, Crouch, Climb, Interaction, Crawl, Attack }

public static class KeyBindings
{
    public static event System.Action OnChanged;

    // 기본값
    static readonly KeyCode DEF_FORWARD = KeyCode.W;
    static readonly KeyCode DEF_BACK = KeyCode.S;
    static readonly KeyCode DEF_LEFT = KeyCode.A;
    static readonly KeyCode DEF_RIGHT = KeyCode.D;
    static readonly KeyCode DEF_RUN = KeyCode.LeftShift;
    static readonly KeyCode DEF_MINIMAP = KeyCode.Tab;
    static readonly KeyCode DEF_CROUCH = KeyCode.C;
    static readonly KeyCode DEF_CLIMB = KeyCode.Space;
    static readonly KeyCode DEF_Interaction= KeyCode.E;
    static readonly KeyCode DEF_Crawl = KeyCode.Z;
    static readonly KeyCode DEF_ATTACK = KeyCode.Mouse0;

    static KeyCode _forward = DEF_FORWARD;
    static KeyCode _back = DEF_BACK;
    static KeyCode _left = DEF_LEFT;
    static KeyCode _right = DEF_RIGHT;
    static KeyCode _run = DEF_RUN;
    static KeyCode _MiniMap = DEF_MINIMAP;
    static KeyCode _crouch = DEF_CROUCH;
    static KeyCode _climb = DEF_CLIMB;
    static KeyCode _interaction = DEF_Interaction;
    static KeyCode _crawl = DEF_Crawl;
    static KeyCode _attack = DEF_ATTACK;

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
        _MiniMap = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.MiniMap), (int)DEF_MINIMAP);
        _crouch = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Crouch), (int)DEF_CROUCH);
        _climb = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Climb), (int)DEF_CLIMB);
        _interaction = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Interaction), (int)DEF_Interaction);
        _crawl = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Crawl), (int)DEF_Crawl);
        _attack = (KeyCode)PlayerPrefs.GetInt(PF_PREFIX + nameof(GameAction.Attack), (int)DEF_ATTACK);
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
            case GameAction.MiniMap: return _MiniMap;
            case GameAction.Crouch: return _crouch;
            case GameAction.Climb: return _climb;
            case GameAction.Interaction: return _interaction;
            case GameAction.Crawl: return _crawl;
            case GameAction.Attack: return _attack;
        }
        return KeyCode.None;
    }

    static bool TryGetMouseIndex(KeyCode kc, out int index)
    {
        if (kc == KeyCode.Mouse0) { index = 0; return true; }
        if (kc == KeyCode.Mouse1) { index = 1; return true; }
        index = -1; return false;
    }
    static bool IsAllowedKey(KeyCode code)
    {
        // 마우스 계열이면 Left/Right만 허용
        if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6)
            return code == KeyCode.Mouse0 || code == KeyCode.Mouse1;
        return code != KeyCode.None;
    }

    // 충돌 시 스왑(기본 true)
    public static bool TrySet(GameAction a, KeyCode code, bool swapIfUsed = true)
    {
        EnsureLoaded();
        if (!IsAllowedKey(code)) return false;
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
            case GameAction.MiniMap: _MiniMap = code; break;
            case GameAction.Crouch: _crouch = code; break;
            case GameAction.Climb: _climb = code; break;
            case GameAction.Interaction: _interaction = code; break;
            case GameAction.Crawl: _crawl = code; break;
            case GameAction.Attack: _attack = code; break;
        }
    }

    static bool TryFindByKey(KeyCode code, out GameAction action)
    {
        if (_forward == code) { action = GameAction.Forward; return true; }
        if (_back == code) { action = GameAction.Back; return true; }
        if (_left == code) { action = GameAction.Left; return true; }
        if (_right == code) { action = GameAction.Right; return true; }
        if (_run == code) { action = GameAction.Run; return true; }
        if (_MiniMap == code) { action = GameAction.MiniMap; return true; }
        if (_crouch == code) { action = GameAction.Crouch; return true; }
        if (_climb == code) { action = GameAction.Climb; return true; }
        if (_interaction == code) { action = GameAction.Interaction; return true; }
        if (_crawl == code) { action = GameAction.Crawl; return true; }
        if (_attack == code) { action = GameAction.Attack; return true; }
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
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.MiniMap), (int)_MiniMap);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Crouch), (int)_crouch);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Climb), (int)_climb);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Interaction), (int)_interaction);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Crawl), (int)_crawl);
        PlayerPrefs.SetInt(PF_PREFIX + nameof(GameAction.Attack), (int)_attack);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void ResetToDefaults()
    {
        EnsureLoaded();
        _forward = DEF_FORWARD; _back = DEF_BACK; _left = DEF_LEFT; _right = DEF_RIGHT;
        _run = DEF_RUN; _crouch = DEF_CROUCH; _climb = DEF_CLIMB; _MiniMap = DEF_MINIMAP;
        _interaction = DEF_Interaction; _crawl = DEF_Crawl;
        _attack = DEF_ATTACK;
        SaveAll();
    }

    public static bool GetKey(GameAction a)
    {
        EnsureLoaded();
        var kc = Get(a);
        if (TryGetMouseIndex(kc, out int mi)) return Input.GetMouseButton(mi);
        return Input.GetKey(kc);
    }
    public static bool GetKeyDown(GameAction a)
    {
        EnsureLoaded();
        var kc = Get(a);
        if (TryGetMouseIndex(kc, out int mi)) return Input.GetMouseButtonDown(mi);
        return Input.GetKeyDown(kc);
    }
    public static bool GetKeyUp(GameAction a)
    {
        EnsureLoaded();
        var kc = Get(a);
        if (TryGetMouseIndex(kc, out int mi)) return Input.GetMouseButtonUp(mi);
        return Input.GetKeyUp(kc);
    }

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
            case KeyCode.Mouse0: return "L-Mouse";
            case KeyCode.Mouse1: return "R-Mouse";
            default: return code.ToString();
        }
    }
}
