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

    void Update()
    {
        if (canWarp && KeyBindings.GetKeyDown(GameAction.Interaction))
        {
            DoWarp();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        canWarp = true;
        playerInRange = other;

        onEnterRange?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

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
        ScreenFader.i.FadeOutAndLoad(() =>
        {
            SceneTransit.Go(sceneName, spawnPointName);
        });
    }
}
