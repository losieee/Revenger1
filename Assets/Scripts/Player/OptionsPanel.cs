using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionsPanel : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider effectSlider;

    [SerializeField] private GameObject soundSettingImg;
    [SerializeField] private GameObject keySettingImg;
    [SerializeField] private Slider sensitivity;

    void OnEnable()
    {
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

    // 슬라이더 OnValueChanged(float)에 연결
    public void OnMusicChanged(float v) => SoundManager.i?.SetMusicVolume(v);
    public void OnEffectChanged(float v) => SoundManager.i?.SetEffectVolume(v);
}
