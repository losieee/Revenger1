using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionsPanel : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider effectSlider;

    void OnEnable()
    {
        // 창이 켜질 때 현재 값을 UI에 반영
        if (SoundManager.i == null) return;
        musicSlider.SetValueWithoutNotify(SoundManager.i.GetMusicVolume01());
        effectSlider.SetValueWithoutNotify(SoundManager.i.GetEffectVolume01());
    }

    // 슬라이더 OnValueChanged(float)에 연결
    public void OnMusicChanged(float v) => SoundManager.i?.SetMusicVolume(v);
    public void OnEffectChanged(float v) => SoundManager.i?.SetEffectVolume(v);
}
