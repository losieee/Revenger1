using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMov : MonoBehaviour
{
    // 컴포넌트 참조
    private Rigidbody rb;
    private Animator animator;

    // 이동 및 회전
    public float speed = 5f;
    public float rotSpeed = 5f;
    public float runSpeed = 3f;
    public Transform cameraPivot;

    private Vector3 currentMoveInput;
    private Vector3 lastFixedPosition;
    private float lastFixedSpeed = 0f;
    private bool isRunning;

    // 애니메이션 파라미터
    private float moveX, moveY, velX, velY;
    private float smoothTime = 0.05f;

    // 벽타기
    public float climbDuration = 3.25f;
    public float climbCheckDistance = 2.0f;
    public LayerMask climbableLayer;
    private bool canClimbZone = false;
    private bool isClimbing = false;
    private Vector3 climbStartPos, climbTargetPos;
    private Quaternion climbStartRot, climbTargetRot;
    private float climbTimer = 0f;

    // 점프 / 낙하 / 착지
    private bool isJumping = false;
    private bool collisionGrounded = false;
    private float jumpForce = 5f;
    private float verticalVelocity = 0f;
    private bool isLanding = false;
    private float landingTimer = 0f;
    private float landingDelay = 0.4f;
    private bool isGrounded = true;
    private bool wasGroundedLastFrame = true;

    // Alt 이동
    private Vector3 savedForward, savedRight;
    private Quaternion savedRotation;
    private bool wasAltPressedLastFrame, justReleasedAlt;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        lastFixedPosition = rb.position;
    }

    void Update()
    {
        collisionGrounded = false;

        // 바닥 판정
        int groundMask = LayerMask.GetMask("Ground", "Climbable");
        isGrounded = Mathf.Abs(rb.velocity.y) < 0.05f;

        // 벽타기 중이면 고정
        if (isClimbing)
        {
            climbTimer += Time.deltaTime;
            float t = Mathf.Clamp01(climbTimer / climbDuration);
            transform.position = Vector3.Lerp(climbStartPos, climbTargetPos, t);
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

        // 입력 처리
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

        if (isAlt && !wasAltPressedLastFrame)
        {
            savedForward = camForward;
            savedRight = camRight;
            savedRotation = transform.rotation;
        }

        if (wasAltPressedLastFrame && !isAlt)
        {
            cameraPivot.GetComponent<CameraMov>().RecenterToPlayerSmooth(0.2f);
            justReleasedAlt = true;
        }

        Vector3 moveForward = (isAlt || justReleasedAlt) ? savedForward : camForward;
        Vector3 moveRight = (isAlt || justReleasedAlt) ? savedRight : camRight;

        // 이동 입력 처리
        if (isGrounded && !isLanding)
        {
            Vector3 targetMoveInput = (moveForward * v + moveRight * h).normalized;
            float lerpSpeed = Vector3.Dot(currentMoveInput, targetMoveInput) < -0.1f ? 7f : 15f;
            currentMoveInput = Vector3.Lerp(currentMoveInput, targetMoveInput, Time.deltaTime * lerpSpeed);
            if (currentMoveInput.magnitude < 0.05f) currentMoveInput = Vector3.zero;
        }
        else
        {
            Vector3 targetMoveInput = (moveForward * v + moveRight * h).normalized;
            float lerpSpeed = 5f;
            currentMoveInput = Vector3.Lerp(currentMoveInput, targetMoveInput, Time.deltaTime * lerpSpeed);
        }

        // 애니메이션 파라미터
        Vector3 localMove = transform.InverseTransformDirection(currentMoveInput);
        moveX = Mathf.SmoothDamp(moveX, localMove.x, ref velX, smoothTime);
        moveY = Mathf.SmoothDamp(moveY, Mathf.Max(localMove.z, 0f), ref velY, smoothTime);
        animator.SetFloat("MoveX", moveX);
        animator.SetFloat("MoveY", moveY);

        float speedParam = (isGrounded && currentMoveInput.magnitude > 0.05f) ? (isRunning ? 1f : 0.5f) : 0f;
        animator.SetFloat("Speed", speedParam, 0.1f, Time.deltaTime);

        if (speedParam == 0f)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
        }

        // 회전 처리
        if (currentMoveInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * 100f * Time.deltaTime);
        }

        // Alt 상태 처리
        wasAltPressedLastFrame = isAlt;
        if (justReleasedAlt && !isAlt) justReleasedAlt = false;

        // 벽타기 시도
        if (Input.GetKeyDown(KeyCode.Space) && canClimbZone)
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit wall, climbCheckDistance, climbableLayer))
            {
                StartClimb(wall);
            }
        }

        // 점프 처리
        if (Input.GetKeyDown(KeyCode.Space) && !canClimbZone && isGrounded && !isJumping)
        {
            isJumping = true;
            animator.SetBool("IsJumping", true);

            Vector3 jumpDirection = currentMoveInput.normalized;
            float groundSpeed = isRunning ? speed * runSpeed : speed;
            float jumpForwardSpeed = (currentMoveInput.magnitude > 0.1f) ? groundSpeed * 0.4f : 0f;

            Vector3 velocity = jumpDirection * jumpForwardSpeed;
            velocity.y = jumpForce;

            rb.velocity = velocity;
        }

        // 낙하 감지
        verticalVelocity = rb.velocity.y;
        bool isFalling = verticalVelocity < -0.1f && !isGrounded;

        animator.SetBool("IsFalling", isFalling);

        if (isFalling && animator.GetBool("IsJumping"))
        {
            animator.SetTrigger("JumpingDown");
        }

        // 착지 감지 및 처리
        if (!wasGroundedLastFrame && isGrounded)
        {
            animator.SetTrigger("Land");
            animator.SetBool("IsJumping", false);
            animator.SetBool("IsFalling", false);
            isJumping = false;
            isLanding = true;
            landingTimer = landingDelay;
        }

        if (isLanding)
        {
            landingTimer -= Time.deltaTime;
            if (landingTimer <= 0f) isLanding = false;
        }

        wasGroundedLastFrame = isGrounded;
    }

    void FixedUpdate()
    {
        bool shouldBlockMovement = isClimbing || isLanding;

        if (shouldBlockMovement)
        {
            lastFixedSpeed = 0f;
            lastFixedPosition = rb.position;
            return;
        }

        float moveSpeed = isRunning ? speed * runSpeed : speed;
        float airMoveMultiplier = isGrounded ? 1f : 0.5f;

        Vector3 move = currentMoveInput * moveSpeed * airMoveMultiplier * Time.fixedDeltaTime;
        Vector3 newPos = rb.position + move;

        rb.MovePosition(newPos);

        float movedDistance = (newPos - lastFixedPosition).magnitude;
        lastFixedSpeed = movedDistance / Time.fixedDeltaTime;
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
            canClimbZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("ClimbZone"))
            canClimbZone = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        collisionGrounded = true;

        if (!isGrounded)
        {
            if (isJumping || animator.GetBool("IsJumping"))
            {
                isJumping = false;
                animator.SetBool("IsJumping", false);
            }

            if (animator.GetBool("IsFalling"))
            {
                animator.SetBool("IsFalling", false);
            }

            animator.ResetTrigger("Land");
            animator.SetTrigger("Land");

            isLanding = true;
            landingTimer = landingDelay;
        }
    }
}
