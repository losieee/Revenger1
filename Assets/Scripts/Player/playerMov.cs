using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMov : MonoBehaviour
{
    // ������Ʈ
    public Rigidbody rb;
    private Animator animator;

    // �̵� �� ȸ��
    public float speed = 5f;
    private float currentMoveSpeed = 0f;
    public float rotSpeed = 5f;
    public float runSpeed = 3f;
    public Transform cameraPivot;

    private Vector3 currentMoveInput;
    private Vector3 lastFixedPosition;
    private float lastFixedSpeed = 0f;
    private bool isRunning;

    private float moveX, moveY, velX, velY;
    private float smoothTime = 0.05f;

    // ��Ÿ�� �غ�
    public float climbDuration = 3.25f;
    public float climbCheckDistance = 2.0f;
    public LayerMask climbableLayer;
    private bool canClimbZone = false;
    private bool isClimbing = false;
    private bool blockInput = false; // �� ������ ���� �Է� ����

    // �����̴� �Ҹ� ����
    public float walkDetectRange = 6f;
    public float runDetectRange = 12f;
    public LayerMask aiLayerMask;

    // ���� �Ŵ޷��ֱ�
    private bool isHolding = false;
    private bool canStartClimb = false;
    private bool isLerpingHoldOffset = false;
    private Vector3 holdLerpStartPos, holdLerpTargetPos;
    private Quaternion holdLerpStartRot, holdLerpTargetRot;
    private float holdLerpTimer = 0f;
    private float holdLerpDuration = 0.1f;
    private Vector3 holdingStartPos; // �Ŵ޸��� ���� �� ��ġ ����

    private Vector3 climbStartPos, climbTargetPos;
    private Quaternion climbStartRot, climbTargetRot;
    private float climbTimer = 0f;

    // ���� / ����
    private bool isJumping = false;
    private bool collisionGrounded = false;
    private float jumpForce = 5f;
    private float verticalVelocity = 0f;
    private bool isLanding = false;
    private float landingTimer = 0f;
    private float landingDelay = 0.6f;
    private bool isGrounded = true;
    private bool wasGroundedLastFrame = true;
    private float jumpCooldown = 1.9f; // ���� ��Ÿ��
    private float jumpCooldownTimer = 0f;
    private float airMultiplier;
    private bool ignoreGroundedCheck = false;
    private float ignoreGroundedTimer = 0f;
    private float ignoreDurationAfterJump = 0.14f; // ���� �� 0.14�ʰ� isGrounded ����

    // Alt �̵�
    private Vector3 savedForward, savedRight;
    private Quaternion savedRotation;
    private bool wasAltPressedLastFrame, justReleasedAlt;

    // �ɱ�
    private bool isCrouching = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        lastFixedPosition = rb.position;
    }

    void Update()
    {
        collisionGrounded = false;

        // �ٴ� ���� - SphereCast ���
        isGrounded = CheckGrounded();

        // �� ���� ���� ó��
        if (isLerpingHoldOffset)
        {
            holdLerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(holdLerpTimer / holdLerpDuration);
            transform.position = Vector3.Lerp(holdLerpStartPos, holdLerpTargetPos, t);
            transform.rotation = Quaternion.Slerp(holdLerpStartRot, holdLerpTargetRot, t);
            if (t >= 1f) isLerpingHoldOffset = false;
            return;
        }

        if (isHolding)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);
            if (Input.GetKeyDown(KeyCode.Space) && canStartClimb)
                StartClimbFromHold(0.52f);
            return;
        }

        if (isClimbing)
        {
            climbTimer += Time.deltaTime;
            float t = Mathf.Clamp01(climbTimer / climbDuration);
            transform.position = Vector3.Lerp(climbStartPos, climbTargetPos, t);
            transform.rotation = Quaternion.Slerp(climbStartRot, climbTargetRot, t);

            // climb �� �Է� ����
            currentMoveInput = Vector3.zero;

            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);

            if (t >= 1f)
            {
                isClimbing = false;
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
                lastFixedPosition = rb.position;
            }
            return;
        }

        // �Է�
        isRunning = Input.GetKey(KeyCode.LeftShift) && !isCrouching;
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

        // blockInput �� �Է� �̵� ����
        if (!blockInput)
        {
            Vector3 targetMoveInput = (moveForward * v + moveRight * h).normalized;
            float lerpSpeed = (isGrounded && !isLanding) ? 15f : 5f;
            currentMoveInput = Vector3.Lerp(currentMoveInput, targetMoveInput, Time.deltaTime * lerpSpeed);

            if (currentMoveInput.magnitude < 0.05f)
                currentMoveInput = Vector3.zero;
        }
        else
        {
            // Climb �߿��� �̵� �Է� ����
            currentMoveInput = Vector3.zero;
        }

        // �ִϸ��̼� �Ķ����
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

        // ȸ��
        if (currentMoveInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * 100f * Time.deltaTime);
        }

        wasAltPressedLastFrame = isAlt;
        if (justReleasedAlt && !isAlt) justReleasedAlt = false;

        // �� ���(Holding) ����
        if (Input.GetKeyDown(KeyCode.Space) && canClimbZone && !isHolding && !isClimbing)
        {
            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit wall, climbCheckDistance, climbableLayer))
            {
                StartHolding(wall);
            }
        }

        // ����
        if (Input.GetKeyDown(KeyCode.Space) && !canClimbZone && isGrounded && !isJumping && jumpCooldownTimer <= 0f && !isCrouching)
        {
            isJumping = true;
            jumpCooldownTimer = jumpCooldown;
            animator.SetBool("IsJumping", true);

            ignoreGroundedCheck = true;
            ignoreGroundedTimer = ignoreDurationAfterJump;

            rb.isKinematic = false;
            rb.useGravity = true;

            Vector3 velocity = rb.velocity;
            Vector3 jumpDir = currentMoveInput.sqrMagnitude > 0.01f ? currentMoveInput.normalized : transform.forward;
            float groundSpeed = isRunning ? speed * runSpeed : speed;
            float jumpForwardSpeed = (currentMoveInput.magnitude > 0.1f) ? groundSpeed * 0.4f : groundSpeed * 0.2f;

            velocity.x = jumpDir.x * jumpForwardSpeed;
            velocity.z = jumpDir.z * jumpForwardSpeed;
            velocity.y = jumpForce;
            rb.velocity = velocity;
        }

        // ���� ���� ��� Grounded ����
        if (ignoreGroundedCheck)
        {
            ignoreGroundedTimer -= Time.deltaTime;
            if (ignoreGroundedTimer <= 0f)
            {
                ignoreGroundedCheck = false;
            }
        }

        // ���� ��Ÿ�� ����
        if (jumpCooldownTimer > 0f)
        {
            jumpCooldownTimer -= Time.deltaTime;
        }

        // ���� ����
        verticalVelocity = rb.velocity.y;
        bool isFalling = verticalVelocity < -0.1f && !isGrounded;
        animator.SetBool("IsFalling", isFalling);

        if (isFalling && animator.GetBool("IsJumping"))
        {
            //animator.SetTrigger("JumpingDown");
        }

        // ���� ����
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

        // C ���� �ɱ�
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
            animator.SetBool("IsCrouching", isCrouching);
        }

        // �ӵ� ����
        float moveSpeed = isRunning ? speed * runSpeed : speed;
        if (isCrouching) moveSpeed *= 0.6f;
        currentMoveSpeed = moveSpeed;

        // �����̴� �Ҹ� ����
        CheckNearbyEnemies();
    }

    private bool CheckGrounded()
    {
        if (ignoreGroundedCheck) return false;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float radius = 0.25f;
        float distance = 0.6f;

        bool grounded = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore);
        Debug.DrawRay(origin, Vector3.down * distance, grounded ? Color.green : Color.red); // �ʷϻ��̸� ������

        return grounded;
    }

    void FixedUpdate()
    {
        bool block = isClimbing || isHolding;
        if (block)
        {
            lastFixedSpeed = 0f;
            lastFixedPosition = rb.position;
            return;
        }

        // �̵� ó��
        airMultiplier = isGrounded ? 1f : 0.5f;

        Vector3 move = currentMoveInput * currentMoveSpeed * airMultiplier * Time.fixedDeltaTime;
        Vector3 newPos = rb.position + move;
        rb.MovePosition(rb.position + move);

        float movedDistance = (newPos - lastFixedPosition).magnitude;
        lastFixedSpeed = movedDistance / Time.fixedDeltaTime;
        lastFixedPosition = newPos;
    }

    // �� ���(Holding) ����
    void StartHolding(RaycastHit hit)
    {
        isCrouching = false;  // �� ���� �� �ڵ����� ������ ó��
        animator.SetBool("IsCrouching", false);

        blockInput = true;
        isHolding = true;
        canStartClimb = false;
        rb.useGravity = false;
        rb.isKinematic = true;

        float holdDistanceFromWall = 0.14f;

        // 1. ���� ���� �������� ���� ��ġ ��� (Y�� ���� ����)
        Vector3 wallNormal = hit.normal;

        // �Է°� ������� �� ���� ���߾� ��ġ ���
        Vector3 targetPos = hit.point + wallNormal * holdDistanceFromWall;
        targetPos.y = transform.position.y; // ���� Y ����

        // 2. ȸ��
        Quaternion targetRot = Quaternion.LookRotation(-wallNormal);

        // 3. ����
        holdLerpStartPos = transform.position;
        holdLerpTargetPos = targetPos;
        holdLerpStartRot = transform.rotation;
        holdLerpTargetRot = targetRot;
        holdLerpTimer = 0f;
        isLerpingHoldOffset = true;

        holdingStartPos = targetPos;    // �Ŵ޷��ִ� ��ġ ����

        animator.SetBool("Hold", true);
    }

    // �� ������(Climb) ����
    public void StartClimbFromHold(float duration)
    {
        isHolding = false;
        canStartClimb = false;
        climbTimer = 0f;
        climbDuration = duration; // ���⼭ �ð� ����

        climbStartPos = transform.position;
        climbStartRot = transform.rotation;

        climbTargetPos = holdingStartPos + Vector3.up * 2.3f;
        climbTargetRot = transform.rotation;

        animator.SetBool("Hold", false);
        animator.SetTrigger("Climb");

        isClimbing = true;
        rb.useGravity = false;
    }

    // ClimbZone ����/��Ż
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

    // �� �Ŵ޸��� ���� ����,�ð� ���
    public void MoveUpDuringHold(float height, float duration)
    {
        if (!isHolding) return;

        StartCoroutine(MoveHoldWithDip(height, duration));
    }

    // �� �Ŵ޸��� �� �ܰ�
    private IEnumerator MoveHoldWithDip(float height, float duration)
    {
        // 1�ܰ�: ��¦ �Ʒ��� ������ (���� ������)
        Vector3 start = transform.position;
        Vector3 downPos = start + new Vector3(0f, -0.1f, 0f);
        float downDuration = 0.1f;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / downDuration;
            transform.position = Vector3.Lerp(start, downPos, t);
            yield return null;
        }

        // 2�ܰ�: ���� �ø��� (����)
        Vector3 upTarget = start + new Vector3(0f, height, 0f);
        t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(downPos, upTarget, t);
            yield return null;
        }

        // Climb ���� ���·� ��ȯ
        canStartClimb = true;
    }

    // �� �ȶո��� �ϱ����� �ö󰡴ٰ� ������ ����
    public void MoveForwardAfterClimb(float distance, float duration)
    {
        StartCoroutine(ForwardLerpRoutine(distance, duration));
    }

    private IEnumerator ForwardLerpRoutine(float distance, float duration)
    {
        rb.useGravity = false;

        Vector3 start = transform.position;
        Vector3 target = start + transform.forward * distance;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        rb.velocity = Vector3.zero;

        // �̵� ������ ����
        lastFixedPosition = rb.position;
    }
    public void OnClimbEnd()
    {
        isClimbing = false;
        rb.useGravity = true;
        rb.isKinematic = false;

        rb.velocity = Vector3.zero; // Ȥ�� Ƣ�� �̵� ����
        blockInput = false;
    }

    // ������ �Ҹ� ����
    void CheckNearbyEnemies()
    {
        if (currentMoveInput.magnitude < 0.05f)
            return;

        float detectRange = isRunning ? runDetectRange : walkDetectRange;
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange, aiLayerMask);

        foreach (Collider col in hits)
        {
            EnemyMov enemy = col.GetComponent<EnemyMov>();
            if (enemy != null)
            {
                enemy.PlayerDetected(transform.position);
            }
        }
    }

    // ���� �����
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, walkDetectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, runDetectRange);
    }
}
