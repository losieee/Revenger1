using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DiningPuzzleGroup : MonoBehaviour
{
    private SlotPlate[] slots;
    private bool solved = false;

    [SerializeField] GameObject key;

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
                PlayerInventory.PickupsLocked = true;
                SoundManager.i?.PlaySFX(PlayerSfx.WeaponDraw, SfxBus.Effect, 1f);
                key.SetActive(true);

                // 모두 잠금
                foreach (var s in slots) s.SetLocked(true);

                // 열려있던 배치 UI가 있으면 닫기(선택)
                if (PlacementUI.i) PlacementUI.i.CloseIfOpenFor(slots);
            }
            else
            {
                SoundManager.i?.PlaySFX(PlayerSfx.AttackCrowbar, SfxBus.Effect, 1f);
            }
        }
    }
}
