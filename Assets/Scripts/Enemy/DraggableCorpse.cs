using UnityEngine;

public class DraggableCorpse : MonoBehaviour
{
    public bool IsDeadAndDraggable { get; private set; }
    public Collider handleTrigger;      // 잡는 판정
    public Transform visualRoot;        // 실제로 끌려올려야 하는 본체
    public float followSpeed = 10f;

    private bool _isGrabbed;
    private Transform _followAnchor;
    private Rigidbody _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (!visualRoot) visualRoot = transform;
    }

    public void OnDeath()
    {
        IsDeadAndDraggable = true;
        if (handleTrigger) handleTrigger.enabled = true;
        if (_rb) _rb.isKinematic = false;
    }

    public bool TryGrab(Transform anchor)
    {
        if (!IsDeadAndDraggable) return false;
        _isGrabbed = true;
        _followAnchor = anchor;
        return true;
    }

    public void Release()
    {
        _isGrabbed = false;
        _followAnchor = null;
    }

    void FixedUpdate()
    {
        if (!_isGrabbed || _followAnchor == null) return;
        Vector3 target = _followAnchor.position;

        if (_rb) _rb.MovePosition(Vector3.Lerp(visualRoot.position, target, Time.fixedDeltaTime * followSpeed));
        else visualRoot.position = Vector3.Lerp(visualRoot.position, target, Time.fixedDeltaTime * followSpeed);
    }
}
