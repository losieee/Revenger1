using UnityEngine;
using UnityEngine.Events;

public class PuzzleManager : MonoBehaviour
{
    [Header("Solved Event (퍼즐별로 인스펙터에서 바인딩)")]
    public UnityEvent onSolved;

    [Header("참조")]
    public PuzzleGroup group;

    [Header("퍼즐 실패 시 적 호출")]
    [SerializeField] private float enemyWaitSeconds = 3f;

    private bool _solved;        // 한 번만 발동
    private bool _firing;        // 재진입 방지

    public AudioSource audioSource;
    public AudioClip fail;
    public AudioClip success;

    private int failCount = 0;

    private void Start()
    {
        audioSource = GameObject.Find("Player").GetComponent<AudioSource>();
    }

    void Reset()
    {
        if (!group) group = GetComponentInChildren<PuzzleGroup>();
    }

    public void OnSocketChanged()
    {
        if (_firing || _solved || !group) return;
        _firing = true;

        group.GetCounts(out int total, out int filled, out int correct);

        if (total > 0)
        {
            if (filled == total && correct == total)
            {
                // 모든 퍼즐이 맞았을 때
                _solved = true;
                failCount = 0;
                audioSource.PlayOneShot(success, 0.5f);

                if (!string.Equals(gameObject.name, "GuestManager"))
                {
                    KeyManager.i.AddKey(1);
                }

                try { onSolved?.Invoke(); }
                finally { _firing = false; }
                return;
            }
            else if (filled == total && correct != total)
            {
                failCount++;

                if (failCount % 2 == 0)
                {
                    // 모든 칸이 채워졌는데 정답이 아닐 때
                    audioSource.PlayOneShot(fail, 0.5f);
                    EnemyMov.AlertFoyerGuardToOwnPoint(enemyWaitSeconds);
                }
            }
        }

        _firing = false;
    }
}
