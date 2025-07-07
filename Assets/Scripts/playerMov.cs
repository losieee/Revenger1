using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMov : MonoBehaviour
{
    private Rigidbody rb;
    private Animator animator;

    public float speed = 5f;
    public float rotSpeed = 5f;
    public Transform cameraPivot; // CameraMov�� �پ��ִ� Transform
    public float runSpeed = 3f;

    private Vector3 moveInput;
    private Vector3 currentMoveInput;
    private float lastFixedSpeed = 0f;
    private Vector3 lastFixedPosition;

    private bool isRunning;
    private bool canClimbZone = false;
    private bool isGrounded = true;

    // Alt ���� ���� ����
    private Vector3 savedForward;
    private Vector3 savedRight;
    private Quaternion savedRotation;
    private bool wasAltPressedLastFrame;
    private bool justReleasedAlt;

    // �ִϸ��̼� �Ķ���� ������
    private float moveX = 0f;
    private float moveY = 0f;
    private float velX = 0f;
    private float velY = 0f;
    private float smoothTime = 0.05f;

    //��Ÿ�� ����
    public float climbDuration = 3.25f;
    public float climbCheckDistance = 2.0f; // �ڽ� ���� �Ÿ�
    public LayerMask climbableLayer;        // ������ ���̾�

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

        // Alt ó�� ������ �� ����� ȸ�� ����
        if (isAlt && !wasAltPressedLastFrame)
        {
            savedForward = camForward;
            savedRight = camRight;
            savedRotation = transform.rotation;
        }

        // Alt ���� �� ī�޶� �÷��̾� �������� ������
        if (wasAltPressedLastFrame && !isAlt)
        {
            cameraPivot.GetComponent<CameraMov>().RecenterToPlayerSmooth(0.2f); // 0.5�ʿ� �ε巴��
            justReleasedAlt = true;
        }

        // Alt �����ų� Alt ���� ���Ŀ� ����� ���� ���
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

        // �̵� �Է�
        if (isGrounded)
        {
            Vector3 targetMoveInput = (moveForward * v + moveRight * h).normalized;

            // ��� ���� Lerp
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
            // ���߿����� �̵� �Է� ����
            currentMoveInput = Vector3.zero;
        }


        // �ִϸ��̼� �Ķ����
        Vector3 localMove = transform.InverseTransformDirection(currentMoveInput);
        moveX = Mathf.SmoothDamp(moveX, localMove.x, ref velX, smoothTime);
        moveY = Mathf.SmoothDamp(moveY, Mathf.Max(localMove.z, 0f), ref velY, smoothTime);

        animator.SetFloat("MoveX", moveX);
        animator.SetFloat("MoveY", moveY);

        // ���ϴ� ���� �ӵ�(0.05 �����̸� ���� �ɷ� ����)
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

        // ������ ������ MoveX, MoveY�� 0����
        if (speedParam == 0f)
        {
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 0f);
        }

        // ȸ��
        if (currentMoveInput.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentMoveInput);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                rotSpeed * 100f * Time.deltaTime
            );
        }

        // Alt ���� ���
        wasAltPressedLastFrame = isAlt;

        // Alt ���� �� 1������ ����
        if (justReleasedAlt && !isAlt)
        {
            justReleasedAlt = false;
        }

        //space�� ���� �� Ÿ��
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

        // �����̸� �̵� ����
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

        // ������ �̵��� �Ÿ� ����
        float movedDistance = (newPos - lastFixedPosition).magnitude;
        lastFixedSpeed = movedDistance / Time.fixedDeltaTime;

        // ���� ��ġ�� ���� �������� ���� ����
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