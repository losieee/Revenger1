using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SecretHoldSpot : MonoBehaviour
{
    public float holdTime = 3f;
    public AudioClip holdingLoop;      // 누르고 있는 동안 재생
    public AudioClip completeClip;     // 완료 효과음(선택)

    bool playerIn = false;
    bool holding = false;
    Coroutine holdCo;


    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerMov>()) playerIn = true;
    }
    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerMov>())
        {
            playerIn = false;
            StopHold();
        }
    }

    void Update()
    {
        if (!playerIn) return;

        bool hasLighter = PlayerInventory.Instance && PlayerInventory.Instance.ContainsId("Lighter");
        if (!hasLighter) return;

        // 누르는 동안 진행
        if (KeyBindings.GetKey(GameAction.Interaction))
        {
            if (!holding) StartHold();
        }
        else
        {
            if (holding) StopHold();
            DrawingPuzzleManager.Instance.audioSource.Stop();
        }
    }

    void StartHold()
    {
        holding = true;
        if (DrawingPuzzleManager.Instance.audioSource && holdingLoop)
        {
            DrawingPuzzleManager.Instance.audioSource.clip = holdingLoop;
            if (!DrawingPuzzleManager.Instance.audioSource.isPlaying) 
                DrawingPuzzleManager.Instance.audioSource.Play();
        }
        holdCo = StartCoroutine(HoldRoutine());
    }

    void StopHold()
    {
        holding = false;
        if (DrawingPuzzleManager.Instance.audioSource && DrawingPuzzleManager.Instance.audioSource.loop) 
            DrawingPuzzleManager.Instance.audioSource.Stop();
        if (holdCo != null) { StopCoroutine(holdCo); holdCo = null; }
    }

    IEnumerator HoldRoutine()
    {
        float t = 0f;
        while (t < holdTime)
        {
            if (!holding) yield break; // 중간에 손 떼면 취소
            t += Time.deltaTime;
            yield return null;
        }

        // 완료
        if (DrawingPuzzleManager.Instance.audioSource && completeClip) 
            DrawingPuzzleManager.Instance.audioSource.PlayOneShot(completeClip);
        Debug.Log("SECRET: 3초 홀드 성공!");

        var col = GetComponent<Collider>();
        col.enabled = false;

        // TODO: 열림/보상 로직
        StopHold(); // 루프 정리
    }
}
