using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class FoyerPuzzleManager : MonoBehaviour
{
    public static FoyerPuzzleManager i { get; private set; }

    [Header("초기 흩뿌릴 영역(없으면 Pieces 부모 사용)")]
    public RectTransform scatterArea;

    [Header("드래그 조각들")]
    public List<DraggablePiece> pieces = new();

    [Header("근접 스냅 설정")]
    public float snapRadius = 80f;

    void Awake() => i = this;

    void Start() { ScatterAll(); }

    void ScatterAll()
    {
        RectTransform area = scatterArea ? scatterArea : (RectTransform)pieces[0].transform.parent;
        var half = area.rect.size * 0.45f;

        foreach (var p in pieces)
        {
            p.transform.SetParent(area, true);
            var rt = (RectTransform)p.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-5f, 5f));
        }
    }

    // 근처 '빈 슬롯(아무 슬롯)' 찾기
    public DropSlot FindNearestSnappableSlot(Vector2 pieceScreenPos)
    {
        Camera cam = GetComponentInParent<Canvas>()?.worldCamera;
        DropSlot best = null;
        float bestDist = float.MaxValue;

        foreach (var s in DropSlot.All)
        {
            if (!s.IsEmpty) continue;
            Vector2 slotScreen = RectTransformUtility.WorldToScreenPoint(cam, s.WorldCenter());
            float d = Vector2.Distance(pieceScreenPos, slotScreen);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return (bestDist <= snapRadius) ? best : null;
    }

    // 매번 슬롯 구성이 바뀔 때 호출 → 정답 여부 재평가
    public void OnSlotChanged()
    {
        int total = DropSlot.All.Count;
        int filled = 0;
        int correct = 0;

        foreach (var s in DropSlot.All)
        {
            var cur = s.Current;
            if (cur != null)
            {
                filled++;
                if (cur.pieceId == s.slotId) correct++;
            }
        }

        // 모든 칸이 차있고, 모든 칸에서 slotId == pieceId 면 정답
        if (filled == total && correct == total)
        {
            PuzzleSolved();
        }
    }

    void PuzzleSolved()
    {
        Debug.Log("퍼즐 완성");
    }

    // 기존 호출 지점 호환
    public void NotifyPlacedCorrect() => OnSlotChanged();
    public void NotifyWrong() => OnSlotChanged();
}
