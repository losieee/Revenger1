using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-1000)] // 씬 초기에 가장 먼저 실행되게
public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public string fallbackSpawnId = "Home_Start"; // nextSpawnId가 비어있을 때 기본값

    void Start()
    {
        // 0) 스폰 포인트
        var points = FindObjectsOfType<SceneSpawnPoint>(true);
        if (points == null || points.Length == 0) { Debug.LogWarning("[PlayerSpawner] SceneSpawnPoint 없음"); return; }

        string id = !string.IsNullOrEmpty(SceneTransit.nextSpawnId) ? SceneTransit.nextSpawnId : fallbackSpawnId;
        var target = points.FirstOrDefault(p => p.id == id) ?? points.FirstOrDefault();
        if (!target) { Debug.LogWarning($"[PlayerSpawner] '{id}' 스폰포인트를 찾지 못함"); return; }

        // 1) 이미 존재하는 플레이어(DDOL이든 씬이든 상관 없이) 찾기
        var existingMov = FindObjectOfType<PlayerMov>(true);
        if (existingMov != null)
        {
            existingMov.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
            var rb = existingMov.GetComponent<Rigidbody>();
            if (rb) rb.velocity = Vector3.zero;

            FindObjectOfType<CameraMov>(true)?.BindPlayer(existingMov.transform); // 태그 NO
            SceneTransit.nextSpawnId = null;
            return;
        }

        // 2) 없으면 새로 생성
        if (playerPrefab == null) { Debug.LogWarning("[PlayerSpawner] playerPrefab 비어있음"); return; }
        var player = Instantiate(playerPrefab, target.transform.position, target.transform.rotation);

        // 카메라 바인딩
        FindObjectOfType<CameraMov>(true)?.BindPlayer(player.transform);
        // 또는 SetTarget 등…

        SceneTransit.nextSpawnId = null;
    }
}
