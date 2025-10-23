using UnityEngine;
using System.Collections.Generic;

public class DrawingPuzzleManager : MonoBehaviour
{
    public static DrawingPuzzleManager Instance;

    [Header("정답 Candle 순서 (왼쪽부터 차례로)")]
    public List<CandleSpot> correctOrder = new List<CandleSpot>();

    public AudioSource audioSource;
    public AudioClip[] clips;           // 0 성공     1 실패        2 2회 실패

    private int failCount = 0;

    // 이번 라운드의 입력 순서/중복 방지
    private List<CandleSpot> inputSequence = new List<CandleSpot>();
    private HashSet<CandleSpot> litSet = new HashSet<CandleSpot>();

    // 이번 라운드가 판정/리셋 중일 때 더 이상 입력 받지 않도록
    private bool roundLocked = false;

    void Awake()
    {
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void OnCandleInteracted(CandleSpot candle)
    {
        if (roundLocked) return;

        // 퍼즐 구성 외의 촛불이면 무시
        if (!correctOrder.Contains(candle)) return;

        // 이미 켠 촛불을 중복 입력하지 않도록
        if (litSet.Contains(candle)) return;

        // 즉시 켜주되(피드백 유지), 실패는 '나중에' 판정
        candle.ActivateCandle();

        // 순서 기록 & 중복 방지 집합 추가
        inputSequence.Add(candle);
        litSet.Add(candle);

        // 모든 촛불이 켜졌다면 이제 한 번에 판정
        if (litSet.Count >= correctOrder.Count)
        {
            EvaluateRound();
        }
    }

    // 모든 촛불이 켜진 시점에 한 번만 호출
    private void EvaluateRound()
    {
        roundLocked = true;

        bool isCorrect = true;
        // 입력 개수와 정답 개수가 같다는 전제 하에 순서 비교
        for (int i = 0; i < correctOrder.Count; i++)
        {
            if (i >= inputSequence.Count || inputSequence[i] != correctOrder[i])
            {
                isCorrect = false;
                break;
            }
        }

        if (isCorrect)
        {
            // 성공 사운드
            if (clips != null && clips.Length > 0 && clips[0] != null)
                audioSource.PlayOneShot(clips[0]);

            // 연속 실패 초기화
            failCount = 0;
        }
        else
        {
            // 실패 카운트에 따른 사운드
            failCount++;
            if (failCount >= 2)
            {
                if (clips != null && clips.Length > 2 && clips[2] != null)
                    audioSource.PlayOneShot(clips[2]);
            }
            else
            {
                if (clips != null && clips.Length > 1 && clips[1] != null)
                    audioSource.PlayOneShot(clips[1]);
            }

            // 모든 촛불 끄고 다음 라운드 준비
            ResetAll();
        }
    }

    public void ResetAll()
    {
        foreach (var c in correctOrder)
        {
            if (c != null)
                c.ResetCandle();
        }

        // 라운드 상태 초기화
        inputSequence.Clear();
        litSet.Clear();
        roundLocked = false;
    }
}
