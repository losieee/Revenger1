using UnityEngine;
using UnityEngine.Audio;

public static class SfxPlayer
{
    static AudioSource _src2D;
    public static AudioMixerGroup outputGroup;

    public static void Play2D(AudioClip clip, float volume = 0.1f)
    {
        if (!clip) return;
        if (_src2D == null)
        {
            var go = new GameObject("SFX2D");
            Object.DontDestroyOnLoad(go);
            _src2D = go.AddComponent<AudioSource>();
            _src2D.spatialBlend = 0f;            // 2D
            _src2D.playOnAwake = false;

            if (outputGroup != null)
                _src2D.outputAudioMixerGroup = outputGroup;
        }
        _src2D.PlayOneShot(clip, volume);
    }
}
