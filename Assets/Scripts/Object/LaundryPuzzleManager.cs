using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LaundryPuzzleManager : MonoBehaviour
{
    public static LaundryPuzzleManager i;

    [Header("정답 확인 버튼/오브젝트")]
    [SerializeField] Button resultButton;
    [SerializeField] GameObject resultObject;

    [Header("UI Buttons (눌리는 쪽)")]
    [SerializeField] Button[] uiButtons;

    [Header("Target Objects (회전할 오브젝트들)")]
    [SerializeField] GameObject[] targetButtons;

    [Header("Rotate Settings")]
    [SerializeField] Vector3 rotateAxis = new Vector3(0, 0, 1);
    [SerializeField] float onAngle = 90f;
    [SerializeField] float rotateDuration = 0.18f;

    [Header("Result Object Rotate")]
    [SerializeField] Vector3 resultRotateAxis = new Vector3(0, 0, 1);
    [SerializeField] float resultRotateAngle = 90f;
    [SerializeField] float resultRotateDuration = 0.18f;
    [SerializeField] float resultStayTime = 0.25f; // 90도에서 멈춰있는 시간

    [Header("SFX")]
    [SerializeField] AudioSource sfx;
    [SerializeField] AudioClip rotateOnClip;
    [SerializeField] AudioClip rotateOffClip;
    [SerializeField] AudioClip solvedClip;
    [SerializeField] AudioClip failedClip;
    [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;
    [SerializeField, Range(0f, 0.2f)] float pitchJitter = 0.04f;

    private readonly HashSet<int> correctSet = new HashSet<int> { 0, 2, 3, 5, 6 };
    private readonly HashSet<int> activeSet = new HashSet<int>();
    private bool puzzleCleared = false;

    private bool[] isOn;
    private Quaternion[] baseRot;
    private Quaternion[] targetRot;
    private Quaternion[] fromRot;
    private float[] tLerp;
    private Rigidbody[] rbs;

    private Quaternion resultBaseRot;
    private Coroutine resultCo;

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; }
        i = this;
    }

    void Start()
    {
        if (!sfx) sfx = GetComponent<AudioSource>();
        if (!sfx) sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.ignoreListenerPause = true;

        // 퍼즐 대상 초기화
        int n = targetButtons.Length;
        isOn = new bool[n];
        baseRot = new Quaternion[n];
        targetRot = new Quaternion[n];
        fromRot = new Quaternion[n];
        tLerp = new float[n];
        rbs = new Rigidbody[n];

        for (int i = 0; i < n; i++)
        {
            Transform t = targetButtons[i].transform;
            baseRot[i] = t.localRotation;
            targetRot[i] = baseRot[i];
            fromRot[i] = baseRot[i];
            tLerp[i] = 1f;

            rbs[i] = targetButtons[i].GetComponent<Rigidbody>();
            if (rbs[i] != null)
            {
                rbs[i].isKinematic = true;
                rbs[i].interpolation = RigidbodyInterpolation.None;
                rbs[i].constraints = RigidbodyConstraints.FreezeRotation;
            }

            SetRotationInstant(i, false);
        }

        // resultObject 회전 기준 저장
        if (resultObject)
            resultBaseRot = resultObject.transform.localRotation;

        // 버튼 등록
        for (int i = 0; i < uiButtons.Length; i++)
        {
            uiButtons[i].onClick.RemoveAllListeners();
            int idx = i;
            uiButtons[i].onClick.AddListener(() => OnLaundryButton(idx));
        }

        if (resultButton)
        {
            resultButton.onClick.RemoveAllListeners();
            resultButton.onClick.AddListener(OnResultButton);
        }
    }

    public void OnLaundryButton(int index)
    {
        if (index < 0 || index >= targetButtons.Length) return;
        if (puzzleCleared) return;

        isOn[index] = !isOn[index];
        if (isOn[index]) activeSet.Add(index); else activeSet.Remove(index);

        fromRot[index] = targetButtons[index].transform.localRotation;
        targetRot[index] = isOn[index]
            ? baseRot[index] * Quaternion.AngleAxis(onAngle, rotateAxis.normalized)
            : baseRot[index];
        tLerp[index] = 0f;

        PlayRotateSfx(isOn[index]);
    }

    private void OnResultButton()
    {
        if (puzzleCleared) return;

        bool correct = activeSet.SetEquals(correctSet);

        // resultObject 회전 코루틴 실행
        if (resultObject)
        {
            if (resultCo != null) StopCoroutine(resultCo);
            resultCo = StartCoroutine(RotateResultObject());
        }

        if (correct)
        {
            if (!puzzleCleared) puzzleCleared = true;
            PlaySolvedSfx();
            Debug.Log("퍼즐 정답!");
        }
        else
        {
            PlayFailedSfx();
            Debug.Log("퍼즐 실패!");
        }
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < targetButtons.Length; i++)
        {
            if (tLerp[i] < 1f)
            {
                tLerp[i] += dt / Mathf.Max(0.0001f, rotateDuration);
                float k = Mathf.SmoothStep(0f, 1f, tLerp[i]);
                targetButtons[i].transform.localRotation = Quaternion.Slerp(fromRot[i], targetRot[i], k);
            }
            else
            {
                targetButtons[i].transform.localRotation = targetRot[i];
            }
        }
    }

    private IEnumerator RotateResultObject()
    {
        Quaternion from = resultBaseRot;
        Quaternion to = resultBaseRot * Quaternion.AngleAxis(resultRotateAngle, resultRotateAxis.normalized);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / resultRotateDuration;
            float k = Mathf.SmoothStep(0f, 1f, t);
            resultObject.transform.localRotation = Quaternion.Slerp(from, to, k);
            yield return null;
        }

        yield return new WaitForSeconds(resultStayTime);

        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / resultRotateDuration;
            float k = Mathf.SmoothStep(0f, 1f, t);
            resultObject.transform.localRotation = Quaternion.Slerp(to, from, k);
            yield return null;
        }

        resultObject.transform.localRotation = from;
    }

    private void SetRotationInstant(int i, bool on)
    {
        targetButtons[i].transform.localRotation = on
            ? baseRot[i] * Quaternion.AngleAxis(onAngle, rotateAxis.normalized)
            : baseRot[i];
    }

    public void ResetLaundryPuzzle()
    {
        activeSet.Clear();
        puzzleCleared = false;

        for (int i = 0; i < isOn.Length; i++)
        {
            isOn[i] = false;
            fromRot[i] = baseRot[i];
            targetRot[i] = baseRot[i];
            tLerp[i] = 1f;
            SetRotationInstant(i, false);
        }

        if (resultObject)
            resultObject.transform.localRotation = resultBaseRot;
    }

    void PlayRotateSfx(bool isTurningOn)
    {
        var clip = isTurningOn ? rotateOnClip : rotateOffClip;
        if (!clip || !sfx) return;
        sfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        sfx.PlayOneShot(clip, sfxVolume);
    }

    void PlaySolvedSfx()
    {
        if (!solvedClip || !sfx) return;
        sfx.pitch = 1f;
        sfx.PlayOneShot(solvedClip, sfxVolume);
    }

    void PlayFailedSfx()
    {
        if (!failedClip || !sfx) return;
        sfx.pitch = 1f;
        sfx.PlayOneShot(failedClip, sfxVolume);
    }
}
