using System;
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
    public GameObject questionMark;             // 물음표 (AI가 플레이어를 인식했을 때)
    public GameObject answerMarkexclamationMark;// 느낌표 (AI가 플레이어를 추격할 때)
    public GameObject miniQuestionMark;         // 미니맵에서 물음표
    public GameObject miniAnswerMark;           // 미니맵에서 느낌표

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
    private float destinationUpdateRate = 0.2f; // 추격 중 목표 위치 갱신 간격
    private float destinationUpdateTimer = 0f;  // 현재 추격 위치 갱신 타이머
    private bool isSoundTriggered = false;      // 소리 감지가 발생했는지 여부
    private float soundDetectTimer = 0.5f;      // 소리 감지 후 경과 시간
    private float maxChaseBySoundTime = 3f;     // 소리 감지로 이동하는 최대 시간
    private float soundChaseTimer = 0f;         // 소리 감지로 추적 중인 시간 누적
    private Vector3 firstHeardPosition;         // 처음 들린 소리의 위치
    private bool hasHeardPlayer = false;        // 소리 감지로 플레이어 최초 위치 기록 여부
    private bool isSoundWaiting = false;        // 감지 직후 잠깐 멈춤 플래그

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
                miniAnswerMark.SetActive(false);        // 다시 초기화
                miniQuestionMark.SetActive(false);

                if (playerInSight)
                {
                    playerStayTime = 0f;    // 본 순간 타이머 리셋
                    state = EnemyState.Watching;
                }
                break;

            case EnemyState.Watching:
                viewAngle = 360f;        // 시야각 확장
                animator.SetFloat("Speed", 0f); // 애니메이션 정지
                miniQuestionMark.SetActive(true);   // 미니맵에 마크 표시
                miniAnswerMark.SetActive(false);

                if (playerInSight)
                {
                    agent.isStopped = true;
                    playerStayTime += Time.deltaTime;
                    if (playerStayTime >= 1.5f)       // 1.5초 이상 봤으면 추적 시작
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

                        // 사운드 관련 초기화 추가
                        ResetSoundDetection();
                    }
                }
                break;

            case EnemyState.Chasing:
                viewAngle = 360f;       // 시야 확대
                ChasePlayer();
                miniQuestionMark.SetActive(false);
                miniAnswerMark.SetActive(true);     // 미니맵에 마크 표시

                if (playerInSight)
                {
                    lostPlayerTimer = 0f;
                }
                else
                {
                    lostPlayerTimer += Time.deltaTime;
                    if (lostPlayerTimer >= lostPlayerGraceTime)
                    {
                        // 상태 전환 전에 사운드 감지 상태 초기화
                        ResetSoundDetection();

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

        UpdateMark();
    }

    void LateUpdate()
    {
        // 항상 월드 Z+ 방향을 향하도록 미니맵 마크 회전 고정
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

    // AI 머리위에 뜨는 마크 (?, !)
    void UpdateMark()
    {
        // 모든 마크 비활성화
        questionMark.SetActive(false);
        answerMarkexclamationMark.SetActive(false);

        // 현재 상태에 따라 해당 마크 활성화
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

            // 마크가 항상 카메라를 바라보게
            if (activeMark != null)
            {
                activeMark.SetActive(true);

                // 마크가 항상 카메라를 바라보게
                if (Camera.main != null)
                {
                    activeMark.transform.LookAt(Camera.main.transform);
                    activeMark.transform.Rotate(0f, 180f, 0f);
                }
            }
        }
    }

    // 소리가 들리면 플레이어 방향으로 이동
    public void PlayerDetected(Vector3 _)
    {
        // 소리 감지 재시작을 허용(상태가 Chasing일 때만 무시)
        if (state == EnemyState.Chasing)
            return;

        if (!hasHeardPlayer) // 처음 소리 감지일 때만 위치 기록
        {
            firstHeardPosition = player.position;
            hasHeardPlayer = true;
            isSoundWaiting = true;

            // 소리 감지 초기화(중복 감지도 허용)
            isSoundTriggered = true;
            soundDetectTimer = 0f;
            soundChaseTimer = 0f;

            // 소리 들리면 바로 Watching 상태로 진입하고, 위치 설정
            state = EnemyState.Watching;

            agent.isStopped = true; // 정지 해제
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
