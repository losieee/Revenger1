using System.Linq;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;

    void Start()
    {
        // �ߺ� ���� ����(����� ��)
        if (GameObject.FindWithTag("Player") != null) return;

        var points = FindObjectsOfType<SceneSpawnPoint>();
        var target = points.FirstOrDefault(p => p.id == SceneTransit.nextSpawnId)
                     ?? points.FirstOrDefault();

        if (!target) { Debug.LogWarning("SceneSpawnPoint ����"); return; }

        var player = Instantiate(playerPrefab, target.transform.position, target.transform.rotation);

        // �ʿ��ϸ� ���⼭ ī�޶� ���� ��� ������(�� ī�޶� ��ũ��Ʈ�� ���� ����)
        // FindObjectOfType<CameraMov>()?.SetTarget(player.transform);
    }
}