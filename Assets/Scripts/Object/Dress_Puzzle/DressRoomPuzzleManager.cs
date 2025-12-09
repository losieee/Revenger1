using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DressRoomPuzzleManager : MonoBehaviour
{
    [Header("Audio")]
    private AudioSource audioSource;
    public AudioClip[] clips;

    [SerializeField] private float enemyWaitSeconds = 3f;

    [Header("Dress")]
    private int[] dressNums = new int[3];
    private readonly int[] dressCorrect = { 5, 2, 3 };

    [SerializeField] private TMP_Text[] dressSlotTexts;

    private int failCount = 0;

    void Start()
    {
        audioSource = transform.root.GetComponent<AudioSource>();

        if (dressSlotTexts == null || dressSlotTexts.Length == 0)
        {
            dressSlotTexts = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var txt = transform.GetChild(i).GetComponentInChildren<TMP_Text>();
                dressSlotTexts[i] = txt;
            }
        }
        ResetDressPuzzle();
    }

    void Guard(ref int num)
    {
        if (num > 9) num = 0;
    }

    public void OnClickDress(int index)
    {
        dressNums[index]++;
        Guard(ref dressNums[index]);

        if (dressSlotTexts != null &&
            index < dressSlotTexts.Length &&
            dressSlotTexts[index] != null)
        {
            dressSlotTexts[index].text = $"{dressNums[index]}";
        }
    }

    public void OnClickAnswer()
    {
        bool dressOK = dressNums[0] == dressCorrect[0]
                    && dressNums[1] == dressCorrect[1]
                    && dressNums[2] == dressCorrect[2];

        if (dressOK)
        {
            audioSource.volume = 0.1f;
            audioSource.PlayOneShot(clips[0]);
            SoundManager.i?.PlaySFX(PlayerSfx.WeaponDraw, SfxBus.Effect, 1f);
            KeyManager.i.AddKey(1);
            DisableButtons();
        }
        else
        {
            failCount++;

            if (failCount == 2)
            {
                audioSource.PlayOneShot(clips[2]);
                EnemyMov.AlertDressGuardToOwnPoint(enemyWaitSeconds);
                failCount = 0;
            }

            audioSource.volume = 1f;
            audioSource.PlayOneShot(clips[1]);
        }
    }

    void DisableButtons()
    {
        foreach (Button btn in GetComponentsInChildren<Button>())
        {
            btn.interactable = false;
        }
    }

    public void ResetDressPuzzle()
    {
        failCount = 0;

        for (int i = 0; i < dressNums.Length; i++)
        {
            dressNums[i] = 0;

            if (dressSlotTexts != null && i < dressSlotTexts.Length && dressSlotTexts[i] != null)
            {
                dressSlotTexts[i].text = "0";
            }
        }

        // 버튼 다시 활성화
        foreach (Button btn in GetComponentsInChildren<Button>())
        {
            btn.interactable = true;
        }
    }
}
