using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class DressRoomPuzzleManager : MonoBehaviour
{
    private AudioSource audioSource;
    public AudioClip[] clips;

    int firstNum;
    int middleNum;
    int lastNum;

    int correctfirstNum = 5;
    int correctMiddleNum = 2;
    int correctLastNum = 9;

    int failCount = 0;

    void Start()
    {
        audioSource = transform.root.GetComponent<AudioSource>();
    }

    void Guard()
    {
        if(firstNum > 9) firstNum = 0;
        if(middleNum > 9) middleNum = 0;
        if(lastNum > 9) lastNum = 0;
    }

    public void OnClickFirstBlank()
    {
        firstNum++;
        Guard();
        gameObject.transform.GetChild(0).GetComponentInChildren<TMP_Text>().text = $"{firstNum}";
    }

    public void OnClickSecondBlank()
    {
        middleNum++;
        Guard();
        gameObject.transform.GetChild(1).GetComponentInChildren<TMP_Text>().text = $"{middleNum}";
    }

    public void OnClickLastBlank()
    {
        lastNum++;
        Guard();
        gameObject.transform.GetChild(2).GetComponentInChildren<TMP_Text>().text = $"{lastNum}";
    }

    public void OnClickAnswer()
    {
        if (firstNum == correctfirstNum && middleNum == correctMiddleNum && lastNum == correctLastNum)
        {
            Debug.Log("성공");
            audioSource.volume = 0.1f;
            audioSource.PlayOneShot(clips[0]);
            DestroyButton();
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

    void DestroyButton()
    {
        foreach(Button btn in GetComponentsInChildren<Button>())
        {
            Destroy(btn);
        }
    }
}
