using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class WarpTrigger : MonoBehaviour
{
    [Header("이동할 씬")]
    [SerializeField] private string sceneName;

    [Tooltip("목적지 ID")]
    [SerializeField] private string spawnPointName;

    [SerializeField] private string playerTag = "Player";

    [Header("Events")]
    public UnityEvent onEnterRange;   // 플레이어가 범위에 들어왔을 때
    public UnityEvent onExitRange;    // 범위에서 나갔을 때
    public UnityEvent onWarp;         // 실제 워프 직전에 호출

    private bool canWarp = false;
    private Collider playerInRange;

    bool isHole;
    bool isWindow;
    bool isDoor;

    void Update()
    {
        if (canWarp && KeyBindings.GetKeyDown(GameAction.Interaction) && isHole)
            HoleWarp();

        if (canWarp && KeyBindings.GetKeyDown(GameAction.Interaction) && isWindow)
            WindowWarp();
        
        else if (canWarp && KeyBindings.GetKeyDown(GameAction.Interaction) && isDoor) 
            DoWarp();

    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (gameObject.CompareTag("Manhole")) isHole = true;
        if (gameObject.CompareTag("Window")) isWindow = true;
        if (gameObject.CompareTag("WarpDoor")) isDoor = true;

        canWarp = true;
        playerInRange = other;

        onEnterRange?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (gameObject.CompareTag("Manhole")) isHole = false;
        if (gameObject.CompareTag("Window")) isWindow = false;
        if (gameObject.CompareTag("WarpDoor")) isDoor = false;

        if (other == playerInRange)
        {
            canWarp = false;
            playerInRange = null;
            onExitRange?.Invoke();
        }
    }

    private void DoWarp()
    {
        onWarp?.Invoke();
        SoundManager.i?.PlaySFX(PlayerSfx.Ladder, SfxBus.Effect, 1f);
        ScreenFader.i.FadeOutAndLoad(() =>
        {
            SceneTransit.Go(sceneName, spawnPointName);
        });
    }

    private void HoleWarp()
    {
        onWarp?.Invoke();
        SoundManager.i?.PlaySFX(PlayerSfx.Ladder, SfxBus.Effect, 1f);
        ScreenFader.i.FadeOutAndLoad(() =>
        {
            SceneTransit.Go(sceneName, spawnPointName);
        });
    }

    private void WindowWarp()
    {
        onWarp?.Invoke();
        SoundManager.i?.PlaySFX(PlayerSfx.Ladder, SfxBus.Effect, 1f);
        ScreenFader.i.FadeOutAndLoad(() =>
        {
            SceneTransit.Go(sceneName, spawnPointName);
        });
    }
}
