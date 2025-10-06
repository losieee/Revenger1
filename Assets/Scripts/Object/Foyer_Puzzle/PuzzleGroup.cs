using System.Collections.Generic;
using UnityEngine;

public class PuzzleGroup : MonoBehaviour
{
    public readonly List<SnapSocket3D> sockets = new();
    public void Register(SnapSocket3D s) { if (!sockets.Contains(s)) sockets.Add(s); }
    public void Unregister(SnapSocket3D s) { sockets.Remove(s); }

    // 편의 함수
    public void GetCounts(out int total, out int filled, out int correct)
    {
        total = sockets.Count; filled = 0; correct = 0;
        foreach (var s in sockets)
        {
            if (s.current != null)
            {
                filled++;
                if (s.current.pieceId == s.slotId) correct++;
            }
        }
    }
}
