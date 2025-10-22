using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DressRoomPuzzleManager : MonoBehaviour
{
    [Header("Audio")]
    private AudioSource audioSource;
    public AudioClip[] clips;

    [Header("Dress")]
    private int[] dressNums = new int[3];
    private readonly int[] dressCorrect = { 5, 2, 9 };

    [Header("Guest")]
    private int[] guestNums = new int[3];
    private readonly int[] guestCorrect = { 6, 5, 5 };

    private int failCount = 0;

    void Start()
    {
        audioSource = transform.root.GetComponent<AudioSource>();
    }

    void Guard(ref int num)
    {
        if (num > 9) num = 0;
    }

    public void OnClickDress(int index)
    {
        dressNums[index]++;
        Guard(ref dressNums[index]);

        var txt = transform.GetChild(index).GetComponentInChildren<TMP_Text>();
        txt.text = $"{dressNums[index]}";
    }

    public void OnClickGuest(int index)
    {
        guestNums[index]++;
        Guard(ref guestNums[index]);

        var txt = transform.GetChild(index).GetComponentInChildren<TMP_Text>();
        txt.text = $"{guestNums[index]}";
    }

    public void OnClickAnswer()
    {
        bool dressOK = dressNums[0] == dressCorrect[0]
                    && dressNums[1] == dressCorrect[1]
                    && dressNums[2] == dressCorrect[2];

        bool guestOK = guestNums[0] == guestCorrect[0]
                    && guestNums[1] == guestCorrect[1]
                    && guestNums[2] == guestCorrect[2];

        if (dressOK || guestOK)
        {
            Debug.Log("성공");
            audioSource.volume = 0.1f;
            audioSource.PlayOneShot(clips[0]);
            DestroyButtons();
        }
        else
        {
            Debug.Log("실패");
            failCount++;

            if (failCount == 2)
            {
                audioSource.PlayOneShot(clips[2]);
                failCount = 0;
            }

            audioSource.volume = 1f;
            audioSource.PlayOneShot(clips[1]);
        }
    }

    void DestroyButtons()
    {
        foreach (Button btn in GetComponentsInChildren<Button>())
        {
            Destroy(btn);
        }
    }
}
