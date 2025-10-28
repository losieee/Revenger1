using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class LaundryPuzzleManager : MonoBehaviour
{
    public static LaundryPuzzleManager i;

    [Header("정답 확인 버튼/오브젝트 (플레이어 고정)")]
    [SerializeField] Button resultButton;
    [SerializeField] GameObject resultObject;

    [Header("씬에서 주입될 UI 버튼들 (선택)")]
    [SerializeField] Button[] uiButtons;

    [Header("씬에서 주입될 타겟 오브젝트들")]
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

    private readonly HashSet<int> correctSet = new HashSet<int> { 0, 5, 7 };
    private readonly HashSet<int> activeSet = new HashSet<int>();
    private bool puzzleCleared = false;

    private bool[] isOn;
    private Quaternion[] baseRot;
    private Quaternion[] targetRot;
    private Quaternion[] fromRot;
    private float[] tLerp;
    private Rigidbody[] rbs;
    private int failAttempts = 0;

    private Quaternion resultBaseRot;
    private Coroutine resultCo;

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; }
        i = this;
        var sfxComp = GetComponent<AudioSource>();
        if (!sfxComp) sfxComp = gameObject.AddComponent<AudioSource>();
        sfx = sfxComp; sfx.playOnAwake = false; sfx.ignoreListenerPause = true;
    }

    void Start()
    {
        // 혹시 프리팹 병합/스트립 등으로 sfx가 비었다면 안전 복구
        if (!sfx)
        {
            sfx = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.ignoreListenerPause = true;
        }

        // resultObject 기준 회전값 저장
        if (resultObject) resultBaseRot = resultObject.transform.localRotation;

        // 1) 에디터/바인더로 타겟이 이미 들어온 경우 바로 초기화
        if (targetButtons != null && targetButtons.Length > 0)
            Initialize(uiButtons, targetButtons);
        else
            // 2) 씬에 연결 안 했으면 플레이어 UI에서 자동으로 버튼만 찾아서 리스너 연결
            WireUiButtonsIfNeed();

        // 3) 혹시 바인딩 타이밍 차이로 배열 길이가 안 맞으면 재초기화
        if (baseRot == null || baseRot.Length != (targetButtons?.Length ?? 0))
            ReinitTargets();

        // 4) 리스너 안전하게 다시 연결(중복 방지)
        if (resultButton)
        {
            resultButton.onClick.RemoveAllListeners();
            resultButton.onClick.AddListener(OnResultButton);
        }
        if (uiButtons != null)
        {
            for (int i = 0; i < uiButtons.Length; i++)
            {
                int idx = i;
                uiButtons[i].onClick.RemoveAllListeners();
                uiButtons[i].onClick.AddListener(() => OnLaundryButton(idx));
            }
        }
    }

    /// <summary>
    /// 씬이 로드될 때 LaundrySceneBinder 또는 SceneContext에서 호출
    /// </summary>
    public void BindSceneObjects(GameObject[] targets, GameObject result, Transform camPivot = null)
    {
        // 1) 참조만 갈아끼우는 게 아니라 내부 상태를 ‘다시’ 준비
        targetButtons = targets ?? System.Array.Empty<GameObject>();
        resultObject = result;

        // 카메라 피벗도 넘겨받으면 플레이어에 반영 (선택)
        var p = GameBootstrap.i?.player ?? FindObjectOfType<PlayerMov>();
        if (camPivot && p) p.BindCameraPivot(camPivot);

        // 2) 내부 배열/기준값 재셋업
        ReinitTargets();
    }

    void ReinitTargets()
    {
        // 안전가드
        if (targetButtons == null) targetButtons = System.Array.Empty<GameObject>();

        int n = targetButtons.Length;
        isOn = new bool[n];
        baseRot = new Quaternion[n];
        targetRot = new Quaternion[n];
        fromRot = new Quaternion[n];
        tLerp = new float[n];
        rbs = new Rigidbody[n];

        for (int i = 0; i < n; i++)
        {
            var go = targetButtons[i];
            if (!go) continue;

            Transform t = go.transform;
            baseRot[i] = t.localRotation;
            targetRot[i] = baseRot[i];
            fromRot[i] = baseRot[i];
            tLerp[i] = 1f;

            rbs[i] = go.GetComponent<Rigidbody>();
            if (rbs[i] != null)
            {
                rbs[i].isKinematic = true;
                rbs[i].interpolation = RigidbodyInterpolation.None;
                rbs[i].constraints = RigidbodyConstraints.FreezeRotation;
            }

            SetRotationInstant(i, false);
        }

        if (resultObject)
            resultBaseRot = resultObject.transform.localRotation;

        // 누적 상태도 초기화
        activeSet.Clear();
        puzzleCleared = false;
    }

    void WireUiButtonsIfNeed()
    {
        if (uiButtons != null && uiButtons.Length > 0) return;
        var buttons = GetComponentsInChildren<Button>(true)
                      .Where(b => b.name.StartsWith("Button")) // Result_Button 제외
                      .OrderBy(b => b.name)                   // 이름순 정렬
                      .ToArray();
        if (buttons.Length > 0) uiButtons = buttons;
        if (!resultButton) resultButton = GetComponentsInChildren<Button>(true)
                                .FirstOrDefault(b => b.name == "Result_Button");

        // Result 버튼 리스너 등록
        if (resultButton)
        {
            resultButton.onClick.RemoveAllListeners();
            resultButton.onClick.AddListener(OnResultButton);
        }

        // UI 버튼 리스너 등록
        if (uiButtons != null)
        {
            for (int i = 0; i < uiButtons.Length; i++)
            {
                int idx = i;
                uiButtons[i].onClick.RemoveAllListeners();
                uiButtons[i].onClick.AddListener(() => OnLaundryButton(idx));
            }
        }
    }

    void Initialize(Button[] uiBtns, GameObject[] targets)
    {
        if (targets == null || targets.Length == 0) return;

        // 버튼 리스너(플레이어 UI)
        if (resultButton)
        {
            resultButton.onClick.RemoveAllListeners();
            resultButton.onClick.AddListener(OnResultButton);
        }
        if (uiBtns != null && uiBtns.Length > 0)
        {
            for (int i = 0; i < uiBtns.Length; i++)
            {
                int idx = i;
                uiBtns[i].onClick.RemoveAllListeners();
                uiBtns[i].onClick.AddListener(() => OnLaundryButton(idx));
            }
        }

        int n = targets.Length;
        isOn = new bool[n];
        baseRot = new Quaternion[n];
        targetRot = new Quaternion[n];
        fromRot = new Quaternion[n];
        tLerp = new float[n];
        rbs = new Rigidbody[n];

        for (int i = 0; i < n; i++)
        {
            var go = targets[i];
            if (!go) continue;

            Transform t = go.transform;
            baseRot[i] = t.localRotation;
            targetRot[i] = baseRot[i];
            fromRot[i] = baseRot[i];
            tLerp[i] = 1f;

            rbs[i] = go.GetComponent<Rigidbody>();
            if (rbs[i])
            {
                rbs[i].isKinematic = true;
                rbs[i].interpolation = RigidbodyInterpolation.None;
                rbs[i].constraints = RigidbodyConstraints.FreezeRotation;
            }
            SetRotationInstant(i, false);
        }

        activeSet.Clear();
        puzzleCleared = false;
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

        if (resultObject)
        {
            if (resultCo != null) StopCoroutine(resultCo);
            resultCo = StartCoroutine(RotateResultObject());
        }

        if (correct)
        {
            puzzleCleared = true;
            failAttempts = 0;
            PlaySolvedSfx();
        }
        else
        {
            failAttempts++;
            if (failAttempts % 2 == 0)
                PlayFailedSfx();
        }
    }

    void LateUpdate()
    {
        if (targetButtons == null) return;

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < targetButtons.Length; i++)
        {
            if (!targetButtons[i]) continue;

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
        if (!targetButtons[i]) return;
        targetButtons[i].transform.localRotation = on
            ? baseRot[i] * Quaternion.AngleAxis(onAngle, rotateAxis.normalized)
            : baseRot[i];
    }

    public void ResetLaundryPuzzle()
    {
        failAttempts = 0;
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
