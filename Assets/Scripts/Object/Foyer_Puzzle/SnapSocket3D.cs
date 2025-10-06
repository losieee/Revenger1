using UnityEngine;
public class SnapSocket3D : MonoBehaviour
{
    public int slotId = 1;
    public Transform anchor;
    public float snapRange = 0.08f;

    public DragPiece3D current { get; private set; }
    public bool IsEmpty => current == null;
    public Vector3 AnchorPos => (anchor ? anchor : transform).position;

    private PuzzleGroup group;
    private PuzzleManager manager;

    void Awake()
    {
        group = GetComponentInParent<PuzzleGroup>();
        manager = GetComponentInParent<PuzzleManager>();
    }

    void OnEnable() { group?.Register(this); }
    void OnDisable() { group?.Unregister(this); }

    public bool TryLock(DragPiece3D p)
    {
        if (!IsEmpty) return false;
        current = p; p.LockTo(this);
        manager?.OnSocketChanged();
        return true;
    }
    public void Release(DragPiece3D p)
    {
        if (current == p)
        {
            current = null;
            manager?.OnSocketChanged();
        }
    }
}
