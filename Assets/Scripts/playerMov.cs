using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMov : MonoBehaviour
{
    // 컴포넌트
    public Rigidbody rb;
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

    private float moveX, moveY, velX, velY;
    private float smoothTime = 0.05f;

    // 벽타기 준비물
    public float climbDuration = 3.25f;
    public float climbCheckDistance = 2.0f;
    public LayerMask climbableLayer;
    private bool canClimbZone = false;
    private bool isClimbing = false;

    // 벽에 매달려있기
    private bool isHolding = false;
    private bool canStartClimb = false;
    private bool isLerpingHoldOffset = false;
    private Vector3 holdLerpStartPos, holdLerpTargetPos;
    private float holdLerpTimer = 0f;
    private float holdLerpDuration = 0.1f;

    private Vector3 climbStartPos, climbTargetPos;
    private Quaternion climbStartRot, climbTargetRot;
    private float climbTimer = 0f;

    // 점프 / 낙하
    private bool isJumping = false;
    private bool collisionGrounded = false;
    private float jumpForce = 5f;
    private float verticalVelocity = 0f;
    private bool isLanding = false;
    private float landingTimer = 0f;
    private float landingDelay = 0.6f;
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

        // 바닥 체크
        isGrounded = Mathf.Abs(rb.velocity.y) < 0.05f;

        //벽 붙은 상태에서 애니메이션 이벤트로 위로 이동
        if (isLerpingHoldOffset)
        {
            holdLerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(holdLerpTimer / holdLerpDuration);
            transform.position = Vector3.Lerp(holdLerpStartPos, holdLerpTargetPos, t);

            if (t >= 1f)
                isLerpingHoldOffset = false;

            return; // 다른 동작 차단

        }
        // 벽에 붙은 상태(Holding)
        if (isHolding)
        {
            // 이동 차단
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);

            // 벽에 붙어있는 상태에서 Climb 시도
            if (Input.GetKeyDown(KeyCode.Space) && canStartClimb)
            {
                StartClimbFromHold(0.54f);
            }

            return;
        }

        // Climb 상태 처리
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

        // 입력
        isRunning = Input.GetKey(KeyCode.LeftShift);
        bool isAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 camForward = cameraPivot.forward;
        Vector3 camRight = cameraPivot.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();

        if (isAlt && !wasAltPressedLastFrame)
        {
            savedForward = camForward;
            savedRight = camRight;
            savedRotation = transform.rotation;
        }

        if (wasAltPressedLastFrame && !isAlt)
        {
            cameraPivot.GetComponent<CameraMov>()?.RecenterToPlayerSmooth(0.2f);
            justReleasedAlt = true;
        }

        Vector3 moveForward = (isAlt || justReleasedAlt) ? savedForward : camForward;
        Vector3 moveRight = (isAlt || justReleasedAlt) ? savedRight : camRight;

        Vector3 targetMoveInput = (moveForward * v + moveRight * h).normalized;
        float lerpSpeed = (isGrounded && !isLanding) ? 15f : 5f;
        currentMoveInput = Vector3.Lerp(currentMoveInput, targetMoveInput, Time.deltaTime * lerpSpeed);

        if (currentMoveInput.magnitude < 0.05f) currentMoveInput = Vector3.zero;

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

        // 회전
        if (currentMoveInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * 100f * Time.deltaTime);
        }

        wasAltPressedLastFrame = isAlt;
        if (justReleasedAlt && !isAlt) justReleasedAlt = false;

        // 벽 잡기(Holding) 시작
        if (Input.GetKeyDown(KeyCode.Space) && canClimbZone && !isHolding && !isClimbing)
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit wall, climbCheckDistance, climbableLayer))
            {
                StartHolding(wall);
            }
        }

        // 점프
        if (Input.GetKeyDown(KeyCode.Space) && !canClimbZone && isGrounded && !isJumping)
        {
            isJumping = true;
            animator.SetBool("IsJumping", true);

            Vector3 velocity = rb.velocity;
            Vector3 jumpDir = currentMoveInput.sqrMagnitude > 0.01f ? currentMoveInput.normalized : transform.forward;
            float groundSpeed = isRunning ? speed * runSpeed : speed;
            float jumpForwardSpeed = (currentMoveInput.magnitude > 0.1f) ? groundSpeed * 0.4f : groundSpeed * 0.2f;

            velocity.x = jumpDir.x * jumpForwardSpeed;
            velocity.z = jumpDir.z * jumpForwardSpeed;
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

        // 착지 감지
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
        bool block = isClimbing || isHolding || (isLanding && isGrounded);
        if (block)
        {
            lastFixedSpeed = 0f;
            lastFixedPosition = rb.position;
            return;
        }

        float moveSpeed = isRunning ? speed * runSpeed : speed;
        float airMultiplier = isGrounded ? 1f : 0.5f;

        Vector3 move = currentMoveInput * moveSpeed * airMultiplier * Time.fixedDeltaTime;
        Vector3 newPos = rb.position + move;
        rb.MovePosition(newPos);

        float movedDistance = (newPos - lastFixedPosition).magnitude;
        lastFixedSpeed = movedDistance / Time.fixedDeltaTime;
        lastFixedPosition = newPos;
    }

    // 벽 잡기(Holding) 시작
    void StartHolding(RaycastHit hit)
    {
        isHolding = true;
        canStartClimb = false;
        rb.isKinematic = true;

        // 벽 앞 위치 계산
        Vector3 forwardOffset = -hit.normal * 0.05f;
        Vector3 targetPos = transform.position + forwardOffset;
        Quaternion targetRot = Quaternion.LookRotation(-hit.normal);

        transform.position = targetPos;
        transform.rotation = targetRot;

        animator.SetBool("Hold", true);
    }

    // 벽 오르기(Climb) 시작
    public void StartClimbFromHold(float duration)
    {
        isHolding = false;
        canStartClimb = false;
        climbTimer = 0f;
        climbDuration = duration; // 여기서 시간 설정

        climbStartPos = transform.position;
        climbStartRot = transform.rotation;

        climbTargetPos = transform.position + Vector3.up * 2f;
        climbTargetRot = transform.rotation;

        animator.SetBool("Hold", false);
        animator.SetTrigger("Climb");

        isClimbing = true;
        rb.isKinematic = true;
    }

    // ClimbZone 진입/이탈
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
                animator.SetBool("IsFalling", false);

            animator.ResetTrigger("Land");
            animator.SetTrigger("Land");

            isLanding = true;
            landingTimer = landingDelay;
        }
    }

    public void OnJumpingDownComplete()
    {
        isLanding = false;
    }

    // 벽 매달리기 위한 높이,시간 계산
    public void MoveUpDuringHold(float height, float duration)
    {
        if (!isHolding) return;

        StartCoroutine(MoveHoldWithDip(height, duration));
    }

    // 벽 매달리기 전 단계
    private IEnumerator MoveHoldWithDip(float height, float duration)
    {
        // 1단계: 살짝 아래로 내리기
        Vector3 start = transform.position;
        Vector3 downPos = start + new Vector3(0f, -0.05f, 0f);
        float downDuration = 0.1f;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / downDuration;
            transform.position = Vector3.Lerp(start, downPos, t);
            yield return null;
        }

        // 2단계: 위로 올리기
        Vector3 upTarget = start + new Vector3(0f, height, 0f);
        t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(downPos, upTarget, t);
            yield return null;
        }

        // Climb 가능 상태로 전환
        canStartClimb = true;
    }

    // 벽 안뚫리게 하기위한 올라가다가 앞으로 가기
    public void MoveForwardAfterClimb(float distance, float duration)
    {
        StartCoroutine(ForwardLerpRoutine(distance, duration));
    }

    private IEnumerator ForwardLerpRoutine(float distance, float duration)
    {
        rb.useGravity = false;
        rb.isKinematic = true;

        Vector3 start = transform.position;
        Vector3 target = start + transform.forward * distance;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }
    }
}
