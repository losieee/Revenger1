using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DiningPuzzleGroup : MonoBehaviour
{
    private SlotPlate[] slots;
    private bool solved = false;

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
                Debug.Log("[Puzzle] 정답! 모든 슬롯 완료 -> 잠금");

                // 모두 잠금
                foreach (var s in slots) s.SetLocked(true);

                // 열려있던 배치 UI가 있으면 닫기(선택)
                if (PlacementUI.i) PlacementUI.i.CloseIfOpenFor(slots);
            }
            else
            {
                Debug.Log("[Puzzle] 실패! 전부 채웠지만 조합이 틀림");
            }
        }
    }
}
