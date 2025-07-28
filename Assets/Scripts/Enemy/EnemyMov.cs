using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyMov : MonoBehaviour
{
    [Header("경로 이동 관련")]
    public Transform[] waypoints;               // 순찰 할 경로
    public float walkSpeed = 2f;                // 걷는 속도 (순찰 중)
    public float runSpeed = 5f;                 // 달리는 속도 (추격 중)
    public float waitTimeAtEnds = 1.5f;         // 경로 끝에 도달했을 때 대기 시간
    public float rotationSpeed = 5f;            // 필요 시 수동 회전 속도

    [Header("시야 관련")]
    public float viewDistance = 10f;            // 감지 가능한 최대 거리
    public float viewAngle = 60f;               // 기본 시야각 (수평 방향)
    public Transform player;                    // 추적 대상 (플레이어)

    [Header("추적 관련")]
    public float lostPlayerGraceTime = 2f;      // 플레이어를 놓친 뒤 몇 초까지 추적 유지할지

    // 내부 상태
    private int currentIndex = 0;               // 현재 이동 중인 waypoint 인덱스
    private int direction = 1;                  // 방향: 1 = 순방향, -1 = 역방향
    private bool isWaiting = false;             // 경로 중 정지 중인지
    private float speed = 0f;                   // 현재 이동 속도
    private float playerStayTime = 0f;          // 시야 안에 플레이어가 있었던 누적 시간
    private float lostPlayerTimer = 0f;         // 플레이어를 놓친 후 경과 시간
    private float originalViewAngle;            // 원래 시야각
    private float destinationUpdateRate = 0.2f;
    private float destinationUpdateTimer = 0f;

    // 컴포넌트 참조
    private Animator animator;
    private NavMeshAgent agent;

    // Enemy 상태 정의
    private enum EnemyState { Patrol, Watching, Chasing }       // 순찰 중, 경고(?) - 플레이어 최초 발각 시, 추격(!) - 플레이어 추적
    private EnemyState state = EnemyState.Patrol;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

        originalViewAngle = viewAngle;      //시작할때는 기본 사야각 60으로
        lostPlayerTimer = 0f;

        // 처음 목적지 설정
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

        // 상태에 따른 동작 처리
        switch (state)
        {
            case EnemyState.Patrol:
                viewAngle = originalViewAngle;  // 시야각 복원
                Patrol();

                if (playerInSight)
                {
                    playerStayTime = 0f;    // 본 순간 타이머 리셋
                    state = EnemyState.Watching;
                }
                break;

            case EnemyState.Watching:
                viewAngle = 90f;        //시야각 확장
                agent.isStopped = true;
                animator.SetFloat("Speed", 0f); // 정지 상태

                if (playerInSight)
                {
                    playerStayTime += Time.deltaTime;
                    if (playerStayTime >= 1f)       //1초 이상 봤으면 추적 시작
                    {
                        agent.isStopped = false;
                        state = EnemyState.Chasing;
                    }
                }
                else
                {
                    agent.isStopped = false;
                    state = EnemyState.Patrol;
                }
                break;

            case EnemyState.Chasing:
                viewAngle = 360f;       // 시야 확대
                ChasePlayer();

                if (playerInSight)
                {
                    lostPlayerTimer = 0f;
                }
                else
                {
                    lostPlayerTimer += Time.deltaTime;
                    if (lostPlayerTimer >= lostPlayerGraceTime)
                    {
                        state = EnemyState.Patrol;
                        agent.SetDestination(waypoints[currentIndex].position);
                    }
                }
                break;
        }

        // 현재 이동 중일 때만 회전
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }

    void Patrol()
    {
        if (waypoints.Length < 2 || isWaiting) return;

        agent.speed = walkSpeed;

        // 목적지 도달 확인
        if (!agent.pathPending && agent.remainingDistance < 0.2f)
        {
            // 시작점 or 끝점에 도달했을 경우
            if ((currentIndex == 0 && direction == -1) || (currentIndex == waypoints.Length - 1 && direction == 1))
            {
                direction *= -1; // 반드시 방향 반전 필요
                StartCoroutine(WaitBeforeMoving());
                return;
            }

            // 다음 인덱스로 이동
            currentIndex += direction;

            // 다음 목적지 설정
            agent.SetDestination(waypoints[currentIndex].position);
        }

        // 속도 보간을 통해 애니메이션 부드럽게 처리
        float smoothSpeed = Mathf.Lerp(animator.GetFloat("Speed"), agent.velocity.magnitude, Time.deltaTime * 10f);
        animator.SetFloat("Speed", smoothSpeed);
    }

    // 추격 행동
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

    // 잠시 정지
    IEnumerator WaitBeforeMoving()
    {
        animator.SetFloat("Speed", 0f);
        isWaiting = true;
        yield return new WaitForSeconds(waitTimeAtEnds);
        isWaiting = false;
        agent.isStopped = false;
        agent.SetDestination(waypoints[currentIndex].position);
    }

    // 플레이어가 시야 내에 있는지 검사
    bool IsPlayerInSight()
    {
        Vector3 eyePos = transform.position + Vector3.up * 1.5f; // 눈높이
        Vector3 dirToPlayer = player.position - eyePos;
        float distanceToPlayer = dirToPlayer.magnitude;

        if (distanceToPlayer > viewDistance)
            return false;

        // 시야각 확인 (3D 방향 포함)
        float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer.normalized);
        if (angleToPlayer > viewAngle / 2f)
            return false;

        // 장애물 Raycast 확인
        if (Physics.Raycast(eyePos, dirToPlayer.normalized, out RaycastHit hit, viewDistance))
        {
            if (hit.transform == player)
                return true;
        }

        return false;
    }

    // 시야 관련 기즈모
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        // 시야 거리 원
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        // 3D 시야각 방향 시각화
        Vector3 forward = transform.forward;
        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * forward;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * forward;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, left * viewDistance);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, right * viewDistance);
    }
}
