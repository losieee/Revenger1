using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DiningPuzzleGroup : MonoBehaviour
{
    private SlotPlate[] slots;
    private bool solved = false;

    [SerializeField] GameObject key;
    [SerializeField] private float enemyWaitSeconds = 3f;
    [SerializeField] AudioClip dropKey;

    int failCount = 0;

    void Awake()
    {
        slots = GetComponentsInChildren<SlotPlate>(true);
    }

    public void NotifyChanged() => CheckSolved();

    public void CheckSolved()
    {
        if (solved) return; // 이미 해결됐으면 더 볼 필요 없음

        int total = slots.Length;
        int filled = slots.Count(s => s.IsFilled);
        int correct = slots.Count(s => s.IsCorrectFilled);

        if (total > 0 && filled == total)
        {
            if (correct == total)
            {
                solved = true;
                failCount = 0;
                PlayerInventory.PickupsLocked = true;
                PlaySfx(dropKey);
                key.SetActive(true);

                // 모두 잠금
                foreach (var s in slots) s.SetLocked(true);

                // 열려있던 배치 UI가 있으면 닫기(선택)
                if (PlacementUI.i) PlacementUI.i.CloseIfOpenFor(slots);
            }
            else
            {
                if (failCount % 2 == 0)
                {
                    EnemyMov.AlertDiningGuardToOwnPoint(enemyWaitSeconds);
                    failCount = 0;
                }
                failCount++;
                SoundManager.i?.PlaySFX(PlayerSfx.FailDiningPuzzle, SfxBus.Effect, 1f);
            }
        }
    }

    void PlaySfx(AudioClip clip)
    {
        if (!clip) return;
        AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
    }
}
