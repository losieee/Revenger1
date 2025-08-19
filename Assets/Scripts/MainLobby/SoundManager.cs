using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum SfxBus { Effect, ButtonClick }

public class SoundManager : MonoBehaviour
{
    public static SoundManager i;

    [System.Serializable]
    public struct SceneBgm
    {
        public string sceneName;                 // 씬 이름(정확히)
        public AudioClip clip;                   // 재생할 BGM
        [Range(0f, 1.5f)] public float baseVolume; // 클립별 기본 볼륨(상대 보정)
    }

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

        // 시작 시 저장값 반영
        LoadVolume();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // 게임 시작 직후, 현재 씬 이름으로 한 번 트리거
        TryPlaySceneBgm(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 이후 씬이 바뀔 때마다 자동으로 교체
        TryPlaySceneBgm(scene.name);
    }

    // 슬라이더(0~1) -> dB 로 변환해서 적용
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

    // 0~1 선형값을 dB 로 (0 방지용 epsilon)
    float LinearToDecibel(float v)
    {
        if (v <= 0.0001f) return -80f;
        return Mathf.Log10(v) * 20f;
    }

    // 효과음 재생
    public void PlaySFX(AudioClip clip, SfxBus bus = SfxBus.Effect, float scale = 1f)
    {
        if (clip == null) return;
        AudioSource src = (bus == SfxBus.ButtonClick) ? sfxButtonSource : sfxEffectSource;
        if (src != null) src.PlayOneShot(clip, scale);
    }

    public void TryPlaySceneBgm(string sceneName)
    {
        // 매핑 찾기
        var idx = sceneBgms.FindIndex(b => b.sceneName == sceneName);
        if (idx < 0) return;

        var target = sceneBgms[idx];
        if (bgmSource == null || target.clip == null) return;

        // 같은 곡이면 무시
        if (bgmSource.clip == target.clip && bgmSource.isPlaying) return;

        StopAllCoroutines();
        StartCoroutine(SwapBgmCoroutine(target.clip, target.baseVolume));
    }

    IEnumerator SwapBgmCoroutine(AudioClip nextClip, float baseVol)
    {
        float t = 0f;
        float startVol = bgmSource.volume;

        // fade out
        while (t < bgmFadeTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, t / bgmFadeTime);
            yield return null;
        }

        // swap
        bgmSource.clip = nextClip;
        bgmSource.loop = true;
        bgmSource.Play();

        // fade in (baseVol 적용)
        t = 0f;
        while (t < bgmFadeTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, baseVol, t / bgmFadeTime);
            yield return null;
        }
        bgmSource.volume = baseVol; // 최종 고정
    }
}
