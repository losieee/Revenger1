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

        // 플레이어/카메라도 그대로 들고 간다
        DontDestroyOnLoad(player.gameObject);
        DontDestroyOnLoad(cameraRig.gameObject);

        PlayerTr = player.transform;

        // 상호 바인딩(축 틀어짐 방지)
        cameraRig.SetTarget(player.transform);
        player.BindCameraPivot(cameraRig.transform);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1) 스폰 포인트 찾기 (id 매칭 → 없으면 첫 번째)
        var points = Object.FindObjectsOfType<SceneSpawnPoint>();
        var target = points.FirstOrDefault(p => p.id == SceneTransit.nextSpawnId)
                   ?? points.FirstOrDefault();
        if (!target) return;

        // 2) 위치/방향 세팅(수평 Y만 적용해 모델이 기울지 않게)
        var pos = target.transform.position;
        var yaw = target.transform.eulerAngles.y;
        player.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        // 3) 남아있을 수 있는 속도/회전속도 제거
        var rb = player.GetComponent<Rigidbody>();
        if (rb) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        // 4) 카메라 재바인딩 + 부드럽게 리센터
        if (cameraRig.target != player.transform)
            cameraRig.SetTarget(player.transform);
        player.BindCameraPivot(cameraRig.transform);
        cameraRig.RecenterToPlayerSmooth(0.05f);
    }
}
