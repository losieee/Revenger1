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

    // 벽타기 준비물
    public float climbDuration = 3.25f;
    public float climbCheckDistance = 2.0f;
    [HideInInspector] public float remainingWallHeight = 0f;    // 벽 높이
    public LayerMask climbableLayer;
    private bool canClimbZone = false;
    private bool isClimbing = false;
    private bool blockInput = false; // 벽 오르기 동안 입력 차단
    private float lastBoxWallRemainingHeight = 0f; // MoveToBoxTop에서 감지된 전체 높이 저장

    // 움직이는 소리 범위
    public float walkDetectRange = 6f;
    public float runDetectRange = 12f;
    public LayerMask aiLayerMask;

    // 벽에 매달려있기
    [HideInInspector] public float detectedWallHeight = 0f;
    private bool isHolding = false;
    private bool canStartClimb = false;
    private bool isLerpingHoldOffset = false;
    private Vector3 holdLerpStartPos, holdLerpTargetPos;
    private Quaternion holdLerpStartRot, holdLerpTargetRot;
    private float holdLerpTimer = 0f;
    private float holdLerpDuration = 0.1f;
    private Vector3 holdingStartPos; // 매달리기 시작 시 위치 저장

    private Vector3 climbStartPos, climbTargetPos;
    private Quaternion climbStartRot, climbTargetRot;
    private float climbTimer = 0f;

    // 점프 / 낙하
    private bool isJumping = false;
    private float jumpForce = 5f;
    private float verticalVelocity = 0f;
    private bool isLanding = false;
    private float landingTimer = 0f;
    private float landingDelay = 0.6f;
    private bool isGrounded = true;
    private bool wasGroundedLastFrame = true;
    private float jumpCooldown = 1.9f; // 점프 쿨타임
    private float jumpCooldownTimer = 0f;
    private float airMultiplier;
    private bool ignoreGroundedCheck = false;
    private float ignoreGroundedTimer = 0f;
    private float ignoreDurationAfterJump = 0.25f; // 점프 직후 잠깐 지면 판정 무시

    public LayerMask groundLayer;                 // '땅' 레이어에 닿았을 때만 착지
    [Range(0f, 1f)] public float groundMinNormalY = 0.55f; // 허용 경사(~56˚)

    // (옵션) 점프 순간 앞벽이 바짝 있으면 살짝 밀어내기
    public float frontCheckDistance = 0.35f;
    public float wallPushStrength = 2.0f;

    // Alt 이동
    private Vector3 savedForward, savedRight;
    private Quaternion savedRotation;
    private bool wasAltPressedLastFrame, justReleasedAlt;

    // 앉기
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
        // space를 누르자마자 Land 트리거 제거
        if (Input.GetKeyDown(KeyCode.Space))
            ClearLandTriggers();

        // 바닥 감지 - SphereCast 방식(groundLayer + normal.y 기준)
        isGrounded = CheckGrounded();

        // 벽 붙은 상태 처리
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

            // climb 중 입력 무시
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

        // 입력
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

        // blockInput 중 입력 이동 차단
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
            // Climb 중에는 이동 입력 정지
            currentMoveInput = Vector3.zero;
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

        // 회전
        if (currentMoveInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * 100f * Time.deltaTime);
        }

        wasAltPressedLastFrame = isAlt;
        if (justReleasedAlt && !isAlt) justReleasedAlt = false;

        // 벽 잡기(Holding) 시작 조건 검사
        if (Input.GetKeyDown(KeyCode.Space) && canClimbZone && !isHolding && !isClimbing)
        {
            Vector3 dir = transform.forward;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f; // ← 높이 조절 (발밑)

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

        // 점프
        if (Input.GetKeyDown(KeyCode.Space) && !canClimbZone && isGrounded && !isJumping && jumpCooldownTimer <= 0f && !isCrouching)
        {
            ClearLandTriggers();        // Land 트리거 초기화
            isJumping = true;
            jumpCooldownTimer = jumpCooldown;
            animator.SetBool("IsJumping", true);

            // 점프 직후 Grounded 무시
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

            // (옵션) 앞에 벽이 바짝 있으면 살짝 밀어내기 → 들러붙음/착지 오인 감소
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

        // 점프 직후 잠깐 Grounded 무시
        if (ignoreGroundedCheck)
        {
            ignoreGroundedTimer -= Time.deltaTime;
            if (ignoreGroundedTimer <= 0f)
            {
                ignoreGroundedCheck = false;
            }
        }

        // 점프 쿨타임 감소
        if (jumpCooldownTimer > 0f)
        {
            jumpCooldownTimer -= Time.deltaTime;
        }

        // 낙하 감지
        verticalVelocity = rb.velocity.y;
        bool isFalling = verticalVelocity < -0.1f && !isGrounded;
        animator.SetBool("IsFalling", isFalling);

        // 착지 감지(프레임 전이)
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

        // C 눌러 앉기
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
            animator.SetBool("IsCrouching", isCrouching);
        }

        // 속도 조정
        float moveSpeed = isRunning ? speed * runSpeed : speed;
        if (isCrouching) moveSpeed *= 0.6f;
        currentMoveSpeed = moveSpeed;

        // 움직이는 소리 범위
        CheckNearbyEnemies();
    }

    // 애니메이터 트리거/상태 초기화
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
            // 위를 향한 면만 지면으로 인정
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

        // 이동 처리
        airMultiplier = isGrounded ? 1f : 0.5f;

        Vector3 move = currentMoveInput * currentMoveSpeed * airMultiplier * Time.fixedDeltaTime;
        Vector3 newPos = rb.position + move;
        rb.MovePosition(rb.position + move);

        float movedDistance = (newPos - lastFixedPosition).magnitude;
        lastFixedSpeed = movedDistance / Time.fixedDeltaTime;
        lastFixedPosition = newPos;
    }

    // 벽 잡기(Holding) 시작
    void StartHolding(RaycastHit hit)
    {
        ClearLandTriggers();
        isCrouching = false;  // 벽 잡을 때 자동으로 서도록 처리
        animator.SetBool("IsCrouching", false);

        // 벽 높이 측정
        float wallTop = hit.collider.bounds.max.y;
        float playerFoot = transform.position.y;
        float wallHeight = wallTop - playerFoot;

        // 남은 높이 계산 후 저장
        remainingWallHeight = wallHeight;

        // 높이가 1 이하라면 BoxJump 실행
        if (wallHeight <= 1.0f)
        {
            StartBoxJump(hit.point, hit.normal, wallHeight);
            return;
        }

        // 일반 벽타기 (Hold 시작)
        blockInput = true;
        isHolding = true;
        canStartClimb = false;
        rb.useGravity = false;
        rb.isKinematic = true;

        float holdDistanceFromWall = 0.14f;

        // 1. 벽의 법선 방향으로 밀착 위치 계산 (Y는 현재 유지)
        Vector3 wallNormal = hit.normal;

        // 입력과 상관없이 벽 기준 정중앙 위치 계산
        Vector3 targetPos = hit.point + wallNormal * holdDistanceFromWall;
        targetPos.y = transform.position.y; // 현재 Y 유지

        // 2. 회전
        Quaternion targetRot = Quaternion.LookRotation(-wallNormal);

        // 3. 적용
        holdLerpStartPos = transform.position;
        holdLerpTargetPos = targetPos;
        holdLerpStartRot = transform.rotation;
        holdLerpTargetRot = targetRot;
        holdLerpTimer = 0f;
        isLerpingHoldOffset = true;

        holdingStartPos = targetPos;    // 매달려있는 위치 저장

        animator.SetBool("Hold", true);
    }

    // 벽 오르기(Climb) 시작
    public void StartClimbFromHold(float duration)
    {
        ClearLandTriggers();
        isHolding = false;
        canStartClimb = false;
        climbTimer = 0f;
        climbDuration = duration;

        climbStartPos = transform.position;
        climbStartRot = transform.rotation;

        // 벽 높이에 따라 올라갈 높이 계산
        float climbHeight = Mathf.Clamp(detectedWallHeight + 0.15f, 1f, 3.5f); // 약간 여유 줘야 착지 성공

        climbTargetPos = holdingStartPos + Vector3.up * climbHeight;
        climbTargetRot = transform.rotation;

        animator.SetBool("Hold", false);
        animator.SetTrigger("Climb");

        isClimbing = true;
        rb.useGravity = false;
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

    // 지면 접촉 판단(착지 처리에만 사용)
    private bool IsGroundContact(Collision col)
    {
        // groundLayer에 포함된 레이어인지 확인
        if ((groundLayer.value & (1 << col.gameObject.layer)) == 0) return false;

        // 위를 향한 면만 지면으로 인정
        foreach (var c in col.contacts)
            if (c.normal.y >= groundMinNormalY) return true;

        return false;
    }

    private void OnCollisionStay(Collision collision)
    {
        // 점프 직후 무시 시간에는 어떤 충돌도 착지로 처리하지 않음
        if (ignoreGroundedCheck) return;

        // 지면과의 접촉이 아니면(=벽/측면) 무시
        if (!IsGroundContact(collision)) return;

        // 여기서는 상태 보정만 하고, 트리거는 Update의
        //  (!wasGroundedLastFrame && isGrounded) 구간에서만 건다.
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

    // 벽 매달리기 위한 높이,시간 계산
    public void MoveUpDuringHold(float height, float duration)
    {
        if (!isHolding) return;

        StartCoroutine(MoveHoldWithDip(height, duration));
    }

    // 벽 매달리기 전 단계
    private IEnumerator MoveHoldWithDip(float height, float duration)
    {
        // 1단계: 살짝 아래로 내리기 (무릎 굽히기)
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

        // 2단계: 위로 올리기 (점프)
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

        // 이동 시작점 갱신
        lastFixedPosition = rb.position;
    }
    public void OnClimbEnd()
    {
        isClimbing = false;
        rb.useGravity = true;
        rb.isKinematic = false;

        rb.velocity = Vector3.zero; // 혹시 튀는 이동 방지
        blockInput = false;
    }

    // 움직임 소리 범위
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

    // 낮은 벽을 BoxJump 애니메이션으로 넘기기
    void StartBoxJump(Vector3 wallPoint, Vector3 wallNormal, float height)
    {
        ClearLandTriggers();
        // 앉은 상태 해제
        isCrouching = false;
        animator.SetBool("IsCrouching", false);

        // 이동 및 중력 제어
        blockInput = true;
        isHolding = false;
        isClimbing = false;
        rb.useGravity = false;

        // 타겟 위치 및 회전 계산
        Vector3 targetPos = wallPoint + wallNormal * 0.14f;
        targetPos.y = transform.position.y;
        Quaternion targetRot = Quaternion.LookRotation(-wallNormal);

        // 밀착을 부드럽게 처리하는 코루틴 시작
        StartCoroutine(BoxJumpPrepareLerp(targetPos, targetRot, 0.15f));
    }

    // 벽 앞에 부드럽게 밀착시키는 코루틴
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

        // 위치 정렬 완료 후 애니메이션 재생
        animator.Play("BoxJump");
    }

    // BoxJump 애니메이션 중 위치를 부드럽게 이동
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

        // 이동 종료 후 상태 복원
        rb.useGravity = true;
        rb.isKinematic = false;
        blockInput = false;
        lastFixedPosition = rb.position;
    }

    // 박스 위로 올라가는 위치로 duration 시간 동안 이동
    public void MoveToBoxTop(float duration)
    {
        float upOffset = 1f; // 기본값

        // 앞 방향으로 벽 감지
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, climbCheckDistance, climbableLayer))
        {
            float wallTopY = hit.collider.bounds.max.y;
            float playerY = transform.position.y;
            float wallHeight = Mathf.Max(0f, wallTopY - playerY); // 음수 방지
            upOffset = wallHeight * 0.5f;

            // 전체 높이를 저장
            lastBoxWallRemainingHeight = wallHeight;
        }

        Vector3 targetPos = transform.position + Vector3.up * upOffset + transform.forward * 0.3f;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    // 박스 위로 나머지 절반 올라가기
    public void MoveToBoxTopRemaining(float duration)
    {
        float upOffset = lastBoxWallRemainingHeight * 0.5f;
        Vector3 targetPos = transform.position + Vector3.up * upOffset;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    // BoxJump 애니메이션 종료 시 호출되는 함수
    public void OnBoxJumpEnd()
    {
        rb.useGravity = true;
        rb.isKinematic = false;
        blockInput = false;
    }

    // 범위 기즈모
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, walkDetectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, runDetectRange);
    }
}
