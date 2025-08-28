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

    [Header("끌려갈때 회전")]
    public bool keepFacingOpposite = true;
    public float rotFollowSpeed = 12f;
    [SerializeField] float yawOffset = 0f;


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

    // 시체 잡기
    public bool TryGrab(Transform anchor)
    {
        if (!IsDeadAndDraggable) return false;
        _isGrabbed = true;
        _followAnchor = anchor;

        if (_rb) { _rb.isKinematic = true; _rb.interpolation = RigidbodyInterpolation.Interpolate; }

        // 다리가 플레이어 쪽으로 회전
        Vector3 faceDir = -anchor.forward;
        faceDir = Vector3.ProjectOnPlane(faceDir, Vector3.up).normalized;

        Quaternion rot = Quaternion.LookRotation(faceDir, Vector3.up)
                         * Quaternion.Euler(0f, yawOffset, 0f);

        if (_rb) _rb.MoveRotation(rot);
        else visualRoot.rotation = rot;

        return true;
    }

    public void Release()
    {
        _isGrabbed = false;
        _followAnchor = null;
        if (_rb) _rb.isKinematic = false;
    }

    void FixedUpdate()
    {
        if (!_isGrabbed || _followAnchor == null) return;

        // 위치 추종
        Vector3 target = _followAnchor.position;
        if (_rb) _rb.MovePosition(Vector3.Lerp(visualRoot.position, target, Time.fixedDeltaTime * followSpeed));
        else visualRoot.position = Vector3.Lerp(visualRoot.position, target, Time.fixedDeltaTime * followSpeed);

        // 회전 추종 (반대 + yawOffset 유지)
        if (keepFacingOpposite)
        {
            Vector3 dir = -_followAnchor.forward; // 플레이어 정반대
            dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;

            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up)
                                   * Quaternion.Euler(0f, yawOffset, 0f);  // ★ 추가

            if (_rb) _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, Time.fixedDeltaTime * rotFollowSpeed));
            else visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRot, Time.fixedDeltaTime * rotFollowSpeed);
        }
    }
}
