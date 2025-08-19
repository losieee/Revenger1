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
        public string sceneName;                 // �� �̸�(��Ȯ��)
        public AudioClip clip;                   // ����� BGM
        [Range(0f, 1.5f)] public float baseVolume; // Ŭ���� �⺻ ����(��� ����)
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

        // ���� �� ���尪 �ݿ�
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
        // ���� ���� ����, ���� �� �̸����� �� �� Ʈ����
        TryPlaySceneBgm(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ���� ���� �ٲ� ������ �ڵ����� ��ü
        TryPlaySceneBgm(scene.name);
    }

    // �����̴�(0~1) -> dB �� ��ȯ�ؼ� ����
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

    // 0~1 �������� dB �� (0 ������ epsilon)
    float LinearToDecibel(float v)
    {
        if (v <= 0.0001f) return -80f;
        return Mathf.Log10(v) * 20f;
    }

    // ȿ���� ���
    public void PlaySFX(AudioClip clip, SfxBus bus = SfxBus.Effect, float scale = 1f)
    {
        if (clip == null) return;
        AudioSource src = (bus == SfxBus.ButtonClick) ? sfxButtonSource : sfxEffectSource;
        if (src != null) src.PlayOneShot(clip, scale);
    }

    public void TryPlaySceneBgm(string sceneName)
    {
        // ���� ã��
        var idx = sceneBgms.FindIndex(b => b.sceneName == sceneName);
        if (idx < 0) return;

        var target = sceneBgms[idx];
        if (bgmSource == null || target.clip == null) return;

        // ���� ���̸� ����
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

        // fade in (baseVol ����)
        t = 0f;
        while (t < bgmFadeTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, baseVol, t / bgmFadeTime);
            yield return null;
        }
        bgmSource.volume = baseVol; // ���� ����
    }
}
