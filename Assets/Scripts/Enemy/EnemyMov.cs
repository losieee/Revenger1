using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.Rendering;

public class EnemyMov : MonoBehaviour
{
    [Header("��� �̵� ����")]
    public AudioClip[] enemySounds;                 // �Ҹ�
    public Transform[] waypoints;                   // ���� �� ���
    public float walkSpeed = 2f;                    // �ȴ� �ӵ� (���� ��)
    public float runSpeed = 5f;                     // �޸��� �ӵ� (�߰� ��)
    public float waitTimeAtEnds = 1.5f;             // ��� ���� �������� �� ��� �ð�
    public float rotationSpeed = 5f;                // �ʿ� �� ���� ȸ�� �ӵ�

    [Header("�þ� ����")]
    public float viewDistance = 10f;                // ���� ������ �ִ� �Ÿ�
    public float viewAngle = 60f;                   // �⺻ �þ߰� (���� ����)
    public Transform player;                        // ���� ��� (�÷��̾�)
    public GameObject questionMark;                 // ����ǥ (AI�� �÷��̾ �ν����� ��)
    public GameObject answerMarkexclamationMark;    // ����ǥ (AI�� �÷��̾ �߰��� ��)
    public GameObject miniQuestionMark;             // �̴ϸʿ��� ����ǥ
    public GameObject miniAnswerMark;               // �̴ϸʿ��� ����ǥ

    [Header("���� ����")]
    public float lostPlayerGraceTime = 2f;          // �÷��̾ ��ģ �� �� �ʱ��� ���� ��������
    public bool infiniteChaseAfterCorpse = true;    // ��ü�� �߰� ���� �� ���� ����
    private bool chasingFromCorpse = false;         // ��ü�� ���� �߰� �����ߴ°�

    [Header("�߰� �� ���� �Ÿ� ����")]
    public float chaseViewDistance = 50f;           // Chasing������ ������ ���� �þ߰Ÿ�

    [Header("��ü ���� ����")]
    public bool corpseRequiresLineOfSight = true;   // ��ü�� ������ üũ����
    private bool sawCorpse = false;                 // ��ü ���� Watching�� ���Դ��� �÷���
    public static event Action<Transform> OnAnyEnemyKilled;     // ��� ���� �����ϴ� ��ü ��ε�ĳ��Ʈ �̺�Ʈ
    private static readonly List<EnemyMov> Instances = new List<EnemyMov>();    // �ʿ� �ִ� Enemy��


    // ���� ����
    private int currentIndex = 0;               // ���� �̵� ���� waypoint �ε���
    private int direction = 1;                  // ����: 1 = ������, -1 = ������
    private bool isWaiting = false;             // ��� �� ���� ������
    private float playerStayTime = 0f;          // �þ� �ȿ� �÷��̾ �־��� ���� �ð�
    private float lostPlayerTimer = 0f;         // �÷��̾ ��ģ �� ��� �ð�
    private float originalViewAngle;            // ���� �þ߰�
    private float destinationUpdateRate = 0.2f; // �߰� �� ��ǥ ��ġ ���� ����
    private float destinationUpdateTimer = 0f;  // ���� �߰� ��ġ ���� Ÿ�̸�
    private bool isDead = false;                // ���
    private static readonly List<Transform> Corpses = new List<Transform>();    // �þ߰��ȿ� ��ü�� �ִ��� �˻�

    // �Ҹ� ���� �̵� ����
    private bool isSoundTriggered = false;      // �Ҹ� ������ �߻��ߴ��� ����
    private float soundDetectTimer = 0.5f;      // �Ҹ� ���� �� ��� �ð�
    private float maxChaseBySoundTime = 3f;     // �Ҹ� ������ �̵��ϴ� �ִ� �ð�
    private float soundChaseTimer = 0f;         // �Ҹ� ������ ���� ���� �ð� ����
    private Vector3 firstHeardPosition;         // ó�� �鸰 �Ҹ��� ��ġ
    private bool hasHeardPlayer = false;        // �Ҹ� ������ �÷��̾� ���� ��ġ ��� ����
    private bool isSoundWaiting = false;        // ���� ���� ��� ���� �÷���

    // ����, ����
    public float footstepVolume = 0.7f;
    public float QuestionVolume = 0.7f;
    public float chaseVolume = 0.7f;
    private int currentSoundIndex = 0;

    // ������Ʈ
    private Animator animator;
    private NavMeshAgent agent;
    [SerializeField] private BoxCollider catchBox;
    [SerializeField] private BoxCollider attackBox;
    private AudioSource audioSource;
    private AudioSource chaseAudio;     // �߰� ���� ����(�߰� ����)
    private AudioSource footstepAudio;  // ��� �߼Ҹ� ����

    // ���� �߰��� ����
    public static int ActiveChaseVoices = 0;
    public const int MaxChaseVoices = 1;
    private bool hasChaseVoice = false;

    // Enemy ���� ����
    private enum EnemyState { Patrol, Watching, Chasing }       // ���� ��, ���(?) - �÷��̾� ���� �߰� ��, �߰�(!) - �÷��̾� ����
    private EnemyState state = EnemyState.Patrol;

    void OnEnable()
    {
        OnAnyEnemyKilled += HandleCorpseCreated; // ��ü �˸� ����
        if (!Instances.Contains(this)) Instances.Add(this);
    }

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();

        // �߰� ���� ����� �ҽ� ����
        chaseAudio = gameObject.AddComponent<AudioSource>();
        chaseAudio.playOnAwake = false;
        chaseAudio.loop = true;
        chaseAudio.volume = chaseVolume;
        chaseAudio.spatialBlend = 1f;                 // 3D
        chaseAudio.dopplerLevel = 0f;                 // ���÷� �ְ� ����
        chaseAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        chaseAudio.minDistance = 2f;
        chaseAudio.maxDistance = 20f;
        chaseAudio.priority = 180;                    // �ʿ�� ����

        // �߼Ҹ� ����
        footstepAudio = gameObject.AddComponent<AudioSource>(); 
        footstepAudio.playOnAwake = false;                      
        footstepAudio.loop = false;                             
        footstepAudio.spatialBlend = 1f;                        // 3D ������
        footstepAudio.dopplerLevel = 0f;                        // �ְ� ����
        footstepAudio.rolloffMode = AudioRolloffMode.Logarithmic; 
        footstepAudio.minDistance = 2f;                         // �鸮�� �ּ� �Ÿ�
        footstepAudio.maxDistance = 15f;                        // �߼Ҹ� ��û �Ÿ�
        footstepAudio.priority = 200;                           // �켱���� (�������� ����)


        originalViewAngle = viewAngle;      //�����Ҷ��� �⺻ ��߰� 60����
        lostPlayerTimer = 0f;

        if (SoundManager.i && SoundManager.i.EffectGroup != null)
        {
            if (audioSource) audioSource.outputAudioMixerGroup = SoundManager.i.EffectGroup;
            if (chaseAudio) chaseAudio.outputAudioMixerGroup = SoundManager.i.EffectGroup;
            if (footstepAudio) footstepAudio.outputAudioMixerGroup = SoundManager.i.EffectGroup;
        }

        if (catchBox) catchBox.enabled = false;
        if (attackBox) attackBox.enabled = true;

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
        if (agent.velocity.magnitude < 0.1f)
        {
            animator.SetFloat("Speed", 0f);
            currentSoundIndex = 0;          // ���߸� ���� �߼Ҹ��� ������ index 0����
        }

        bool playerInSight = IsPlayerInSight();
        bool corpseInSightNow = IsAnyCorpseVisible();

        // ���¿� ���� ���� ó��
        switch (state)
        {
            case EnemyState.Patrol:
                viewAngle = originalViewAngle;  // �þ߰� ����
                catchBox.enabled = false;       // ��� ���� ��Ȱ��ȭ
                attackBox.enabled = true;       // ����(�ϻ�) ���� Ȱ��ȭ
                Patrol();
                miniAnswerMark.SetActive(false);        // �ٽ� �ʱ�ȭ
                miniQuestionMark.SetActive(false);

                // �߰� ����, ���� ����
                StopChaseLoopCapped();
                audioSource.Stop();

                if (playerInSight || corpseInSightNow)
                {
                    playerStayTime = 0f;    // �� ���� Ÿ�̸� ����
                    sawCorpse = corpseInSightNow;       // ��ü�� �� ��� �÷���

                    // ��ü�� �� ��� ���� ��׷�
                    if (corpseInSightNow)
                        TriggerGlobalAggro(player ? player.position : transform.position);

                    state = EnemyState.Watching;
                    audioSource.PlayOneShot(enemySounds[2], QuestionVolume);
                    break;
                }
                break;

            case EnemyState.Watching:
                viewAngle = 360f;        // �þ߰� Ȯ��
                catchBox.enabled = false;       // ��� ���� ��Ȱ��ȭ
                attackBox.enabled = true;
                animator.SetFloat("Speed", 0f); // �ִϸ��̼� ����
                miniQuestionMark.SetActive(true);   // �̴ϸʿ� ��ũ ǥ��
                miniAnswerMark.SetActive(false);

                if (corpseInSightNow) sawCorpse = true;

                bool escalateTrigger = playerInSight || sawCorpse;

                if (escalateTrigger)
                {
                    agent.isStopped = true;
                    playerStayTime += Time.deltaTime;
                    if (playerStayTime >= 1.5f)       // 1.5�� �̻� ������ ���� ����
                    {
                        agent.isStopped = false;
                        chasingFromCorpse = sawCorpse;
                        state = EnemyState.Chasing;
                        sawCorpse = false;
                        StartChaseLoopCapped();     // �߰� ���� �õ�
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

                            StopChaseLoopCapped();
                            audioSource.Stop();
                        }
                    }
                }
                else
                {
                    playerStayTime += Time.deltaTime;
                    if (playerStayTime >= 1f)
                    {
                        audioSource.Stop();
                        StopChaseLoopCapped();

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
                    // �߰� ���� ����
                    StartChaseLoopCapped();
                    lostPlayerTimer = 0f;
                }
                else
                {
                    // ��ü�� ���۵� �߰��̰�, ���� �ɼ��� ���� ������ ���� �������� ����
                    if (infiniteChaseAfterCorpse && chasingFromCorpse)
                    {
                        // ������ Chasing ����
                        lostPlayerTimer = 0f;
                    }
                    else
                    {
                        lostPlayerTimer += Time.deltaTime;
                        if (lostPlayerTimer >= lostPlayerGraceTime)
                        {
                            StopChaseLoopCapped();
                            audioSource.Stop();
                            ResetSoundDetection();

                            // �Ϲ������� ������ ���� �÷��� ����
                            chasingFromCorpse = false;

                            state = EnemyState.Patrol;
                            agent.SetDestination(waypoints[currentIndex].position);
                        }
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
        if (waypoints == null || waypoints.Length < 2 || isWaiting) return;

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

        catchBox.enabled = true;        // �÷��̾ �i�ư��� ��� ���� Ȱ��ȭ
        attackBox.enabled = false;

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
        if (player == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 dirToPlayer = player.position - eyePos;
        float distanceToPlayer = dirToPlayer.magnitude;

        float maxDist = (state == EnemyState.Chasing) ? chaseViewDistance : viewDistance;
        if (distanceToPlayer > maxDist)
            return false;

        // �þ߰� Ȯ�� (3D ���� ����)
        float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer.normalized);
        if (angleToPlayer > viewAngle / 2f)
            return false;

        // ��ֹ� Raycast Ȯ��
        if (Physics.Raycast(eyePos, dirToPlayer.normalized, out RaycastHit hit, maxDist))
            return hit.transform == player;

        return false;
    }

    // ��ü üũ
    private bool IsTargetVisible(Transform target, float maxDistance, float fov, bool checkLOS)
    {
        if (!target) return false;
        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 dir = target.position - eyePos;
        float dist = dir.magnitude;

        if (dist > maxDistance) return false;
        float angle = Vector3.Angle(transform.forward, dir.normalized);
        if (angle > fov * 0.5f) return false;

        if (checkLOS)
        {
            if (Physics.Raycast(eyePos, dir.normalized, out RaycastHit hit, dist))
            {
                if (!hit.transform.IsChildOf(target)) return false; // ������ ������ false
            }
        }
        return true;
    }

    private bool IsAnyCorpseVisible()
    {
        for (int i = Corpses.Count - 1; i >= 0; --i)
        {
            var c = Corpses[i];
            if (c == null) { Corpses.RemoveAt(i); continue; }
            if (c == transform) continue;

            if (IsTargetVisible(c, viewDistance, viewAngle, corpseRequiresLineOfSight))
                return true;
        }
        return false;
    }

    // �ٸ� Enemy�� �׾��� ��(��ü ����) ȣ��Ǵ� �ݹ�
    private void HandleCorpseCreated(Transform corpse)
    {
        if (isDead || corpse == null || corpse == transform) return;

        // "��� �þ�"�θ� ��ü �߰� �� Watching ����
        if (IsTargetVisible(corpse, viewDistance, viewAngle, corpseRequiresLineOfSight))
        {
            sawCorpse = true;                 // Watching���� 1.5�� �� Chasing�� Ʈ�����ϰ� �ϴ� �÷���
            playerStayTime = 0f;

            // �÷��̾ ó�� �� �Ͱ� ������ ����
            PlayOneShotSafe(enemySounds, 2, QuestionVolume);
            state = EnemyState.Watching;
            agent.isStopped = true;

            miniQuestionMark?.SetActive(true);
            miniAnswerMark?.SetActive(false);
        }
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
            audioSource.PlayOneShot(enemySounds[2], QuestionVolume);

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

    // �ִϸ��̼� �̺�Ʈ���� ȣ���� �Լ�
    public void PlayFootstep()
    {
        if (agent == null) return;

        // ����/��� ���̸� ���� (Watching�̶� �̵��ϸ� ���)
        if (agent.isStopped || agent.velocity.magnitude < 0.1f) return;

        // �߼Ҹ� ����� footstepAudio ��
        if (footstepAudio == null || enemySounds == null || enemySounds.Length < 2) return;

        // 0,1 ���� ������ ���
        footstepAudio.pitch = 1f + UnityEngine.Random.Range(-0.05f, 0.05f);
        footstepVolume = Mathf.Clamp01(footstepVolume);
        footstepAudio.PlayOneShot(enemySounds[currentSoundIndex], footstepVolume);
        currentSoundIndex = (currentSoundIndex + 1) % 2;
    }
    void PlayOneShotSafe(AudioClip[] clips, int index, float vol)
    {
        if (audioSource == null || clips == null) return;
        if (index < 0 || index >= clips.Length) return;
        if (clips[index] == null) return;
        audioSource.PlayOneShot(clips[index], vol);
    }

    // ���� ���� / ����
    void StartChaseLoop()
    {
        if (chaseAudio == null) return;
        if (enemySounds == null || enemySounds.Length < 4 || enemySounds[3] == null) return;
        if (!chaseAudio.isPlaying)
        {
            chaseAudio.clip = enemySounds[3];
            chaseAudio.volume = chaseVolume;
            chaseAudio.Play();
        }
    }

    void StopChaseLoop()
    {
        if (chaseAudio == null) return;
        if (chaseAudio.isPlaying) chaseAudio.Stop();
        chaseAudio.clip = null;
    }

    // Chasing ���� ��� ����
    void StartChaseLoopCapped()
    {
        if (hasChaseVoice) { StartChaseLoop(); return; }
        if (ActiveChaseVoices >= MaxChaseVoices) return; // ���� ����

        StartChaseLoop();
        if (chaseAudio.isPlaying)
        {
            hasChaseVoice = true;
            ActiveChaseVoices++;
        }
    }

    void StopChaseLoopCapped()
    {
        if (hasChaseVoice)
        {
            hasChaseVoice = false;
            ActiveChaseVoices = Mathf.Max(0, ActiveChaseVoices - 1);
        }
        StopChaseLoop();
    }

    // ������Ʈ ��Ȱ��/�ı� �� ī����/���� ���� ���� ����
    void OnDisable()
    {
        OnAnyEnemyKilled -= HandleCorpseCreated; // ����
        Instances.Remove(this);
        chasingFromCorpse = false;
        StopChaseLoopCapped();
        audioSource?.Stop();
    }
    void OnDestroy()
    {
        Instances.Remove(this);
        chasingFromCorpse = false;
        StopChaseLoopCapped();
        audioSource?.Stop();
    }

    // ��� ������ '��ü�� ���ߵ� �߰�'
    private static void TriggerGlobalAggro(Vector3 targetPos)
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            var e = Instances[i];
            if (!e || e.isDead) continue;
            e.ForceChaseFromCorpse(targetPos);
        }
    }

    public void ForceChaseFromCorpse(Vector3 targetPos)
    {
        if (isDead) return;

        bool wasChasing = (state == EnemyState.Chasing);

        chasingFromCorpse = true;
        lostPlayerTimer = 0f;
        state = EnemyState.Chasing;

        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.SetDestination(targetPos);

        StartChaseLoopCapped();

        miniQuestionMark?.SetActive(false);
        miniAnswerMark?.SetActive(true);

        if (!wasChasing) PlayOneShotSafe(enemySounds, 2, QuestionVolume);
    }

    // ���
    public void Kill()
    {
        if (isDead) return;
        isDead = true;

        Corpses.Add(transform);

        OnAnyEnemyKilled?.Invoke(transform);

        // �Ҹ� ����
        StopChaseLoopCapped();
        audioSource?.Stop();

        // ������Ʈ ����
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        // �ݶ��̴�/ǥ�� ����
        if (catchBox) catchBox.enabled = false;
        if (attackBox) attackBox.enabled = false;
        if (questionMark) questionMark.SetActive(false);
        if (answerMarkexclamationMark) answerMarkexclamationMark.SetActive(false);
        if (miniQuestionMark) miniQuestionMark.SetActive(false);
        if (miniAnswerMark) miniAnswerMark.SetActive(false);

        // �ִϸ����� ����(�ʿ� �� ��� Ʈ���ŷ� ��ü ����)
        if (animator)
        {
            animator.SetFloat("Speed", 0f);
            // animator.SetTrigger("Die"); // 'Die' �ִϸ��̼� ������ ���
        }

        // �� ��ũ��Ʈ ��Ȱ��ȭ�ؼ� ���¸ӽ�/Update ���� ����
        enabled = false;
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
