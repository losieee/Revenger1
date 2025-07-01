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

    private bool isRunning;

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
        Vector3 targetMoveInput = (moveForward * v + moveRight * h).normalized;

        // 즉시 반응 Lerp
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

        // 애니메이션 파라미터
        Vector3 localMove = transform.InverseTransformDirection(currentMoveInput);
        moveX = Mathf.SmoothDamp(moveX, localMove.x, ref velX, smoothTime);
        moveY = Mathf.SmoothDamp(moveY, Mathf.Max(localMove.z, 0f), ref velY, smoothTime);

        animator.SetFloat("MoveX", moveX);
        animator.SetFloat("MoveY", moveY);

        float targetSpeed = isRunning ? 1f : (currentMoveInput.magnitude > 0.1f ? 0.5f : 0f);
        animator.SetFloat("Speed", targetSpeed, 0.05f, Time.deltaTime);

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
    }

    void FixedUpdate()
    {
        float moveSpeed = isRunning ? speed * runSpeed : speed;
        Vector3 move = currentMoveInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);
    }
}