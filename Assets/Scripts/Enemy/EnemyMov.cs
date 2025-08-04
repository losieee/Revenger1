using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyMov : MonoBehaviour
{
    [Header("��� �̵� ����")]
    public Transform[] waypoints;               // ���� �� ���
    public float walkSpeed = 2f;                // �ȴ� �ӵ� (���� ��)
    public float runSpeed = 5f;                 // �޸��� �ӵ� (�߰� ��)
    public float waitTimeAtEnds = 1.5f;         // ��� ���� �������� �� ��� �ð�
    public float rotationSpeed = 5f;            // �ʿ� �� ���� ȸ�� �ӵ�

    [Header("�þ� ����")]
    public float viewDistance = 10f;            // ���� ������ �ִ� �Ÿ�
    public float viewAngle = 60f;               // �⺻ �þ߰� (���� ����)
    public Transform player;                    // ���� ��� (�÷��̾�)
    public GameObject questionMark;             // ����ǥ (AI�� �÷��̾ �ν����� ��)
    public GameObject answerMarkexclamationMark;// ����ǥ (AI�� �÷��̾ �߰��� ��)
    public GameObject miniQuestionMark;         // �̴ϸʿ��� ����ǥ
    public GameObject miniAnswerMark;           // �̴ϸʿ��� ����ǥ

    [Header("���� ����")]
    public float lostPlayerGraceTime = 2f;      // �÷��̾ ��ģ �� �� �ʱ��� ���� ��������


    // ���� ����
    private int currentIndex = 0;               // ���� �̵� ���� waypoint �ε���
    private int direction = 1;                  // ����: 1 = ������, -1 = ������
    private bool isWaiting = false;             // ��� �� ���� ������
    private float speed = 0f;                   // ���� �̵� �ӵ�
    private float playerStayTime = 0f;          // �þ� �ȿ� �÷��̾ �־��� ���� �ð�
    private float lostPlayerTimer = 0f;         // �÷��̾ ��ģ �� ��� �ð�
    private float originalViewAngle;            // ���� �þ߰�
    private float destinationUpdateRate = 0.2f; // �߰� �� ��ǥ ��ġ ���� ����
    private float destinationUpdateTimer = 0f;  // ���� �߰� ��ġ ���� Ÿ�̸�
    private bool isSoundTriggered = false;      // �Ҹ� ������ �߻��ߴ��� ����
    private float soundDetectTimer = 0.5f;      // �Ҹ� ���� �� ��� �ð�
    private float maxChaseBySoundTime = 3f;     // �Ҹ� ������ �̵��ϴ� �ִ� �ð�
    private float soundChaseTimer = 0f;         // �Ҹ� ������ ���� ���� �ð� ����
    private Vector3 firstHeardPosition;         // ó�� �鸰 �Ҹ��� ��ġ
    private bool hasHeardPlayer = false;        // �Ҹ� ������ �÷��̾� ���� ��ġ ��� ����
    private bool isSoundWaiting = false;        // ���� ���� ��� ���� �÷���

    // ������Ʈ ����
    private Animator animator;
    private NavMeshAgent agent;

    // Enemy ���� ����
    private enum EnemyState { Patrol, Watching, Chasing }       // ���� ��, ���(?) - �÷��̾� ���� �߰� ��, �߰�(!) - �÷��̾� ����
    private EnemyState state = EnemyState.Patrol;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        originalViewAngle = viewAngle;      //�����Ҷ��� �⺻ ��߰� 60����
        lostPlayerTimer = 0f;

        // ó�� ������ ����
        agent.speed = walkSpeed;
        agent.SetDestination(waypoints[currentIndex].position);

        agent.acceleration = 40f;
        agent.angularSpeed = 720f;
        agent.updateRotation = false;
        agent.autoBraking = false;
        agent.stoppingDistance = 0f;
    }

    void Update()
    {
        bool playerInSight = IsPlayerInSight();

        // ���¿� ���� ���� ó��
        switch (state)
        {
            case EnemyState.Patrol:
                viewAngle = originalViewAngle;  // �þ߰� ����
                Patrol();
                miniAnswerMark.SetActive(false);        // �ٽ� �ʱ�ȭ
                miniQuestionMark.SetActive(false);

                if (playerInSight)
                {
                    playerStayTime = 0f;    // �� ���� Ÿ�̸� ����
                    state = EnemyState.Watching;
                }
                break;

            case EnemyState.Watching:
                viewAngle = 360f;        // �þ߰� Ȯ��
                animator.SetFloat("Speed", 0f); // �ִϸ��̼� ����
                miniQuestionMark.SetActive(true);   // �̴ϸʿ� ��ũ ǥ��
                miniAnswerMark.SetActive(false);

                if (playerInSight)
                {
                    agent.isStopped = true;
                    playerStayTime += Time.deltaTime;
                    if (playerStayTime >= 1.5f)       // 1.5�� �̻� ������ ���� ����
                    {
                        agent.isStopped = false;
                        state = EnemyState.Chasing;
                    }
                }
                else if (isSoundTriggered)
                {
                    if (isSoundWaiting)
                    {
                        soundDetectTimer += Time.deltaTime;
                        if (soundDetectTimer >= 0.5f)
                        {
                            isSoundWaiting = false;
                            questionMark.SetActive(true);
                            agent.isStopped = false;
                            agent.SetDestination(firstHeardPosition);
                        }
                        else
                        {
                            agent.isStopped = true;
                            animator.SetFloat("Speed", 0f);
                        }
                    }
                    else
                    {
                        agent.isStopped = false;
                        animator.SetFloat("Speed", agent.velocity.magnitude);
                        soundChaseTimer += Time.deltaTime;

                        if (soundChaseTimer >= maxChaseBySoundTime)
                        {
                            ResetSoundDetection();

                            state = EnemyState.Patrol;
                            agent.SetDestination(waypoints[currentIndex].position);
                            agent.isStopped = false;
                        }
                    }
                }
                else
                {
                    playerStayTime += Time.deltaTime;
                    if (playerStayTime >= 1f)
                    {
                        state = EnemyState.Patrol;
                        agent.SetDestination(waypoints[currentIndex].position);
                        agent.isStopped = false;

                        // ���� ���� �ʱ�ȭ �߰�
                        ResetSoundDetection();
                    }
                }
                break;

            case EnemyState.Chasing:
                viewAngle = 360f;       // �þ� Ȯ��
                ChasePlayer();
                miniQuestionMark.SetActive(false);
                miniAnswerMark.SetActive(true);     // �̴ϸʿ� ��ũ ǥ��

                if (playerInSight)
                {
                    lostPlayerTimer = 0f;
                }
                else
                {
                    lostPlayerTimer += Time.deltaTime;
                    if (lostPlayerTimer >= lostPlayerGraceTime)
                    {
                        // ���� ��ȯ ���� ���� ���� ���� �ʱ�ȭ
                        ResetSoundDetection();

                        state = EnemyState.Patrol;
                        agent.SetDestination(waypoints[currentIndex].position);
                    }
                }
                break;
        }

        // ���� �̵� ���� ���� ȸ��
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }

        UpdateMark();
    }

    void LateUpdate()
    {
        // �׻� ���� Z+ ������ ���ϵ��� �̴ϸ� ��ũ ȸ�� ����
        if (miniQuestionMark != null)
        {
            miniQuestionMark.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        if (miniAnswerMark != null)
        {
            miniAnswerMark.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    void Patrol()
    {
        if (waypoints.Length < 2 || isWaiting) return;

        agent.speed = walkSpeed;

        // ������ ���� Ȯ��
        if (!agent.pathPending && agent.remainingDistance < 0.2f)
        {
            // ������ or ������ �������� ���
            if ((currentIndex == 0 && direction == -1) || (currentIndex == waypoints.Length - 1 && direction == 1))
            {
                direction *= -1; // �ݵ�� ���� ���� �ʿ�
                StartCoroutine(WaitBeforeMoving());
                return;
            }

            // ���� �ε����� �̵�
            currentIndex += direction;

            // ���� ������ ����
            agent.SetDestination(waypoints[currentIndex].position);
        }

        // �ӵ� ������ ���� �ִϸ��̼� �ε巴�� ó��
        float smoothSpeed = Mathf.Lerp(animator.GetFloat("Speed"), agent.velocity.magnitude, Time.deltaTime * 10f);
        animator.SetFloat("Speed", smoothSpeed);
    }

    // �߰� �ൿ
    void ChasePlayer()
    {
        agent.speed = runSpeed;

        destinationUpdateTimer += Time.deltaTime;
        if (destinationUpdateTimer >= destinationUpdateRate)
        {
            agent.SetDestination(player.position);
            destinationUpdateTimer = 0f;
        }

        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    // ��� ����
    IEnumerator WaitBeforeMoving()
    {
        animator.SetFloat("Speed", 0f);
        isWaiting = true;
        yield return new WaitForSeconds(waitTimeAtEnds);
        isWaiting = false;
        agent.isStopped = false;
        agent.SetDestination(waypoints[currentIndex].position);
    }

    // �÷��̾ �þ� ���� �ִ��� �˻�
    bool IsPlayerInSight()
    {
        Vector3 eyePos = transform.position + Vector3.up * 1.5f; // ������
        Vector3 dirToPlayer = player.position - eyePos;
        float distanceToPlayer = dirToPlayer.magnitude;

        if (distanceToPlayer > viewDistance)
            return false;

        // �þ߰� Ȯ�� (3D ���� ����)
        float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer.normalized);
        if (angleToPlayer > viewAngle / 2f)
            return false;

        // ��ֹ� Raycast Ȯ��
        if (Physics.Raycast(eyePos, dirToPlayer.normalized, out RaycastHit hit, viewDistance))
        {
            if (hit.transform == player)
                return true;
        }

        return false;
    }

    // AI �Ӹ����� �ߴ� ��ũ (?, !)
    void UpdateMark()
    {
        // ��� ��ũ ��Ȱ��ȭ
        questionMark.SetActive(false);
        answerMarkexclamationMark.SetActive(false);

        // ���� ���¿� ���� �ش� ��ũ Ȱ��ȭ
        GameObject activeMark = null;

        switch (state)
        {
            case EnemyState.Watching:
                activeMark = questionMark;
                break;
            case EnemyState.Chasing:
                activeMark = answerMarkexclamationMark;
                break;
        }

        if (activeMark != null)
        {
            activeMark.SetActive(true);

            // ��ũ�� �׻� ī�޶� �ٶ󺸰�
            if (activeMark != null)
            {
                activeMark.SetActive(true);

                // ��ũ�� �׻� ī�޶� �ٶ󺸰�
                if (Camera.main != null)
                {
                    activeMark.transform.LookAt(Camera.main.transform);
                    activeMark.transform.Rotate(0f, 180f, 0f);
                }
            }
        }
    }

    // �Ҹ��� �鸮�� �÷��̾� �������� �̵�
    public void PlayerDetected(Vector3 _)
    {
        // �Ҹ� ���� ������� ���(���°� Chasing�� ���� ����)
        if (state == EnemyState.Chasing)
            return;

        if (!hasHeardPlayer) // ó�� �Ҹ� ������ ���� ��ġ ���
        {
            firstHeardPosition = player.position;
            hasHeardPlayer = true;
            isSoundWaiting = true;

            // �Ҹ� ���� �ʱ�ȭ(�ߺ� ������ ���)
            isSoundTriggered = true;
            soundDetectTimer = 0f;
            soundChaseTimer = 0f;

            // �Ҹ� �鸮�� �ٷ� Watching ���·� �����ϰ�, ��ġ ����
            state = EnemyState.Watching;

            agent.isStopped = true; // ���� ����
            animator.SetFloat("Speed", 0f);
        }
    }

    void ResetSoundDetection()
    {
        isSoundTriggered = false;
        hasHeardPlayer = false;
        isSoundWaiting = false;
        soundDetectTimer = 0f;
        soundChaseTimer = 0f;
    }

    // �þ� ���� �����
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        // �þ� �Ÿ� ��
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        // 3D �þ߰� ���� �ð�ȭ
        Vector3 forward = transform.forward;
        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * forward;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, left * viewDistance);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, right * viewDistance);
    }
}
