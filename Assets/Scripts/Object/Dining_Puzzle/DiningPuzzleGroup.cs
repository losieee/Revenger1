using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DiningPuzzleGroup : MonoBehaviour
{
    private SlotPlate[] slots;

    void Awake()
    {
        slots = GetComponentsInChildren<SlotPlate>(true);
    }

    public void NotifyChanged() => CheckSolved();

    public void CheckSolved()
    {
        int total = slots.Length;
        int filled = slots.Count(s => s.IsFilled);
        int correct = slots.Count(s => s.IsCorrectFilled);

        if (total > 0 && filled == total)
        {
            if (correct == total)
                Debug.Log("[Puzzle] 정답! 모든 슬롯 완료");
            else
                Debug.Log("[Puzzle] 실패! 전부 채웠지만 조합이 틀림");
        }
    }
}
