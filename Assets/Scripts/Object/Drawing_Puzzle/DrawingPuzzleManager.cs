using UnityEngine;
using System.Collections.Generic;

public class DrawingPuzzleManager : MonoBehaviour
{
    public static DrawingPuzzleManager Instance;

    [Header("정답 Candle 순서 (왼쪽부터 차례로)")]
    public List<CandleSpot> correctOrder = new List<CandleSpot>();

    public AudioSource audioSource;
    public AudioClip[] clips;

    private int currentIndex = 0;
    private bool isFailed = false;
    private int failCount = 0;

    void Awake()
    {
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    // CandleSpot이 상호작용했을 때 호출됨
    public void OnCandleInteracted(CandleSpot candle)
    {
        if (isFailed) return;  // 실패 상태에서는 무시

        // 순서가 맞는지 검사
        if (correctOrder[currentIndex] == candle)
        {
            // 올바른 순서
            candle.ActivateCandle();
            currentIndex++;

            // 모두 정답이면 성공 처리
            if (currentIndex >= correctOrder.Count)
            {
                if (clips != null && clips.Length > 0 && clips[0] != null)
                    audioSource.PlayOneShot(clips[0]);

                // 성공했으니 실패 횟수도 초기화 가능
                failCount = 0;
            }
        }
        else
        {
            // 순서 틀림 → 실패 처리
            failCount++; // 실패 횟수 증가

            if (failCount >= 2)
            {
                // 2회 이상 실패 시 clips[2] 재생
                if (clips != null && clips.Length > 2 && clips[2] != null)
                    audioSource.PlayOneShot(clips[2]);
            }
            else
            {
                // 첫 번째 실패 시 clips[1]
                if (clips != null && clips.Length > 1 && clips[1] != null)
                    audioSource.PlayOneShot(clips[1]);
            }

            ResetAll();
        }
    }

    public void ResetAll()
    {
        isFailed = true;

        // 모든 CandleSpot 끄기
        foreach (var c in correctOrder)
        {
            if (c != null)
                c.ResetCandle();
        }

        currentIndex = 0;
        isFailed = false;
    }
}
