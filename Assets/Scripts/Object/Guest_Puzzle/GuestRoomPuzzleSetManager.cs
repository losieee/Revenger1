using UnityEngine;

public class GuestRoomPuzzleSetManager : MonoBehaviour
{
    public GuestRoomPuzzle[] panels;

    [HideInInspector] public int solvableIndex;
    bool puzzleSolved = false;
    int totalFailCount = 0;

    public AudioSource audioSource;
    public AudioClip doubleFailClip;
    [SerializeField] private float enemyWaitSeconds = 3f;

    void Awake()
    {
        if (panels == null || panels.Length == 0)
            panels = GetComponentsInChildren<GuestRoomPuzzle>(true);

        solvableIndex = Random.Range(0, panels.Length);

        for (int i = 0; i < panels.Length; i++)
        {
            panels[i].Init(this, i);
        }
    }

    // 퍼즐에서 호출
    public void OnPuzzleSolved()
    {
        if (puzzleSolved) return;
        puzzleSolved = true;

        foreach (var puzzle in panels)
        {
            puzzle.DisableAllButtons();
        }
    }

    public void OnPuzzleFailed()
    {
        totalFailCount++;

        if (totalFailCount == 2 && audioSource && doubleFailClip)
        {
            audioSource.PlayOneShot(doubleFailClip);
            EnemyMov.AlertGuestGuardToOwnPoint(enemyWaitSeconds);
            totalFailCount = 0;
        }
    }
}
