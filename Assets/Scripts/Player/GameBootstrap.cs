using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap i;

    [Header("���� ���� ������Ʈ�� �巡��")]
    public PlayerMov player;     // Tag=Player
    public CameraMov cameraRig;  // Main Camera�� ���� ��ũ��Ʈ

    public Transform PlayerTr { get; private set; }

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; }
        i = this;
        DontDestroyOnLoad(gameObject);

        // �÷��̾�/ī�޶� �״�� ��� ����
        DontDestroyOnLoad(player.gameObject);
        DontDestroyOnLoad(cameraRig.gameObject);

        PlayerTr = player.transform;

        // ��ȣ ���ε�(�� Ʋ���� ����)
        cameraRig.SetTarget(player.transform);
        player.BindCameraPivot(cameraRig.transform);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 1) ���� ����Ʈ ã�� (id ��Ī �� ������ ù ��°)
        var points = Object.FindObjectsOfType<SceneSpawnPoint>();
        var target = points.FirstOrDefault(p => p.id == SceneTransit.nextSpawnId)
                   ?? points.FirstOrDefault();
        if (!target) return;

        // 2) ��ġ/���� ����(���� Y�� ������ ���� ����� �ʰ�)
        var pos = target.transform.position;
        var yaw = target.transform.eulerAngles.y;
        player.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        // 3) �������� �� �ִ� �ӵ�/ȸ���ӵ� ����
        var rb = player.GetComponent<Rigidbody>();
        if (rb) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        // 4) ī�޶� ����ε� + �ε巴�� ������
        if (cameraRig.target != player.transform)
            cameraRig.SetTarget(player.transform);
        player.BindCameraPivot(cameraRig.transform);
        cameraRig.RecenterToPlayerSmooth(0.05f);
    }
}
