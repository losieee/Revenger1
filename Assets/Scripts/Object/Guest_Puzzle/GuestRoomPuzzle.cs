using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuestRoomPuzzle : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] TMP_Text[] slotTexts;
    [Header("Audio")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip successClip;
    [SerializeField] AudioClip failClip;
    [SerializeField] AudioClip secondFailClip;

    [SerializeField] int[] correct = new int[3] { 3, 6, 5 };

    int[] nums = new int[3];

    GuestRoomPuzzleSetManager set;
    int myIndex;

    public void Init(GuestRoomPuzzleSetManager manager, int index)
    {
        set = manager;
        myIndex = index;

        if (!audioSource) audioSource = GetComponentInParent<AudioSource>();

        for (int i = 0; i < slotTexts.Length; i++)
            slotTexts[i].text = "0";
    }

    void Guard(ref int n) { if (n > 9) n = 0; }

    public void OnClickSlot(int index)
    {
        nums[index]++;
        Guard(ref nums[index]);
        slotTexts[index].text = $"{nums[index]}";
    }

    public void OnClickAnswer()
    {
        if (set == null)
            return;

        // 정답 퍼즐이 아니면 무조건 실패
        if (myIndex != set.solvableIndex)
        {
            Fail();
            return;
        }

        bool ok = nums[0] == correct[0] &&
                  nums[1] == correct[1] &&
                  nums[2] == correct[2];

        if (ok)
            Success();
        else
            Fail();
    }

    void Success()
    {
        if (audioSource && successClip)
        {
            audioSource.volume = 0.1f;
            audioSource.PlayOneShot(successClip);
        }

        set.OnPuzzleSolved();
    }

    void Fail()
    {
        if (audioSource && failClip)
        {
            audioSource.volume = 1f;
            audioSource.PlayOneShot(failClip);
        }

        set.OnPuzzleFailed();
    }


    public void DisableAllButtons()
    {
        foreach (var btn in GetComponentsInChildren<Button>())
            btn.interactable = false;
    }
}
