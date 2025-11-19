using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader i;

    [Header("Fade Times")]
    [SerializeField] float fadeOutTime = 0.5f;

    Image img;

    void Awake()
    {
        i = this;
        img = GetComponent<Image>();

        var c = img.color;
        c.a = 1f;
        img.color = c;

        img.raycastTarget = false;
    }
    public void FadeOutAndLoad(Action sceneLoadAction)
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutAndLoadCo(sceneLoadAction));
    }

    IEnumerator FadeOutAndLoadCo(Action sceneLoadAction)
    {
        yield return Fade(0f, 1f, fadeOutTime);

        sceneLoadAction?.Invoke();
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        Color c = img.color;

        c.a = from;
        img.color = c;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(from, to, t / duration);
            c.a = a;
            img.color = c;
            yield return null;
        }

        c.a = to;
        img.color = c;
    }
}
