using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-1000)] // 씬 초기에 가장 먼저 실행되게
public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public string fallbackSpawnId = "Home_Start"; // nextSpawnId가 비어있을 때 기본값

    void Start()
    {
        // 스폰 포인트 찾기(비활성 포함)
        var points = FindObjectsOfType<SceneSpawnPoint>(true);
        if (points == null || points.Length == 0)
        {
            Debug.LogWarning("[PlayerSpawner] SceneSpawnPoint 없음");
            return;
        }

        string id = !string.IsNullOrEmpty(SceneTransit.nextSpawnId)
                    ? SceneTransit.nextSpawnId
                    : fallbackSpawnId;

        var target = points.FirstOrDefault(p => p.id == id) ?? points.FirstOrDefault();
        if (!target)
        {
            Debug.LogWarning($"[PlayerSpawner] '{id}' 스폰포인트를 찾지 못함");
            return;
        }

        // 기존 플레이어가 있으면 '위치만' 이동 (DDOL 플레이어 대응)
        var existing = GameObject.FindWithTag("Player");
        if (existing != null)
        {
            existing.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);

            // 물리/네비 리셋(있으면)
            var rb = existing.GetComponent<Rigidbody>();
            if (rb) rb.velocity = Vector3.zero;

            // 필요 시 카메라/추적 대상 재설정
            // FindObjectOfType<CameraMov>()?.SetTarget(existing.transform);

            SceneTransit.nextSpawnId = null; // 한 번 쓰고 비워두기
            return;
        }

        // 기존이 없으면 새로 생성
        var player = Instantiate(playerPrefab, target.transform.position, target.transform.rotation);

        // 태그 보장
        if (player.tag != "Player") player.tag = "Player";

        // 필요 시 카메라 세팅
        // FindObjectOfType<CameraMov>()?.SetTarget(player.transform);

        SceneTransit.nextSpawnId = null; // 한 번 쓰고 비워두기
    }
}
