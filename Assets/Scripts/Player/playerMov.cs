using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using static UnityEditor.Progress;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMov : MonoBehaviour
{
    // ������Ʈ
    public Rigidbody rb;
    public GameObject gameClearUI;
    public GameObject gameOverUI;
    public GameObject missionUI;
    public GameObject optionUI;
    public GameObject nearNPC;
    private Animator animator;

    // �̵� �� ȸ��
    public float speed = 5f;
    private float currentMoveSpeed = 0f;
    public float rotSpeed = 5f;
    public float runSpeed = 3f;
    public Transform cameraPivot;

    private Vector3 currentMoveInput;
    private bool isRunning;
    private bool canAttack;
    private bool canTakeMission;
    private bool canRun = true;
    private bool escStop = false;
    private bool canKill = false;

    private float moveX, moveY, velX, velY;
    private float smoothTime = 0.05f;

    // �ٴ� ���� (BoxCollider ��� + �ڿ��� Ÿ��)
    private BoxCollider box;
    private EnemyMov killTarget = null;        // �ϻ� ���
    private float boxGroundExtra = 0.1f;       // �ٴڱ��� ���� ĳ��Ʈ �Ÿ�
    private float edgeProbeOffset = 0.18f;     // ��/��/��/�� ���� ���κ� ������
    private float groundedCoyoteTime = 0.12f;  // ���� �ð�
    private float groundedTimer = 0f;

    // ���� ���� ����
    [SerializeField] private float minAirTimeForLand = 0.12f; // �ּ� ���߽ð�
    [SerializeField] private float landMinDownVel = -0.4f;    // �ϰ��ӵ� �Ӱ�
    private float timeSinceLeftGround = 999f;                  // ���� �� ����ð�

    // ��Ÿ��
    public float climbDuration = 3.25f;
    public float climbCheckDistance = 2.0f;
    public LayerMask climbableLayer;
    private bool canClimbZone = false;
    private bool isClimbing = false;
    private bool blockInput = false;
    private float lastBoxWallRemainingHeight = 0f;

    // �����̴� �Ҹ� ����
    public float walkDetectRange = 6f;
    public float runDetectRange = 12f;
    public LayerMask aiLayerMask;

    // �� �Ŵ޸���
    [HideInInspector] public float detectedWallHeight = 0f;
    [HideInInspector] public float remainingWallHeight = 0f;
    private bool isHolding = false;
    private bool canStartClimb = false;
    private bool isLerpingHoldOffset = false;
    private Vector3 holdLerpStartPos, holdLerpTargetPos;
    private Quaternion holdLerpStartRot, holdLerpTargetRot;
    private float holdLerpTimer = 0f;
    private float holdLerpDuration = 0.1f;
    private Vector3 holdingStartPos;

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
    private float fallTimer = 0f;
    [SerializeField] private float fallDebounce = 0.10f; // 100ms

    public LayerMask groundLayer;                 // '��' ���̾ ����� ���� ����
    [Range(0f, 1f)] public float groundMinNormalY = 0.55f; // ��� ���(~56��)

    // ���� ���� �պ� üũ(���� ���� ��)
    public float frontCheckDistance = 0.35f;
    public float wallPushStrength = 2.0f; // ���� ���� ��¦ �پ��� ���� ���� �о��

    // Alt �̵�
    private Vector3 savedForward, savedRight;
    private bool wasAltPressedLastFrame, justReleasedAlt;

    // �ɱ�
    private bool isCrouching = false;
    [SerializeField] private float crouchCooldown = 0.6f;       // �ɱ� ��Ÿ��
    private float crouchCooldownTimer = 0f;

    // Tag ��� �� ���� ����(Keep-Out)
    [Header("Wall Keep-Out (by Tag)")]
    [SerializeField] private string wallTag = "Wall";           // �±׸�
    [SerializeField] private float wallKeepOutRadius = 0.4f;    // �� �ּ� ���� �Ÿ�(XZ ����)
    [SerializeField] private int wallKeepOutIterations = 2;     // �𼭸�/���ߺ� ���� �ݺ�
    [SerializeField] private float wallKeepOutSkin = 0.01f;     // ��¦ ����
    [SerializeField] private float wallKeepOutUnderFootTolerance = 0.03f;   // �߹ٴں��� �Ʒ� ǥ�� ���� ���ġ

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        Time.timeScale = 1f;

        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        box = GetComponent<BoxCollider>();
        
        groundLayer = LayerMask.GetMask("Ground", "Climbable");
    }

    void Update()
    {
        // �ٴ� ����
        isGrounded = CheckGrounded();

        // �������� ���� ���� Ÿ�̸� ����/����
        if (wasGroundedLastFrame && !isGrounded)
            timeSinceLeftGround = 0f;
        if (!isGrounded)
            timeSinceLeftGround += Time.deltaTime;

        // �ɱ� Ÿ�̸� ����
        if (crouchCooldownTimer > 0f)
            crouchCooldownTimer -= Time.deltaTime;

        // �� ���� ���� ó��(���� ��)
        if (isLerpingHoldOffset)
        {
            holdLerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(holdLerpTimer / holdLerpDuration);
            transform.position = Vector3.Lerp(holdLerpStartPos, holdLerpTargetPos, t);
            transform.rotation = Quaternion.Slerp(holdLerpStartRot, holdLerpTargetRot, t);
            if (t >= 1f) isLerpingHoldOffset = false;
            return;
        }

        // �Ŵ޸� ����
        if (isHolding)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
            animator.SetFloat("Speed", 0f);
            if (Input.GetKeyDown(KeyCode.Space) && canStartClimb)
                StartClimbFromHold(0.52f);
            return;
        }

        // ������ ��
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
            }
            return;
        }

        // �Է�
        isRunning = canRun && Input.GetKey(KeyCode.LeftShift) && !isCrouching;
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
        }

        if (wasAltPressedLastFrame && !isAlt)
        {
            cameraPivot.GetComponent<CameraMov>()?.RecenterToPlayerSmooth(0.2f);
            justReleasedAlt = true;
        }

        Vector3 moveForward = (isAlt || justReleasedAlt) ? savedForward : camForward;
        Vector3 moveRight = (isAlt || justReleasedAlt) ? savedRight : camRight;

        // �̵� �Է� (blockInput �̸� 0)
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

        // �� ��� ���� ����
        if (Input.GetKeyDown(KeyCode.Space) && canClimbZone && !isHolding && !isClimbing)
        {
            Vector3 dir = transform.forward;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f; // �߹�

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
        if (Input.GetKeyDown(KeyCode.Space) && !canClimbZone && isGrounded && !isJumping && jumpCooldownTimer <= 0f && !isCrouching && canRun)
        {
            ClearLandTriggers();
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

            // �տ� ���� ������(�±� wall) �������θ� ��¦ �о
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward,
                                out RaycastHit front, frontCheckDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                ApplyWallPush(ref velocity, front);
            }

            rb.velocity = velocity;
        }

        // ���� ���� Grounded ���� Ÿ�̸�
        if (ignoreGroundedCheck)
        {
            ignoreGroundedTimer -= Time.deltaTime;
            if (ignoreGroundedTimer <= 0f)
                ignoreGroundedCheck = false;
        }

        // ���� ��Ÿ��
        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        // ���� ����
        verticalVelocity = rb.velocity.y;
        bool wantFall = (verticalVelocity < -0.1f) && !isGrounded;

        if (wantFall) fallTimer += Time.deltaTime;
        else fallTimer = 0f;

        bool isFallingAnim = fallTimer > fallDebounce;
        animator.SetBool("IsFalling", isFallingAnim);

        // ���� Ʈ����(������ ����)
        if (!wasGroundedLastFrame && isGrounded
                && timeSinceLeftGround >= minAirTimeForLand
                && verticalVelocity <= landMinDownVel
                && !ignoreGroundedCheck)
        {
            animator.SetTrigger("Land");
            animator.SetBool("IsJumping", false);
            animator.SetBool("IsFalling", false);
            fallTimer = 0f;             // ���� Ÿ�̸� ����
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
        if (Input.GetKeyDown(KeyCode.C) && crouchCooldownTimer <= 0f)
        {
            isCrouching = !isCrouching;
            animator.SetBool("IsCrouching", isCrouching);

            // CŰ ��Ÿ�� ����
            crouchCooldownTimer = crouchCooldown;

            // ȿ����
            SoundManager.i?.PlaySFX(PlayerSfx.CrouchToggle, SfxBus.Effect, 1f);
        }

        // �ӵ� ����
        float moveSpeed = isRunning ? speed * runSpeed : speed;
        if (isCrouching) moveSpeed *= 0.6f;
        currentMoveSpeed = moveSpeed;

        // �����̴� �Ҹ� ����
        CheckNearbyEnemies();

        // Ŭ����
        if (canAttack && Input.GetMouseButtonDown(0))
            ShowPausePanel(gameClearUI);

        // �̼� �ޱ�
        if (canTakeMission && Input.GetKeyDown(KeyCode.E))
            ShowPausePanel(missionUI);

        // ESC�� �ɼ�â ���
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            escStop = !escStop;

            if (escStop)
                ShowPausePanel(optionUI);
            else
                HidePausePanel(optionUI);
        }
        if (escStop)
            return;

        // �ϻ�
        if (canKill && Input.GetKeyDown(KeyCode.E))
        {
            if (killTarget != null)
            {
                killTarget.Kill();
                canKill = false;
                killTarget = null;
            }
        }
    }

    // ���� ���� �պ� �о��(�±� wall ���)
    private void ApplyWallPush(ref Vector3 velocity, RaycastHit front)
    {
        // �ٴ�ó�� ���� ���� ���̸� ����
        bool isGroundLike = ((1 << front.collider.gameObject.layer) & groundLayer) != 0
                            && front.normal.y >= groundMinNormalY;
        if (isGroundLike) return;

        // �±� �˻� (wallTag���� ����)
        if (!front.collider.CompareTag(wallTag)) return;

        // ���� �������θ� �о
        Vector3 horizNormal = new Vector3(front.normal.x, 0f, front.normal.z);
        if (horizNormal.sqrMagnitude > 0.0001f)
        {
            horizNormal.Normalize();
            velocity += horizNormal * wallPushStrength;
        }

        // ��� Grounded ���� �� Land ���� ����
        ignoreGroundedCheck = true;
        ignoreGroundedTimer = Mathf.Max(ignoreDurationAfterJump, minAirTimeForLand);
    }

    // �ִϸ����� Ʈ����/���� �ʱ�ȭ
    private void ClearLandTriggers()
    {
        animator.ResetTrigger("Land");
    }

    // �ٴ� üũ(�ڿ��� Ÿ��)
    private bool CheckGrounded()
    {
        if (ignoreGroundedCheck) return false;

        bool touching = BoxGroundProbeMulti();

        // �ڿ��� Ÿ��: ��� �������� ����
        groundedTimer = touching ? groundedCoyoteTime : groundedTimer - Time.deltaTime;
        return groundedTimer > 0f;
    }

    // ���� ������Ʈ(�̵�/���� ����)
    void FixedUpdate()
    {
        bool block = isClimbing || isHolding;
        if (block)
        {
            return;
        }

        // �̵� ó��
        airMultiplier = isGrounded ? 1f : 0.5f;

        Vector3 move = currentMoveInput * currentMoveSpeed * airMultiplier * Time.fixedDeltaTime;

        // ���� ��ǥ ��ġ
        Vector3 newPos = rb.position + move;

        // �±װ� wall�� ������Ʈ�� wallKeepOutRadius ���Ϸ� ���� ����(����)
        EnforceWallKeepOut(ref newPos);

        rb.MovePosition(newPos);
    }

    // Tag=wall ���� ����(Keep-Out) ���� (���� XZ��)
    private void EnforceWallKeepOut(ref Vector3 pos)
    {
        // �÷��̾� �߹ٴ� ���� Y (BoxCollider ����)
        float footY = box ? box.bounds.min.y : transform.position.y;

        for (int it = 0; it < wallKeepOutIterations; it++)
        {
            Collider[] hits = Physics.OverlapSphere(
                pos,
                wallKeepOutRadius + wallKeepOutSkin,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            bool adjusted = false;

            foreach (var col in hits)
            {
                if (!col) continue;
                if (col.isTrigger) continue;
                if (col.attachedRigidbody == rb) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (!col.CompareTag(wallTag)) continue;

                // '�߹ٴں��� �Ʒ�'�� �ִ� ǥ���� �о�� ����
                Vector3 query = new Vector3(pos.x, footY, pos.z);
                Vector3 cp = col.ClosestPoint(query);
                if (cp.y <= footY - wallKeepOutUnderFootTolerance ||
                    col.bounds.max.y <= footY - wallKeepOutUnderFootTolerance)
                {
                    continue; // �̹� ���� ���� �����Ƿ� ���� ����
                }

                // ���� �Ÿ� �������θ� �и�
                Vector3 delta = new Vector3(pos.x - cp.x, 0f, pos.z - cp.z);
                float d = delta.magnitude;
                if (d < wallKeepOutRadius)
                {
                    Vector3 n;
                    if (d > 1e-4f) n = delta / d;
                    else
                    {
                        Vector3 fallback = new Vector3(
                            pos.x - col.bounds.center.x, 0f, pos.z - col.bounds.center.z
                        );
                        n = (fallback.sqrMagnitude > 1e-6f) ? fallback.normalized : transform.forward;
                    }

                    float push = (wallKeepOutRadius - d) + wallKeepOutSkin;
                    pos += n * push;
                    adjusted = true;
                }
            }

            if (!adjusted) break;
        }
    }

    // �ٴ� ����(BoxCast ��Ƽ ���κ�)
    private bool BoxGroundProbeMulti()
    {
        if (!box) return false;

        // BoxCast �Ķ���� ��� (���� ����)
        Vector3 center = box.transform.TransformPoint(box.center);
        Vector3 lossy = box.transform.lossyScale;

        // �ڽ� ���� ũ��(����)
        Vector3 half = new Vector3(
            Mathf.Abs(box.size.x * 0.5f * lossy.x),
            Mathf.Abs(box.size.y * 0.5f * lossy.y),
            Mathf.Abs(box.size.z * 0.5f * lossy.z)
        );

        // ��¦ ����� �ڱ� �ڽ�/���鿡 ������ �� ����
        Vector3 halfShrink = new Vector3(
            Mathf.Max(half.x - 0.01f, 0.001f),
            Mathf.Max(half.y - 0.01f, 0.001f),
            Mathf.Max(half.z - 0.01f, 0.001f)
        );

        // ������ ���� ���� ���� �����(��ħ ����), �Ʒ��� ĳ��Ʈ
        float skin = 0.02f;
        Vector3 start = center + Vector3.up * skin;
        float distance = half.y + boxGroundExtra + skin;
        Quaternion rot = box.transform.rotation;

        bool Probe(Vector3 o)
        {
            if (Physics.BoxCast(o, halfShrink, Vector3.down, out RaycastHit hit, rot, distance, groundLayer, QueryTriggerInteraction.Ignore))
            {
                // ��� ���ġ �̻� �������� ����
                return hit.normal.y >= groundMinNormalY;
            }
            return false;
        }

        // ���� + ��/��/��/�� 4���� ���� ���κ�
        if (Probe(start)) return true;

        Vector3 fwd = transform.forward; fwd.y = 0; fwd.Normalize();
        Vector3 right = transform.right; right.y = 0; right.Normalize();
        float off = edgeProbeOffset;

        if (Probe(start + fwd * off)) return true;
        if (Probe(start - fwd * off)) return true;
        if (Probe(start + right * off)) return true;
        if (Probe(start - right * off)) return true;

        return false;
    }

    // �� ���(Holding) ����
    void StartHolding(RaycastHit hit)
    {
        ClearLandTriggers();
        isCrouching = false;
        animator.SetBool("IsCrouching", false);

        // �� ���� ����
        float wallTop = hit.collider.bounds.max.y;
        float playerFoot = transform.position.y;
        float wallHeight = wallTop - playerFoot;

        // ���� ���� ���� (�ٸ� ��ũ��Ʈ���� ����)
        remainingWallHeight = wallHeight;

        // ���� ���̸� BoxJump(�� ������ ���� �־��ٸ� �Բ� ����)
        if (wallHeight <= 1.0f)
        {
            StartBoxJump(hit.point, hit.normal, wallHeight);
            return;
        }

        // �Ϲ� Hold
        blockInput = true;
        isHolding = true;
        canStartClimb = false;
        rb.useGravity = false;
        rb.isKinematic = true;

        float holdDistanceFromWall = 0.14f;

        // �� ���� ���� ���� ��ġ
        Vector3 wallNormal = hit.normal;
        Vector3 targetPos = hit.point + wallNormal * holdDistanceFromWall;
        targetPos.y = transform.position.y; // ���� Y ����

        // ȸ��
        Quaternion targetRot = Quaternion.LookRotation(-wallNormal);

        // ����
        holdLerpStartPos = transform.position;
        holdLerpTargetPos = targetPos;
        holdLerpStartRot = transform.rotation;
        holdLerpTargetRot = targetRot;
        holdLerpTimer = 0f;
        isLerpingHoldOffset = true;

        holdingStartPos = targetPos;

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
        float climbHeight = Mathf.Clamp(detectedWallHeight + 0.15f, 1f, 3.5f);

        climbTargetPos = holdingStartPos + Vector3.up * climbHeight;
        climbTargetRot = transform.rotation;

        animator.SetBool("Hold", false);
        animator.SetTrigger("Climb");

        isClimbing = true;
        rb.useGravity = false;
    }

    // Ʈ���� �浹
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DonRun"))
            canRun = false;

        if (other.CompareTag("ClimbZone"))
            canClimbZone = true;

        if (other.CompareTag("Boss"))
            canAttack = true;

        if (other.CompareTag("NPC"))
        {
            nearNPC.SetActive(true);
            canTakeMission = true;
        }

        if (other.CompareTag("Discorver"))
            ShowPausePanel(gameOverUI);

        if (other.CompareTag("Attack"))
        {
            var enemy = other.GetComponentInParent<EnemyMov>();
            if (enemy == null) enemy = other.GetComponent<EnemyMov>();
            if (enemy != null)
            {
                killTarget = enemy;
                canKill = true;
            }
        }

    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("ClimbZone"))
            canClimbZone = false;

        if (other.CompareTag("Boss"))
            canAttack = false;

        if (other.CompareTag("NPC"))
        {
            nearNPC.SetActive(false);
            canTakeMission = false;
        }
        if (other.CompareTag("Attack"))
        {
            var enemy = other.GetComponentInParent<EnemyMov>();
            if (enemy == null) enemy = other.GetComponent<EnemyMov>();
            if (enemy == killTarget)
            {
                killTarget = null;
                canKill = false;
            }
        }
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

        // ���� ����
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

    // �Ŵ޸��� �� �ܰ� ���
    public void MoveUpDuringHold(float height, float duration)
    {
        if (!isHolding) return;
        StartCoroutine(MoveHoldWithDip(height, duration));
    }

    private IEnumerator MoveHoldWithDip(float height, float duration)
    {
        // 1) ��¦ �Ʒ���
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

        // 2) ���� �ø���
        Vector3 upTarget = start + new Vector3(0f, height, 0.01f);
        t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(downPos, upTarget, t);
            yield return null;
        }

        // Climb ����
        canStartClimb = true;
    }

    // �� �ȶո��� ������ �̵�
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
    }

    public void OnClimbEnd()
    {
        isClimbing = false;
        rb.useGravity = true;
        rb.isKinematic = false;

        rb.velocity = Vector3.zero;
        blockInput = false;
    }

    // ������ �Ҹ� ����
    void CheckNearbyEnemies()
    {
        if (isCrouching) return; // �ɾ������� �Ҹ� ���� X
        if (currentMoveInput.magnitude < 0.05f) return;

        float detectRange = isRunning ? runDetectRange : walkDetectRange;
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange, aiLayerMask);

        foreach (Collider col in hits)
        {
            EnemyMov enemy = col.GetComponent<EnemyMov>();
            if (enemy != null)
                enemy.PlayerDetected(transform.position);
        }
    }

    // ���� �� BoxJump
    void StartBoxJump(Vector3 wallPoint, Vector3 wallNormal, float height)
    {
        ClearLandTriggers();
        isCrouching = false;
        animator.SetBool("IsCrouching", false);

        blockInput = true;
        isHolding = false;
        isClimbing = false;
        rb.useGravity = false;

        Vector3 targetPos = wallPoint + wallNormal * 0.14f;
        targetPos.y = transform.position.y;
        Quaternion targetRot = Quaternion.LookRotation(-wallNormal);

        StartCoroutine(BoxJumpPrepareLerp(targetPos, targetRot, 0.15f));
    }

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

        animator.Play("BoxJump");
    }

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

        rb.useGravity = true;
        rb.isKinematic = false;
        blockInput = false;
    }

    public void MoveToBoxTop(float duration)
    {
        float upOffset = 1f; // �⺻��

        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, climbCheckDistance, climbableLayer))
        {
            float wallTopY = hit.collider.bounds.max.y;
            float playerY = transform.position.y;
            float wallHeight = Mathf.Max(0f, wallTopY - playerY);
            upOffset = wallHeight * 0.5f;

            lastBoxWallRemainingHeight = wallHeight;
        }

        Vector3 targetPos = transform.position + Vector3.up * upOffset + transform.forward * 0.3f;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    public void MoveToBoxTopRemaining(float duration)
    {
        float upOffset = lastBoxWallRemainingHeight * 0.5f;
        Vector3 targetPos = transform.position + Vector3.up * upOffset;
        StartCoroutine(BoxJumpLerp(targetPos, duration));
    }

    public void OnBoxJumpEnd()
    {
        rb.useGravity = true;
        rb.isKinematic = false;
        blockInput = false;
    }

    // �����
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, walkDetectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, runDetectRange);

        // Keep-Out �ݰ� �ð�ȭ(�����)
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, wallKeepOutRadius);
    }

    // UI ǥ��/����
    void ShowPausePanel(GameObject panel)
    {
        if (!panel) return;

        panel.SetActive(true);

        var cg = panel.GetComponent<CanvasGroup>();
        if (!cg) cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        foreach (var anim in panel.GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (!EventSystem.current)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        AudioListener.pause = true;
        Time.timeScale = 0f;
    }

    void HidePausePanel(GameObject panel)
    {
        if (!panel) return;

        var cg = panel.GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 0f;
        }
        panel.SetActive(false);

        AudioListener.pause = false;
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
