using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-1000)] // �� �ʱ⿡ ���� ���� ����ǰ�
public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public string fallbackSpawnId = "Home_Start"; // nextSpawnId�� ������� �� �⺻��

    void Start()
    {
        // ���� ����Ʈ ã��(��Ȱ�� ����)
        var points = FindObjectsOfType<SceneSpawnPoint>(true);
        if (points == null || points.Length == 0)
        {
            Debug.LogWarning("[PlayerSpawner] SceneSpawnPoint ����");
            return;
        }

        string id = !string.IsNullOrEmpty(SceneTransit.nextSpawnId)
                    ? SceneTransit.nextSpawnId
                    : fallbackSpawnId;

        var target = points.FirstOrDefault(p => p.id == id) ?? points.FirstOrDefault();
        if (!target)
        {
            Debug.LogWarning($"[PlayerSpawner] '{id}' ��������Ʈ�� ã�� ����");
            return;
        }

        // ���� �÷��̾ ������ '��ġ��' �̵� (DDOL �÷��̾� ����)
        var existing = GameObject.FindWithTag("Player");
        if (existing != null)
        {
            existing.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);

            // ����/�׺� ����(������)
            var rb = existing.GetComponent<Rigidbody>();
            if (rb) rb.velocity = Vector3.zero;

            // �ʿ� �� ī�޶�/���� ��� �缳��
            // FindObjectOfType<CameraMov>()?.SetTarget(existing.transform);

            SceneTransit.nextSpawnId = null; // �� �� ���� ����α�
            return;
        }

        // ������ ������ ���� ����
        var player = Instantiate(playerPrefab, target.transform.position, target.transform.rotation);

        // �±� ����
        if (player.tag != "Player") player.tag = "Player";

        // �ʿ� �� ī�޶� ����
        // FindObjectOfType<CameraMov>()?.SetTarget(player.transform);

        SceneTransit.nextSpawnId = null; // �� �� ���� ����α�
    }
}
