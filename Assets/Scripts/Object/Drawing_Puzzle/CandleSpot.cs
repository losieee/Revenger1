using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CandleSpot : MonoBehaviour
{
    [Header("활성화할 자식 오브젝트")]
    [SerializeField] private GameObject childToActivate;
    [SerializeField] private bool requireLighter = true;

    public AudioClip clip;  // 각 촛불마다 다른 효과음도 가능

    private bool isActive = false;


    void OnValidate()
    {
        if (!childToActivate && transform.childCount > 0)
            childToActivate = transform.GetChild(0).gameObject;
    }

    public bool TryInteract(PlayerMov player)
    {
        if (isActive) return false;

        if (requireLighter)
        {
            var inv = PlayerInventory.Instance;
            if (!inv || !inv.ContainsId("Lighter"))
            {
                Debug.Log("라이터가 필요합니다.");
                return false;
            }
        }

        if (DrawingPuzzleManager.Instance != null)
        {
            DrawingPuzzleManager.Instance.OnCandleInteracted(this);
            return true;
        }

        return false;
    }

    public void ActivateCandle()
    {
        if (!childToActivate && transform.childCount > 0)
            childToActivate = transform.GetChild(0).gameObject;

        if (childToActivate)
            childToActivate.SetActive(true);

        // 여기서 매니저의 AudioSource로 재생
        if (clip != null && DrawingPuzzleManager.Instance != null && DrawingPuzzleManager.Instance.audioSource != null)
        {
            DrawingPuzzleManager.Instance.audioSource.PlayOneShot(clip);
        }

        isActive = true;
    }

    public void ResetCandle()
    {
        if (childToActivate)
            childToActivate.SetActive(false);

        isActive = false;
    }
}
