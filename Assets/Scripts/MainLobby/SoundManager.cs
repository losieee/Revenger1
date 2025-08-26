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
        public PlayerSfx id;                  // � ��Ȳ�� ȿ��������
        public AudioClip clip;                // ���� Ŭ��
        [Range(0f, 2f)] public float baseVolume; // Ŭ�� �⺻ ����
    }

    [Header("Player SFX Library")]
    public List<SfxEntry> playerSfx = new List<SfxEntry>();
    private Dictionary<PlayerSfx, SfxEntry> sfxMap;  // ���� ���̺�

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

    public AudioMixerGroup effectGroup;
    public AudioMixerGroup EffectGroup => effectGroup;

    void Awake()
    {
        if (i == null) { i = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        LoadVolume();

        // SFX ���� ����
        sfxMap = new Dictionary<PlayerSfx, SfxEntry>();
        foreach (var e in playerSfx)
            sfxMap[e.id] = e;

        ReapplyVolumesAndUnpause();

        if (sfxEffectSource && effectGroup) sfxEffectSource.outputAudioMixerGroup = effectGroup;
        if (sfxButtonSource && effectGroup) sfxButtonSource.outputAudioMixerGroup = effectGroup;
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Start()
    {
        // ���� �ÿ��� 1������ �� ����� �׷��� ����ȭ �� BGM ����
        StartCoroutine(CoInitAudioThenPlay(SceneManager.GetActiveScene().name));
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ��� �ٲ� ������: ������ -> ���� ������ -> 1������ ��� -> BGM ��ü
        StartCoroutine(CoInitAudioThenPlay(scene.name));
    }


    // ��� ��ȯ ���� ����� �ʱ�ȭ + BGM ������ ������� ����
    IEnumerator CoInitAudioThenPlay(string sceneName)
    {
        ReapplyVolumesAndUnpause();

        yield return null;
        TryPlaySceneBgm(sceneName);
    }

    // ����: ������ ������ + ����� ���� �ٽ� ���� + �ҽ��� ���� unmute
    void ReapplyVolumesAndUnpause()
    {
        AudioListener.pause = false;                // Ȥ�� �������� �� �ִ� ���� �Ͻ����� ����
        Time.timeScale = Mathf.Max(Time.timeScale, 0f); // ���� ����

        LoadVolume();

        if (bgmSource) { bgmSource.mute = false; }
        if (sfxEffectSource) { sfxEffectSource.mute = false; }
        if (sfxButtonSource) { sfxButtonSource.mute = false; }
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
        audioMixer.SetFloat(MUSIC, LinearToDecibel(m));
        audioMixer.SetFloat(EFFECT, LinearToDecibel(e));
    }
    float LinearToDecibel(float v) => (v <= 0.0001f) ? -80f : Mathf.Log10(v) * 20f;

    // ȿ���� ���
    public void PlaySFX(AudioClip clip, SfxBus bus = SfxBus.Effect, float scale = 1f)
    {
        if (!clip) return;
        var src = (bus == SfxBus.ButtonClick) ? sfxButtonSource : sfxEffectSource;
        if (src) src.PlayOneShot(clip, scale);
    }

    // �̺�Ʈ Ű ��� ���
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
