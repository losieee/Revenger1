using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonActivate : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] AudioClip hoverClip;

    Transform child0;

    void Awake()
    {
        if (transform.childCount > 0)
        {
            child0 = transform.GetChild(0);
            if (child0) child0.gameObject.SetActive(false); // 기본 비활성 권장
        }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (child0) child0.gameObject.SetActive(true);

        // 효과음 재생
        if (SoundManager.i != null)
            SoundManager.i.PlaySFX(hoverClip);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (child0) child0.gameObject.SetActive(false);
    }
}
