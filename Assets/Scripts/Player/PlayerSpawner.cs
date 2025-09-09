using System.Linq;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;

    void Start()
    {
        // 중복 생성 방지(재시작 등)
        if (GameObject.FindWithTag("Player") != null) return;

        var points = FindObjectsOfType<SceneSpawnPoint>();
        var target = points.FirstOrDefault(p => p.id == SceneTransit.nextSpawnId)
                     ?? points.FirstOrDefault();

        if (!target) { Debug.LogWarning("SceneSpawnPoint 없음"); return; }

        var player = Instantiate(playerPrefab, target.transform.position, target.transform.rotation);

        // 필요하면 여기서 카메라 추적 대상 재지정(네 카메라 스크립트에 맞춰 수정)
        // FindObjectOfType<CameraMov>()?.SetTarget(player.transform);
    }
}