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

    [Header("애니메이션")]
    [SerializeField] private Animator anim;         // 시체 애니메이터
    [SerializeField] private float moveThreshold = 0.1f; // 이동/정지 판정
    private Vector3 _lastAnchorPos;
    private float _smoothedSpeed;


    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (!visualRoot) visualRoot = transform;
        if (!anim) anim = GetComponentInChildren<Animator>();
    }

    public void OnDeath()
    {
        IsDeadAndDraggable = true;
        if (handleTrigger) handleTrigger.enabled = true;
        if (_rb) _rb.isKinematic = false;

        anim?.SetBool("IsDead", true);
    }

    // 시체 잡기
    public bool TryGrab(Transform anchor)
    {
        if (!IsDeadAndDraggable) return false;
        _isGrabbed = true;
        _followAnchor = anchor;
        _lastAnchorPos = anchor.position;
        _smoothedSpeed = 0f;

        if (_rb) { _rb.isKinematic = true; _rb.interpolation = RigidbodyInterpolation.Interpolate; }

        if (anim && !anim.enabled) anim.enabled = true;

        // 애니 파라미터
        if (anim)
        {
            anim.ResetTrigger("Drop");
            anim.SetBool("IsGrabbed", true);
            anim.SetFloat("Speed", 0f);
            anim.SetTrigger("Grab");
        }

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

        if (anim)
        {
            anim.ResetTrigger("Grab");
            anim.SetBool("IsGrabbed", false);
            anim.SetFloat("Speed", 0f);
            anim.SetTrigger("Drop");
        }
    }

    void FixedUpdate()
    {
        if (!_isGrabbed || _followAnchor == null) return;

        // 위치 추종
        Vector3 target = _followAnchor.position;
        if (_rb) _rb.MovePosition(Vector3.Lerp(visualRoot.position, target, Time.fixedDeltaTime * followSpeed));
        else visualRoot.position = Vector3.Lerp(visualRoot.position, target, Time.fixedDeltaTime * followSpeed);

        // 회전 추종
        if (keepFacingOpposite)
        {
            Vector3 dir = -_followAnchor.forward;
            dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
            if (_rb) _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, Time.fixedDeltaTime * rotFollowSpeed));
            else visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, targetRot, Time.fixedDeltaTime * rotFollowSpeed);
        }

        // 플레이어 이동 속도 → Speed 파라미터 갱신
        Vector3 cur = _followAnchor.position;
        float rawSpeed = (cur - _lastAnchorPos).magnitude / Mathf.Max(Time.fixedDeltaTime, 1e-5f);
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed, 1f - Mathf.Exp(-10f * Time.fixedDeltaTime));
        _lastAnchorPos = cur;

        if (anim)
        {
            // 임계값 아래는 0으로 깎아 깜빡임 방지
            float animSpeed = (_smoothedSpeed >= moveThreshold) ? _smoothedSpeed : 0f;
            anim.SetFloat("Speed", animSpeed);
        }
    }
}
