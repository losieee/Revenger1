using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMov : MonoBehaviour
{
    private Rigidbody rb;
    private Animator animator;

    public float speed = 5f;
    public float rotSpeed = 5f;
    public Transform cameraPivot; // CameraMov가 붙어있는 Transform
    public float runSpeed = 3f;

    private Vector3 moveInput;
    private Vector3 currentMoveInput;
    private float lastFixedSpeed = 0f;
    private Vector3 lastFixedPosition;

    private bool isRunning;
    private bool canClimbZone = false;
    private bool isGrounded = true;

    // Alt 기준 벡터 저장
    private Vector3 savedForward;
    private Vector3 savedRight;
    private Quaternion savedRotation;
    private bool wasAltPressedLastFrame;
    private bool justReleasedAlt;

    // 애니메이션 파라미터 스무딩
    private float moveX = 0f;
    private float moveY = 0f;
    private float velX = 0f;
    private float velY = 0f;
    private float smoothTime = 0.05f;

    //벽타기 관련
    public float climbDuration = 3.25f;
    public float climbCheckDistance = 2.0f; // 박스 감지 거리
    public LayerMask climbableLayer;        // 감지할 레이어

    private bool isClimbing = false;
    private Vector3 climbStartPos;
    private Vector3 climbTargetPos;
    private Quaternion climbStartRot;
    private Quaternion climbTargetRot;
    private float climbTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        lastFixedPosition = rb.position;
    }

    void Update()
    {
        if (isClimbing)
        {
            climbTimer += Time.deltaTime;
            float t = Mathf.Clamp01(climbTimer / climbDuration);

            Vector3 pose = Vector3.Lerp(climbStartPos, climbTargetPos, t);
            pose.y = Mathf.Lerp(climbStartPos.y, climbTargetPos.y, t);

            transform.position = pose;
            transform.rotation = Quaternion.Slerp(climbStartRot, climbTargetRot, t);

            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);

            if (t >= 1f)
            {
                isClimbing = false;
                rb.isKinematic = false;
            }
            return;
        }

        isRunning = Input.GetKey(KeyCode.LeftShift);
        bool isAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 camForward = cameraPivot.forward;
        Vector3 camRight = cameraPivot.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        // Alt 처음 눌렀을 때 방향과 회전 저장
        if (isAlt && !wasAltPressedLastFrame)
        {
            savedForward = camForward;
            savedRight = camRight;
            savedRotation = transform.rotation;
        }

        // Alt 해제 시 카메라를 플레이어 방향으로 리센터
        if (wasAltPressedLastFrame && !isAlt)
        {
            cameraPivot.GetComponent<CameraMov>().RecenterToPlayerSmooth(0.2f); // 0.5초에 부드럽게
            justReleasedAlt = true;
        }

        // Alt 누르거나 Alt 해제 직후엔 저장된 기준 사용
        Vector3 moveForward;
        Vector3 moveRight;

        if (isAlt || justReleasedAlt)
        {
            moveForward = savedForward;
            moveRight = savedRight;
        }
        else
        {
            moveForward = camForward;
            moveRight = camRight;
        }

        // 이동 입력
        if (isGrounded)
        {
            Vector3 targetMoveInput = (moveForward * v + moveRight * h).normalized;

            // 즉시 반응 Lerp
            float lerpSpeed = 15f;
            if (Vector3.Dot(currentMoveInput, targetMoveInput) < -0.1f)
                lerpSpeed = 7f;
            currentMoveInput = Vector3.Lerp(currentMoveInput, targetMoveInput, Time.deltaTime * lerpSpeed);

            // Deadzone
            if (currentMoveInput.magnitude < 0.05f)
                currentMoveInput = Vector3.zero;
        }
        else
        {
            // 공중에서는 이동 입력 차단
            currentMoveInput = Vector3.zero;
        }


        // 애니메이션 파라미터
        Vector3 localMove = transform.InverseTransformDirection(currentMoveInput);
        moveX = Mathf.SmoothDamp(moveX, localMove.x, ref velX, smoothTime);
        moveY = Mathf.SmoothDamp(moveY, Mathf.Max(localMove.z, 0f), ref velY, smoothTime);

        animator.SetFloat("MoveX", moveX);
        animator.SetFloat("MoveY", moveY);

        // 원하는 기준 속도(0.05 이하이면 멈춘 걸로 간주)
        float speedParam = 0f;
        if (isGrounded && currentMoveInput.magnitude > 0.05f)
        {
            speedParam = isRunning ? 1f : 0.5f;
        }
        else
        {
            speedParam = 0f;
        }
        animator.SetFloat("Speed", speedParam, 0.1f, Time.deltaTime);

        // 움직임 없으면 MoveX, MoveY도 0으로
        if (speedParam == 0f)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
        }

        // 회전
        if (currentMoveInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveInput);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotSpeed * 100f * Time.deltaTime
            );
        }

        // Alt 상태 기록
        wasAltPressedLastFrame = isAlt;

        // Alt 해제 후 1프레임 유지
        if (justReleasedAlt && !isAlt)
        {
            justReleasedAlt = false;
        }

        //space를 눌러 벽 타기
        if (Input.GetKeyDown(KeyCode.Space) && canClimbZone)
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit wall, climbCheckDistance, climbableLayer))
            {
                StartClimb(wall);
            }
        }
    }

    void FixedUpdate()
    {
        if (isClimbing)
        {
            lastFixedSpeed = 0f;
            lastFixedPosition = rb.position;
            return;
        }

        // 공중이면 이동 막기
        if (!isGrounded)
        {
            lastFixedSpeed = 0f;
            lastFixedPosition = rb.position;
            return;
        }
        
        float moveSpeed = isRunning ? speed * runSpeed : speed;
        Vector3 move = currentMoveInput * moveSpeed * Time.fixedDeltaTime;

        Vector3 newPos = rb.position + move;

        rb.MovePosition(newPos);

        // 실제로 이동한 거리 측정
        float movedDistance = (newPos - lastFixedPosition).magnitude;
        lastFixedSpeed = movedDistance / Time.fixedDeltaTime;

        // 현재 위치를 다음 프레임을 위해 저장
        lastFixedPosition = newPos;
    }
    void StartClimb(RaycastHit hit)
    {
        isClimbing = true;
        climbTimer = 0f;

        rb.isKinematic = true;

        climbStartPos = transform.position;
        climbStartRot = transform.rotation;

        Bounds bounds = hit.collider.bounds;
        Vector3 topPoint = new Vector3(hit.point.x, bounds.max.y, hit.point.z);
        Vector3 ledgeOffset = -hit.normal * 0.3f;

        climbTargetPos = topPoint + ledgeOffset;
        climbTargetRot = Quaternion.LookRotation(-hit.normal);

        animator.SetTrigger("Climb");
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ClimbZone"))
        {
            canClimbZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("ClimbZone"))
        {
            canClimbZone = false;
        }
    }
    private void OnCollisionStay(Collision collision)
    {
        isGrounded = true;
    }

    private void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}