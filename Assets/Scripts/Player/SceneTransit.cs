using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneTransit
{
    public static string nextSpawnId;   // PlayerSpawner�� ������ ���� ���� ID

    public static void Go(string sceneName, string spawnId)
    {
        // �� ���⼭ ���� (�� �ε� ����)
        nextSpawnId = spawnId;

        // ���� ����(Ȥ�� ȣ���ڰ� ���߾ ������)
        AudioListener.pause = false;
        Time.timeScale = 1f;

        SceneManager.sceneLoaded += OnLoaded;
        SceneManager.LoadScene(sceneName);

        void OnLoaded(Scene s, LoadSceneMode m)
        {
            SceneManager.sceneLoaded -= OnLoaded;

            // 1) Player �ߺ� ������ �� ���� �����
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length > 1)
            {
                var keep = players[0];
                for (int i = 1; i < players.Length; i++) Object.Destroy(players[i]);
            }
            var player = GameObject.FindWithTag("Player");

            // 2) ��������Ʈ ã�� ��ġ/ȸ���� �̵�
            var points = Object.FindObjectsOfType<SceneSpawnPoint>();
            var sp = points.FirstOrDefault(p => p.id == spawnId);
            if (player && sp)
                player.transform.SetPositionAndRotation(sp.transform.position, sp.transform.rotation);

            // (����) PlayerSpawner�� �Բ� ���ٸ� �� �� �� �� ����α�
            nextSpawnId = null;

            // 3) ���� UI ����
            var gameOver = GameObject.Find("GameOver");
            if (gameOver) Object.Destroy(gameOver);

            // 4) ���� ���� �ʱ�ȭ
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            AudioListener.pause = false;

            // 5) (����) EventSystem ���� �ʱ�ȭ
            var es = GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (es) es.SetSelectedGameObject(null);
        }
    }
}
