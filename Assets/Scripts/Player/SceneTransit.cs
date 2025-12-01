using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneTransit
{
    public static string nextSpawnId;   // PlayerSpawner가 참조할 다음 스폰 ID
    static bool _showCursorOnLoad = false;

    public static void Go(string sceneName, string spawnId, bool showCursorOnLoad = false)
    {
        // 여기서 세팅 (씬 로드 전에)
        nextSpawnId = spawnId;

        // 상태 복구(혹시 호출자가 안했어도 안전망)
        AudioListener.pause = false;
        Time.timeScale = 1f;
        _showCursorOnLoad = showCursorOnLoad;

        SceneManager.sceneLoaded += OnLoaded;
        SceneManager.LoadScene(sceneName);

        void OnLoaded(Scene s, LoadSceneMode m)
        {
            SceneManager.sceneLoaded -= OnLoaded;

            // 1) Player 중복 있으면 한 개만 남기기
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length > 1)
            {
                var keep = players[0];
                for (int i = 1; i < players.Length; i++) Object.Destroy(players[i]);
            }
            var player = GameObject.FindWithTag("Player");

            // 2) 스폰포인트 찾아 위치/회전만 이동
            var points = Object.FindObjectsOfType<SceneSpawnPoint>();
            var sp = points.FirstOrDefault(p => p.id == spawnId);
            if (player && sp)
                player.transform.SetPositionAndRotation(sp.transform.position, sp.transform.rotation);

            nextSpawnId = null;

            // 3) 잔존 UI 제거
            var gameOver = GameObject.Find("GameOver");
            if (gameOver) Object.Destroy(gameOver);

            // 4) 공통 상태 초기화
            if (_showCursorOnLoad)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

            AudioListener.pause = false;

            // 5) EventSystem 선택 초기화
            var es = GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (es) es.SetSelectedGameObject(null);
        }
    }
}
