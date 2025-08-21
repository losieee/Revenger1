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
        // â�� ���� �� ���� ���� UI�� �ݿ�
        if (SoundManager.i == null) return;
        musicSlider.SetValueWithoutNotify(SoundManager.i.GetMusicVolume01());
        effectSlider.SetValueWithoutNotify(SoundManager.i.GetEffectVolume01());
    }

    // �����̴� OnValueChanged(float)�� ����
    public void OnMusicChanged(float v) => SoundManager.i?.SetMusicVolume(v);
    public void OnEffectChanged(float v) => SoundManager.i?.SetEffectVolume(v);
}
