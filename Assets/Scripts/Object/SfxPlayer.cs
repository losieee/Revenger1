using UnityEngine;

public static class SfxPlayer
{
    public static void Play(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f)
    {
        if (!clip) return;
        var go = new GameObject("SFX_" + clip.name);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 1f;     // 3D
        src.minDistance = 1.5f;
        src.maxDistance = 20f;
        src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        src.volume = Mathf.Clamp01(volume);
        src.Play();
        Object.Destroy(go, clip.length / src.pitch + 0.05f);
    }
}
