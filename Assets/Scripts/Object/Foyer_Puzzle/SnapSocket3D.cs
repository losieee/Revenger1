using UnityEngine;
using System.Collections.Generic;
public class SnapSocket3D : MonoBehaviour
{
    public int slotId = 1;
    public Transform anchor;
    public float snapRange = 0.08f;
    public DragPiece3D current { get; private set; }
    public bool IsEmpty => current == null;
    public Vector3 AnchorPos => (anchor ? anchor : transform).position;
    public static readonly List<SnapSocket3D> All = new();
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }
    public bool TryLock(DragPiece3D p) { if (!IsEmpty) { return false; } current = p; p.LockTo(this); FoyerPuzzleManager.i?.OnSocketChanged(); return true; }
    public void Release(DragPiece3D p) { if (current == p) { current = null; FoyerPuzzleManager.i?.OnSocketChanged(); } }
}
