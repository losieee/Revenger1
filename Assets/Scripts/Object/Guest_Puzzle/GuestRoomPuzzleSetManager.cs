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

    public void ResetAllPuzzles()
    {
        puzzleSolved = false;
        totalFailCount = 0;

        solvableIndex = Random.Range(0, panels.Length);

        // 각 퍼즐 리셋
        foreach (var p in panels)
        {
            if (p != null)
                p.ResetPuzzle();
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
