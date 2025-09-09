using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ButtonControl : MonoBehaviour
{
    [Header("Option")]
    public GameObject optionPop;
    private bool onOption = false;
    public GameObject exitPop;

    [Header("Sliders")]
    public Slider musicSlider;
    public Slider effectSlider;

    [Header("Sounds")]
    public AudioClip activateSound;
    public AudioClip deactivateSound;

    [Header("Scene Jump")]
    [SerializeField] string spawnId = "Default";


    [HideInInspector] public bool canNextStage = false;

    void Start()
    {
        // �����̴� �ʱ�ȭ
        if (musicSlider != null)
        {
            float m = PlayerPrefs.GetFloat("MusicVol", 1f);
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.wholeNumbers = false;
            musicSlider.value = m;

            musicSlider.onValueChanged.RemoveAllListeners();
            musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        }

        if (effectSlider != null)
        {
            float e = PlayerPrefs.GetFloat("EffectVol", 1f);
            effectSlider.minValue = 0f;
            effectSlider.maxValue = 1f;
            effectSlider.wholeNumbers = false;
            effectSlider.value = e;

            effectSlider.onValueChanged.RemoveAllListeners();
            effectSlider.onValueChanged.AddListener(OnEffectSliderChanged);
        }

        if (SoundManager.i != null)
            SoundManager.i.LoadVolume();
    }

    private void Update()
    {
        if (onOption && Input.GetKeyDown(KeyCode.Escape))
        {
            optionPop.SetActive(false);

            if (SoundManager.i != null)
                SoundManager.i.PlaySFX(deactivateSound, SfxBus.ButtonClick);
        }
            
    }

    // �� �ε�
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        if (!canNextStage)
        {
            StartCoroutine(TextCount());
        }
        else
        {
            Time.timeScale = 1f;
            SceneTransit.Go(sceneName, spawnId);
        }
    }

    public void LoadSceneSimple(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        // Ȥ�� �Ͻ�����/������ ���� �ִ� ��Ȳ ����
        AudioListener.pause = false;
        Time.timeScale = 1f;

        SceneManager.LoadScene(sceneName);   // Home ����
    }

    public void StartGame() => LoadSceneSimple("Home");

    IEnumerator TextCount()
    {
        transform.GetChild(1).gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(0.5f);
        transform.GetChild(1).gameObject.SetActive(false);
    }

    // �����̴� �� �����
    public void OnMusicSliderChanged(float v)
    {
        if (SoundManager.i != null)
            SoundManager.i.SetMusicVolume(v);
    }

    public void OnEffectSliderChanged(float v)
    {
        if (SoundManager.i != null)
            SoundManager.i.SetEffectVolume(v);
    }

    public void ClickOption()
    {
        optionPop.SetActive(true);
        onOption = true;
        if (SoundManager.i != null)
            SoundManager.i.PlaySFX(activateSound, SfxBus.ButtonClick);
    }

    public void OffOption()
    {
        optionPop.SetActive(false);
        onOption = false;
        if (SoundManager.i != null)
            SoundManager.i.PlaySFX(deactivateSound, SfxBus.ButtonClick);
    }

    public void ExitGame()
    {
        exitPop.SetActive(true);
        if (SoundManager.i != null)
            SoundManager.i.PlaySFX(deactivateSound, SfxBus.ButtonClick);
    }

    public void OffExit()
    {
        exitPop.SetActive(false);
        if (SoundManager.i != null)
            SoundManager.i.PlaySFX(deactivateSound, SfxBus.ButtonClick);
    }

    public void RealExitGame()
    {
        Application.Quit();
    }

    public void InGameOrigin()
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
