using UnityEngine;
[RequireComponent(typeof(Collider))]
public class DragPiece3D : MonoBehaviour
{
    public int pieceId = 1;
    public bool isLocked { get; private set; }
    [HideInInspector] public SnapSocket3D current;
    public void Release() { if (current != null) { current.Release(this); current = null; } isLocked = false; }
    public void LockTo(SnapSocket3D s) { current = s; isLocked = true; var a = s.anchor ? s.anchor : s.transform; transform.position = a.position; transform.rotation = a.rotation; }
}
