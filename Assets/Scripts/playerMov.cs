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

    private bool isRunning;

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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
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
        Vector3 targetMoveInput = (moveForward * v + moveRight * h).normalized;

        // ��� ���� Lerp
        float lerpSpeed = 15f;
        if (Vector3.Dot(currentMoveInput, targetMoveInput) < -0.1f)
        {
            lerpSpeed = 7f;
        }
        currentMoveInput = Vector3.Lerp(currentMoveInput, targetMoveInput, Time.deltaTime * lerpSpeed);

        // Deadzone
        if (currentMoveInput.magnitude < 0.05f)
        {
            currentMoveInput = Vector3.zero;
        }

        // �ִϸ��̼� �Ķ����
        Vector3 localMove = transform.InverseTransformDirection(currentMoveInput);
        moveX = Mathf.SmoothDamp(moveX, localMove.x, ref velX, smoothTime);
        moveY = Mathf.SmoothDamp(moveY, Mathf.Max(localMove.z, 0f), ref velY, smoothTime);

        animator.SetFloat("MoveX", moveX);
        animator.SetFloat("MoveY", moveY);

        float targetSpeed = isRunning ? 1f : (currentMoveInput.magnitude > 0.1f ? 0.5f : 0f);
        animator.SetFloat("Speed", targetSpeed, 0.05f, Time.deltaTime);

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
    }

    void FixedUpdate()
    {
        float moveSpeed = isRunning ? speed * runSpeed : speed;
        Vector3 move = currentMoveInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);
    }
}