using System.Collections;
using TMPro;
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

    [Header("Scene Jump")]
    [SerializeField] string spawnId = "Default";

    [SerializeField] TMP_Text takeWeaponText;

    [Header("Puzzles")]
    [SerializeField] private DressRoomPuzzleManager dressRoomPuzzle;
    [SerializeField] private GuestRoomPuzzleSetManager guestRoomPuzzle;


    [HideInInspector] public bool canNextStage = false;

    void Start()
    {
        // 슬라이더 초기화
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

    }

    // 씬 로드
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

            if (ScreenFader.i != null)
            {
                ScreenFader.i.FadeOutAndLoad(() =>
                {
                    SceneTransit.Go(sceneName, spawnId);
                });
            }
            else
            {
                SceneTransit.Go(sceneName, spawnId);
            }
        }
    }

    public void LoadSceneSimple(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        AudioListener.pause = false;
        Time.timeScale = 1f;

        if (ScreenFader.i != null)
        {
            ScreenFader.i.FadeOutAndLoad(() =>
            {
                SceneTransit.Go(sceneName, spawnId);
            });
        }
        else
        {
            SceneTransit.Go(sceneName, spawnId);
        }
    }

    public void StartStory()
    {
        if (ScreenFader.i != null)
        {
            ScreenFader.i.FadeOutAndLoad(() =>
            {
                SceneManager.LoadScene("Story Start");
            });
        }
        else
        {
            SceneManager.LoadScene("Story Start");
        }
    }

    public void StartGame()
    {
        var player = FindObjectOfType<PlayerMov>();
        if (player)
        {
            player.SetPlayerColliderEnabled(true);
            player.ResetDeathState();
        }

        if (KeyManager.i != null)
            KeyManager.i.ResetKeys();

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.ResetInventory();

        if (dressRoomPuzzle != null)
            dressRoomPuzzle.ResetDressPuzzle();

        if (guestRoomPuzzle != null)
            guestRoomPuzzle.ResetAllPuzzles();

        if (LaundryPuzzleManager.i != null)
            LaundryPuzzleManager.i.ResetLaundryPuzzle();

        Time.timeScale = 1f;
        AudioListener.pause = false;

        GameObject.Find("Player").GetComponent<PlayerMov>().seeSetting = false;

        // 멈춤/클리어/오버 패널들 숨기기
        //HidePanelByName("GameOver");
        HidePanelByName("GameClear");
        HidePanelByName("OptionPop");
        HidePanelByName("Weapon_Choice_Panel");
        HidePanelByName("MissionImg");

        // 선택 포커스 초기화
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        string targetScene = "Home";
        string targetSpawn = "Home_Start";
        bool showCursorOnLoad = true;

        if (player != null && player.checkPoint2)
        {
            // 2번 체크포인트면 1스테이지 클리어 씬으로
            targetScene = "Clear_1_stage_inside";
            targetSpawn = "Default";
            showCursorOnLoad = false;
        }

        if (player != null)
        {
            //player.HidePausePanel(player.gameOverUI);
            player.ResetDeathState();
        }

        if (ScreenFader.i != null)
        {
            ScreenFader.i.FadeOutAndLoad(() =>
            {
                SceneTransit.Go(targetScene, targetSpawn, showCursorOnLoad: showCursorOnLoad);
            });
        }
        else
        {
            SceneTransit.Go(targetScene, targetSpawn, showCursorOnLoad: showCursorOnLoad);
        }
    }

    static void HidePanelByName(string name)
    {
        var go = GameObject.Find(name);
        if (!go) return;

        var cg = go.GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        go.SetActive(false);
    }

    IEnumerator TextCount()
    {
        takeWeaponText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(0.5f);
        takeWeaponText.gameObject.SetActive(false);
    }

    // 슬라이더 → 오디오
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
            SoundManager.i?.PlaySFX(PlayerSfx.ButtonClick, SfxBus.Effect, 1f);
    }

    public void OffOption()
    {
        optionPop.SetActive(false);
        onOption = false;
        if (SoundManager.i != null)
            SoundManager.i?.PlaySFX(PlayerSfx.ButtonClick, SfxBus.Effect, 1f);
    }

    public void ExitGame()
    {
        exitPop.SetActive(true);
        if (SoundManager.i != null)
            SoundManager.i?.PlaySFX(PlayerSfx.ButtonClick, SfxBus.Effect, 1f);
    }

    public void OffExit()
    {
        exitPop.SetActive(false);
        if (SoundManager.i != null)
            SoundManager.i?.PlaySFX(PlayerSfx.ButtonClick, SfxBus.Effect, 1f);
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

    public void ReadyToStart()
    {
        GameObject.Find("Player").GetComponent<PlayerMov>().seeSetting = true;

        if (CameraMov.i != null)
            CameraMov.i.lockLook = false;
    }

    public void GameExit()
    {
        SceneManager.sceneLoaded += OnMainLobbyLoaded;
        SceneManager.LoadScene("MainLobby");
    }

    private void OnMainLobbyLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainLobby") return;
        SceneManager.sceneLoaded -= OnMainLobbyLoaded;

        if (GameBootstrap.i)
        {
            if (GameBootstrap.i.player) Destroy(GameBootstrap.i.player.gameObject);
            if (GameBootstrap.i.cameraRig) Destroy(GameBootstrap.i.cameraRig.gameObject);
            Destroy(GameBootstrap.i.gameObject);
        }

        Time.timeScale = 1;
    }
}
