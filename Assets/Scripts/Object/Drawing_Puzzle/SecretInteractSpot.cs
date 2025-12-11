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
    bool inSecret = false;
    Coroutine holdCo;


    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerMov>()) playerIn = true;

        if (other.CompareTag("Player"))
            inSecret = true;
    }
    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerMov>())
        {
            playerIn = false;
            StopHold();
        }

        if (other.CompareTag("Player"))
            inSecret = false;
    }

    void Update()
    {
        if (!playerIn) return;

        bool hasLighter = PlayerInventory.Instance && PlayerInventory.Instance.ContainsId("Lighter");
        if (!hasLighter) return;


        if (!inSecret) return;

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

        playerIn = false;

        var col = GetComponent<Collider>();
        col.enabled = false;

        StopHold(); // 루프 정리

        StartCoroutine(RotateToAngle(-116f, 0.5f));
    }

    IEnumerator RotateToAngle(float targetX, float duration)
    {
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.Euler(targetX, transform.eulerAngles.y, transform.eulerAngles.z);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t / duration);
            yield return null;
        }

        transform.rotation = targetRot;
    }
}
