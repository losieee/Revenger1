using UnityEngine;
using UnityEngine.Events;
public class FoyerPuzzleManager : MonoBehaviour
{
    public static FoyerPuzzleManager i { get; private set; }
    public UnityEvent onSolved;
    public PlayerMov player;

    void Awake() => i = this;

    public void OnSocketChanged()
    {
        int total = SnapSocket3D.All.Count, filled = 0, correct = 0;
        foreach (var s in SnapSocket3D.All) { if (s.current != null) { filled++; if (s.current.pieceId == s.slotId) correct++; } }
        if (filled == total && correct == total) onSolved?.Invoke();
    }

    public void OnSolvedHandler()
    {
        // 성공 처리
        Debug.Log("퍼즐 성공");
        if (player) player.ExitLaundryView();
        // TODO: 미션 완료 플래그/사운드 등
    }
}
