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
    [HideInInspector] public float remainingWallHeight = 0f;    // �� ����
    public LayerMask climbableLayer;
    private bool canClimbZone = false;
    private bool isClimbing = false;
    private bool blockInput = false; // �� ������ ���� �Է� ����
    private float lastBoxWallRemainingHeight = 0f; // MoveToBoxTop���� ������ ��ü ���� ����

    // �����̴� �Ҹ� ����
    public float walkDetectRange = 6f;
    public float runDetectRange = 12f;
    public LayerMask aiLayerMask;

    // ���� �Ŵ޷��ֱ�
    [HideInInspector] public float detectedWallHeight = 0f;
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
    private float ignoreDurationAfterJump = 0.25f; // ���� ���� ��� ���� ���� ����

    public LayerMask groundLayer;                 // '��' ���̾ ����� ���� ����
    [Range(0f, 1f)] public float groundMinNormalY = 0.55f; // ��� ���(~56��)

    // (�ɼ�) ���� ���� �պ��� ��¦ ������ ��¦ �о��
    public float frontCheckDistance = 0.35f;
    public float wallPushStrength = 2.0f;

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
        groundLayer = LayerMask.GetMask("Ground", "Climbable");
    }

    void Update()
    {
        // space�� �����ڸ��� Land Ʈ���� ����
        if (Input.GetKeyDown(KeyCode.Space))
            ClearLandTriggers();

        // �ٴ� ���� - SphereCast ���(groundLayer + normal.y ����)
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

        // �� ���(Holding) ���� ���� �˻�
        if (Input.GetKeyDown(KeyCode.Space) && canClimbZone && !isHolding && !isClimbing)
        {
            Vector3 dir = transform.forward;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f; // �� ���� ���� (�߹�)

            if (Physics.Raycast(rayOrigin, dir, out RaycastHit wall, climbCheckDistance, climbableLayer))
            {
                float wallTopY = wall.collider.bounds.max.y;
                float wallBottomY = wall.collider.bounds.min.y;
                float wallHeight = wallTopY - wallBottomY;

                detectedWallHeight = wallHeight;

                float jumpHeight = Mathf.Clamp(wallHeight * 0.5f, 0.4f, 2.0f);
                float rayStartY = Mathf.Min(transform.position.y + jumpHeight, wallTopY - 0.1f);
                Vector3 rayStart = new Vector3(transform.position.x, rayStartY, transform.position.z);

                if (Physics.Raycast(rayStart, dir, out RaycastHit wallHit, climbCheckDistance, climbableLayer))
                {
                    StartHolding(wallHit);
                }
            }
        }

        // ����
        if (Input.GetKeyDown(KeyCode.Space) && !canClimbZone && isGrounded && !isJumping && jumpCooldownTimer <= 0f && !isCrouching)
        {
            ClearLandTriggers();        // Land Ʈ���� �ʱ�ȭ
            isJumping = true;
            jumpCooldownTimer = jumpCooldown;
            animator.SetBool("IsJumping", true);

            // ���� ���� Grounded ����
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

            // (�ɼ�) �տ� ���� ��¦ ������ ��¦ �о�� �� �鷯����/���� ���� ����
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward,
                                 out RaycastHit front, frontCheckDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (((1 << front.collider.gameObject.layer) & groundLayer) == 0)
                {
                    Vector3 horiz = new Vector3(front.normal.x, 0f, front.normal.z).normalized;
                    velocity += horiz * wallPushStrength;
                }
            }

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

        // ���� ����(������ ����)
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

    // �ִϸ����� Ʈ����/���� �ʱ�ȭ
    private void ClearLandTriggers()
    {
        animator.ResetTrigger("Land");
    }

    private bool CheckGrounded()
    {
        if (ignoreGroundedCheck) return false;

        Vector3 origin = transform.position + Vector3.up * 0.3f;
        float radius = 0.23f;
        float distance = 0.7f;

        if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, distance,
                               groundLayer, QueryTriggerInteraction.Ignore))
        {
            // ���� ���� �鸸 �������� ����
            return hit.normal.y >= groundMinNormalY;
        }
        return false;
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
        ClearLandTriggers();
        isCrouching = false;  // �� ���� �� �ڵ����� ������ ó��
        animator.SetBool("IsCrouching", false);

        // �� ���� ����
        float wallTop = hit.collider.bounds.max.y;
        float playerFoot = transform.position.y;
        float wallHeight = wallTop - playerFoot;

        // ���� ���� ��� �� ����
        remainingWallHeight = wallHeight;

        // ���̰� 1 ���϶�� BoxJump ����
        if (wallHeight <= 1.0f)
        {
            StartBoxJump(hit.point, hit.normal, wallHeight);
            return;
        }

        // �Ϲ� ��Ÿ�� (Hold ����)
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
        ClearLandTriggers();
        isHolding = false;
        canStartClimb = false;
        climbTimer = 0f;
        climbDuration = duration;

        climbStartPos = transform.position;
        climbStartRot = transform.rotation;

        // �� ���̿� ���� �ö� ���� ���
        float climbHeight = Mathf.Clamp(detectedWallHeight + 0.15f, 1f, 3.5f); // �ణ ���� ��� ���� ����

        climbTargetPos = holdingStartPos + Vector3.up * climbHeight;
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

    // ���� ���� �Ǵ�(���� ó������ ���)
    private bool IsGroundContact(Collision col)
    {
        // groundLayer�� ���Ե� ���̾����� Ȯ��
        if ((groundLayer.value & (1 << col.gameObject.layer)) == 0) return false;

        // ���� ���� �鸸 �������� ����
        foreach (var c in col.contacts)
            if (c.normal.y >= groundMinNormalY) return true;

        return false;
    }

    private void OnCollisionStay(Collision collision)
    {
        // ���� ���� ���� �ð����� � �浹�� ������ ó������ ����
        if (ignoreGroundedCheck) return;

        // ������� ������ �ƴϸ�(=��/����) ����
        if (!IsGroundContact(collision)) return;

        // ���⼭�� ���� ������ �ϰ�, Ʈ���Ŵ� Update��
        //  (!wasGroundedLastFrame && isGrounded) ���������� �Ǵ�.
        if (isJumping || animator.GetBool("IsJumping"))
        {
            isJumping = false;
            animator.SetBool("IsJumping", false);
        }
        if (animator.GetBool("IsFalling"))
            animator.SetBool("IsFalling", false);
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

    // ���� ���� BoxJump �ִϸ��̼����� �ѱ��
    void StartBoxJump(Vector3 wallPoint, Vector3 wallNormal, float height)
    {
        ClearLandTriggers();
        // ���� ���� ����
        isCrouching = false;
        animator.SetBool("IsCrouching", false);

        // �̵� �� �߷� ����
        blockInput = true;
        isHolding = false;
        isClimbing = false;
        rb.useGravity = false;

        // Ÿ�� ��ġ �� ȸ�� ���
        Vector3 targetPos = wallPoint + wallNormal * 0.14f;
        targetPos.y = transform.position.y;
        Quaternion targetRot = Quaternion.LookRotation(-wallNormal);

        // ������ �ε巴�� ó���ϴ� �ڷ�ƾ ����
        StartCoroutine(BoxJumpPrepareLerp(targetPos, targetRot, 0.15f));
    }

    // �� �տ� �ε巴�� ������Ű�� �ڷ�ƾ
    private IEnumerator BoxJumpPrepareLerp(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        // ��ġ ���� �Ϸ� �� �ִϸ��̼� ���
        animator.Play("BoxJump");
    }

    // BoxJump �ִϸ��̼� �� ��ġ�� �ε巴�� �̵�
    private IEnumerator BoxJumpLerp(Vector3 targetPos, float duration)
    {
        Vector3 start = transform.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, targetPos, t);
            yield return null;
        }

        // �̵� ���� �� ���� ����
        rb.useGravity = true;
        rb.isKinematic = false;
        blockInput = false;
        lastFixedPosition = rb.position;
    }

    // �ڽ� ���� �ö󰡴� ��ġ�� duration �ð� ���� �̵�
    public void MoveToBoxTop(float duration)
    {
        float upOffset = 1f; // �⺻��

        // �� �������� �� ����
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, climbCheckDistance, climbableLayer))
        {
            float wallTopY = hit.collider.bounds.max.y;
            float playerY = transform.position.y;
            float wallHeight = Mathf.Max(0f, wallTopY - playerY); // ���� ����
            upOffset = wallHeight * 0.5f;

            // ��ü ���̸� ����
            lastBoxWallRemainingHeight = wallHeight;
        }

        Vector3 targetPos = transform.position + Vector3.up * upOffset + transform.forward * 0.3f;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    // �ڽ� ���� ������ ���� �ö󰡱�
    public void MoveToBoxTopRemaining(float duration)
    {
        float upOffset = lastBoxWallRemainingHeight * 0.5f;
        Vector3 targetPos = transform.position + Vector3.up * upOffset;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    // BoxJump �ִϸ��̼� ���� �� ȣ��Ǵ� �Լ�
    public void OnBoxJumpEnd()
    {
        rb.useGravity = true;
        rb.isKinematic = false;
        blockInput = false;
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
