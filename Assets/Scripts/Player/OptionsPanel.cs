using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OptionsPanel : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider effectSlider;

    [SerializeField] private GameObject soundSettingImg;
    [SerializeField] private GameObject keySettingImg;
    [SerializeField] private Slider sensitivity;
    [SerializeField] private Slider lighting;
    [SerializeField] private Volume lightingVolume;

    const float LightingMin = 0.5f;
    const float LightingMax = 3f;
    const float LightingDefault = 1.5f;
    const string PPKey_Lighting = "LightingExposure";

    ColorAdjustments colorAdj;

    void Awake()
    {
        // ColorAdjustments 캐싱
        if (lightingVolume && lightingVolume.profile)
        {
            lightingVolume.profile.TryGet(out colorAdj);
        }
    }

    void OnEnable()
    {
        // 슬라이더 범위, 기본값 설정
        if (lighting)
        {
            lighting.wholeNumbers = false;
            lighting.minValue = LightingMin;
            lighting.maxValue = LightingMax;

            float saved = PlayerPrefs.GetFloat(PPKey_Lighting, LightingDefault);
            lighting.SetValueWithoutNotify(saved);
            ApplyLighting(saved);
        }

        // 창이 켜질 때 현재 값을 UI에 반영
        if (SoundManager.i != null)
        {
            musicSlider.SetValueWithoutNotify(SoundManager.i.GetMusicVolume01());
            effectSlider.SetValueWithoutNotify(SoundManager.i.GetEffectVolume01());
        }

        if (CameraMov.i != null)
            sensitivity.SetValueWithoutNotify(CameraMov.i.GetSensitivity01());
    }

    public void OnSoundSetting()
    {
        soundSettingImg.SetActive(true);
        keySettingImg.SetActive(false);
    }

    public void OnKeySetting()
    {
        soundSettingImg.SetActive(false);
        keySettingImg.SetActive(true);
    }

    public void OnSensitivityChanged(float v)
    {
        if (CameraMov.i != null)
            CameraMov.i.SetSensitivity01(v);
    }

    public void OnLightingChanged(float v)
    {
        ApplyLighting(v);
        PlayerPrefs.SetFloat(PPKey_Lighting, v);
    }

    void ApplyLighting(float v)
    {
        if (colorAdj != null)
        {
            colorAdj.postExposure.overrideState = true;
            colorAdj.postExposure.value = Mathf.Clamp(v, LightingMin, LightingMax);
        }
    }

    // 슬라이더 OnValueChanged(float)에 연결
    public void OnMasterChanged(float v) => SoundManager.i.SetMasterVolume(v);
    public void OnMusicChanged(float v) => SoundManager.i?.SetMusicVolume(v);
    public void OnEffectChanged(float v) => SoundManager.i?.SetEffectVolume(v);

    public void GameExit()
    {
        SceneManager.sceneLoaded += OnMainLobbyLoaded;
        SceneManager.LoadScene("MainLobby");
    }

    private void OnMainLobbyLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainLobby") return;
        SceneManager.sceneLoaded -= OnMainLobbyLoaded;

        if (GameBootstrap.i)
        {
            if (GameBootstrap.i.player) Destroy(GameBootstrap.i.player.gameObject);
            if (GameBootstrap.i.cameraRig) Destroy(GameBootstrap.i.cameraRig.gameObject);
            Destroy(GameBootstrap.i.gameObject);
        }

        Time.timeScale = 1;
    }
}
