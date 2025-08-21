using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Linq;

public enum SfxBus { Effect, ButtonClick }

public enum PlayerSfx { CrouchToggle, Jump, Land, ClimbStart, ClimbEnd }

public class SoundManager : MonoBehaviour
{
    public static SoundManager i;

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

    [Header("Assign in Inspector")]
    public AudioMixer audioMixer;

    [Header("SFX Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxEffectSource;
    public AudioSource sfxButtonSource;

    [Header("Scene BGMs")]
    public List<SceneBgm> sceneBgms;
    [Range(0.1f, 5f)] public float bgmFadeTime = 1f;

    const string MUSIC = "MusicVol";
    const string EFFECT = "EffectVol";

    void Awake()
    {
        if (i == null) { i = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        LoadVolume();

        // SFX 매핑 구성
        sfxMap = new Dictionary<PlayerSfx, SfxEntry>();
        foreach (var e in playerSfx)
            sfxMap[e.id] = e;
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Start() { TryPlaySceneBgm(SceneManager.GetActiveScene().name); }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { TryPlaySceneBgm(scene.name); }

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
        audioMixer.SetFloat(MUSIC, LinearToDecibel(m));
        audioMixer.SetFloat(EFFECT, LinearToDecibel(e));
    }
    float LinearToDecibel(float v) => (v <= 0.0001f) ? -80f : Mathf.Log10(v) * 20f;

    // 효과음 재생
    public void PlaySFX(AudioClip clip, SfxBus bus = SfxBus.Effect, float scale = 1f)
    {
        if (!clip) return;
        var src = (bus == SfxBus.ButtonClick) ? sfxButtonSource : sfxEffectSource;
        if (src) src.PlayOneShot(clip, scale);
    }

    // 이벤트 키 기반 재생
    public void PlaySFX(PlayerSfx id, SfxBus bus = SfxBus.Effect, float scale = 1f)
    {
        if (sfxMap == null) return;
        if (!sfxMap.TryGetValue(id, out var entry)) return;
        if (!entry.clip) return;
        PlaySFX(entry.clip, bus, entry.baseVolume * scale);
    }

    public void TryPlaySceneBgm(string sceneName)
    {
        var idx = sceneBgms.FindIndex(b => b.sceneName == sceneName);
        if (idx < 0) return;
        var target = sceneBgms[idx];
        if (!bgmSource || !target.clip) return;
        if (bgmSource.clip == target.clip && bgmSource.isPlaying) return;
        StopAllCoroutines();
        StartCoroutine(SwapBgmCoroutine(target.clip, target.baseVolume));
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
