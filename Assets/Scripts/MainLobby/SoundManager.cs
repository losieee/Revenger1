using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Linq;

public enum SfxBus { Effect, ButtonClick }

public enum PlayerSfx
{
    CrouchToggle, Jump, Land, ClimbStart, ClimbEnd,
    LeftWalkIndoor, RightWalkIndoor, LeftRunIndoor, RightRunIndoor,
    LeftWalkOutdoor, RightWalkOutdoor, LeftRunOutdoor, RightRunOutdoor,
    ChestOpen, ChestClose, WeaponDraw, AttackCrowbar, AttackGun, AttackBat,
    LieDown, LieMoving, GetKey, SewerWalkLeft, SewerWalkRight, SewerRunLeft, SewerRunRight,
    PickGun, PickCrowbar, PickBat, FailDiningPuzzle, FailGuestPuzzle, DropGuestPuzzle,
    Ladder, BodyguardAttack, BodyguardQuestionSound, ButtonClick
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager i;

    Coroutine _bgmSwap;

    [System.Serializable]
    public struct SceneBgm
    {
        public string sceneName;
        public AudioClip clip;
        [Range(0f, 1.5f)] public float baseVolume;
    }

    [System.Serializable]
    public struct SfxEntry
    {
        public PlayerSfx id;                  // 어떤 상황의 효과음인지
        public AudioClip clip;                // 실제 클립
        [Range(0f, 2f)] public float baseVolume; // 클립 기본 볼륨
    }

    [Header("Player SFX Library")]
    public List<SfxEntry> playerSfx = new List<SfxEntry>();
    private Dictionary<PlayerSfx, SfxEntry> sfxMap;  // 매핑 테이블
    private readonly Dictionary<PlayerSfx, AudioSource> _loopSources = new();

    [Header("Assign in Inspector")]
    public AudioMixer audioMixer;

    [Header("SFX Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxEffectSource;
    public AudioSource sfxButtonSource;

    [Header("Scene BGMs")]
    public List<SceneBgm> sceneBgms;
    [Range(0.1f, 5f)] public float bgmFadeTime = 1f;

    const string MASTER = "MasterVol";
    const string MUSIC = "MusicVol";
    const string EFFECT = "EffectVol";

    public AudioMixerGroup effectGroup;
    public AudioMixerGroup EffectGroup => effectGroup;

    void Awake()
    {
        if (i == null) { i = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        LoadVolume();

        // SFX 매핑 구성
        sfxMap = new Dictionary<PlayerSfx, SfxEntry>();
        foreach (var e in playerSfx)
            sfxMap[e.id] = e;

        ReapplyVolumesAndUnpause();

        if (sfxEffectSource && effectGroup) sfxEffectSource.outputAudioMixerGroup = effectGroup;
        if (sfxButtonSource && effectGroup) sfxButtonSource.outputAudioMixerGroup = effectGroup;

        foreach (var e in playerSfx)
        {
            if (e.clip && e.clip.loadState != AudioDataLoadState.Loaded)
                e.clip.LoadAudioData();
        }
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Start()
    {
        // 시작 시에도 1프레임 후 오디오 그래프 안정화 뒤 BGM 적용
        StartCoroutine(CoInitAudioThenPlay(SceneManager.GetActiveScene().name));
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 장면 바뀔 때마다: 언파즈 -> 볼륨 재적용 -> 1프레임 대기 -> BGM 교체
        StartCoroutine(CoInitAudioThenPlay(scene.name));
    }


    // 장면 전환 직후 오디오 초기화 + BGM 적용을 순서대로 보장
    IEnumerator CoInitAudioThenPlay(string sceneName)
    {
        ReapplyVolumesAndUnpause();

        yield return null;
        TryPlaySceneBgm(sceneName);
    }

    // 공통: 리스너 언파즈 + 저장된 볼륨 다시 적용 + 소스들 강제 unmute
    void ReapplyVolumesAndUnpause()
    {
        AudioListener.pause = false;                // 혹시 남아있을 수 있는 전역 일시정지 해제
        Time.timeScale = Mathf.Max(Time.timeScale, 0f); // 음수 방지

        LoadVolume();

        if (bgmSource) { bgmSource.mute = false; }
        if (sfxEffectSource) { sfxEffectSource.mute = false; }
        if (sfxButtonSource) { sfxButtonSource.mute = false; }
    }

    public void SetMasterVolume(float v)
    {
        audioMixer.SetFloat(MASTER, LinearToDecibel(v));
        PlayerPrefs.SetFloat(MASTER, v);
    }
    public void SetMusicVolume(float v)
    {
        audioMixer.SetFloat(MUSIC, LinearToDecibel(v));
        PlayerPrefs.SetFloat(MUSIC, v);
    }
    public void SetEffectVolume(float v)
    {
        audioMixer.SetFloat(EFFECT, LinearToDecibel(v));
        PlayerPrefs.SetFloat(EFFECT, v);
    }
    public void LoadVolume()
    {
        float m = PlayerPrefs.GetFloat(MUSIC, 1f);
        float e = PlayerPrefs.GetFloat(EFFECT, 1f);
        float master = PlayerPrefs.GetFloat(MASTER, 1f);

        audioMixer.SetFloat(MUSIC, LinearToDecibel(m));
        audioMixer.SetFloat(EFFECT, LinearToDecibel(e));
        audioMixer.SetFloat(MASTER, LinearToDecibel(master));
    }
    float LinearToDecibel(float v) => (v <= 0.0001f) ? -80f : Mathf.Log10(v) * 20f;

    // 효과음 재생
    public void PlaySFX(AudioClip clip, SfxBus bus = SfxBus.Effect, float scale = 1f, float pitch = 1f)
    {
        if (!clip) return;

        var src = (bus == SfxBus.ButtonClick) ? sfxButtonSource : sfxEffectSource;
        if (!src) return;

        scale = Mathf.Clamp01(scale);
        if (scale < 0.001f) scale = 0.001f;
        pitch = Mathf.Clamp(pitch, 0.1f, 3f);

        // 로딩 보장
        if (clip.loadState != AudioDataLoadState.Loaded)
            clip.LoadAudioData();

        float oldPitch = src.pitch;
        src.pitch = pitch;
        src.PlayOneShot(clip, scale);
        src.pitch = oldPitch;
    }

    // 이벤트 키 기반 재생
    public void PlaySFX(PlayerSfx id, SfxBus bus = SfxBus.Effect, float scale = 1f, float pitch = 1f)
    {
        if (sfxMap == null) return;
        if (!sfxMap.TryGetValue(id, out var entry)) return;
        if (!entry.clip) return;
        PlaySFX(entry.clip, bus, entry.baseVolume * scale, pitch);
    }

    public void PlayLoopSFX(PlayerSfx id, SfxBus bus = SfxBus.Effect, float scale = 1f, bool restartIfPlaying = false)
    {
        if (sfxMap == null) return;
        if (!sfxMap.TryGetValue(id, out var entry)) return;
        if (!entry.clip) return;

        if (_loopSources.TryGetValue(id, out var src) && src)
        {
            if (src.isPlaying && !restartIfPlaying) return;
            src.clip = entry.clip;
            src.volume = entry.baseVolume * scale;
            src.loop = true;
            src.Play();
            return;
        }

        // 새 소스 생성
        var go = new GameObject($"SFX_Loop_{id}");
        go.transform.SetParent(transform, false);
        var loopSrc = go.AddComponent<AudioSource>();
        loopSrc.playOnAwake = false;
        loopSrc.loop = true;
        loopSrc.spatialBlend = 0f;

        loopSrc.outputAudioMixerGroup = (bus == SfxBus.ButtonClick) ? sfxButtonSource?.outputAudioMixerGroup : effectGroup;

        loopSrc.clip = entry.clip;
        loopSrc.volume = entry.baseVolume * scale;
        loopSrc.Play();

        _loopSources[id] = loopSrc;
    }

    public void StopSFX(PlayerSfx id, float fadeOut = 0f)
    {
        if (!_loopSources.TryGetValue(id, out var src) || !src) return;
        if (!src.isPlaying) return;

        if (fadeOut <= 0f)
        {
            src.Stop();
            return;
        }
        StartCoroutine(CoFadeOutAndStop(src, fadeOut));
    }

    public static void Play2D(AudioClip clip, SfxBus bus = SfxBus.Effect, float scale = 1f, float pitch = 1f)
    {
        if (i == null) return;

        i.PlaySFX(clip, bus, scale, pitch);
    }

    IEnumerator CoFadeOutAndStop(AudioSource src, float dur)
    {
        float t = 0f, v0 = src.volume;
        dur = Mathf.Max(0.01f, dur);
        while (t < 1f && src)
        {
            t += Time.unscaledDeltaTime / dur;
            src.volume = Mathf.Lerp(v0, 0f, t);
            yield return null;
        }
        if (src)
        {
            src.Stop();
            src.volume = v0;
        }
    }

    public bool IsSFXPlaying(PlayerSfx id)
    {
        return _loopSources.TryGetValue(id, out var src) && src && src.isPlaying;
    }

    public void TryPlaySceneBgm(string sceneName)
    {
        var idx = sceneBgms.FindIndex(b => b.sceneName == sceneName);
        if (idx < 0) return;
        var target = sceneBgms[idx];
        if (!bgmSource || !target.clip) return;
        if (bgmSource.clip == target.clip && bgmSource.isPlaying) return;

        if (_bgmSwap != null) StopCoroutine(_bgmSwap);
        _bgmSwap = StartCoroutine(SwapBgmCoroutine(target.clip, target.baseVolume));
    }

    IEnumerator SwapBgmCoroutine(AudioClip nextClip, float baseVol)
    {
        float t = 0f, startVol = bgmSource.volume;
        while (t < bgmFadeTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, t / bgmFadeTime);
            yield return null;
        }
        bgmSource.clip = nextClip; bgmSource.loop = true; bgmSource.Play();
        t = 0f;
        while (t < bgmFadeTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, baseVol, t / bgmFadeTime);
            yield return null;
        }
        bgmSource.volume = baseVol;
    }

    public float GetMasterVolume01()
    {
        if (!audioMixer.GetFloat(MASTER, out var db)) return 1f;
        return db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);
    }
    public float GetMusicVolume01()
    {
        if (!audioMixer.GetFloat(MUSIC, out var db)) return 1f;
        return db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);
    }
    public float GetEffectVolume01()
    {
        if (!audioMixer.GetFloat(EFFECT, out var db)) return 1f;
        return db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);
    }

    float DecibelToLinear(float db)
    {
        if (db <= -80f) return 0f;
        return Mathf.Pow(10f, db / 20f);
    }
}
