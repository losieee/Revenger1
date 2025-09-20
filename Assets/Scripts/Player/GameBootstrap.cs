using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap i;

    [Header("현재 씬의 오브젝트를 드래그")]
    public PlayerMov player;     // Tag=Player
    public CameraMov cameraRig;  // Main Camera에 붙은 스크립트

    public Transform PlayerTr { get; private set; }

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; }
        i = this;
        DontDestroyOnLoad(gameObject);

        DontDestroyOnLoad(player.gameObject);
        DontDestroyOnLoad(cameraRig.gameObject);

        PlayerTr = player.transform;

        // 상호 바인딩
        cameraRig.SetTarget(player.transform);
        player.BindCameraPivot(cameraRig.transform);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var points = Object.FindObjectsOfType<SceneSpawnPoint>();
        var target = points.FirstOrDefault(p => p.id == SceneTransit.nextSpawnId)
                   ?? points.FirstOrDefault();
        if (!target) return;

        var pos = target.transform.position;
        var yaw = target.transform.eulerAngles.y;
        player.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        var rb = player.GetComponent<Rigidbody>();
        if (rb) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        if (cameraRig.target != player.transform)
            cameraRig.SetTarget(player.transform);
        player.BindCameraPivot(cameraRig.transform);
        cameraRig.RecenterToPlayerSmooth(0.05f);
    }
}
