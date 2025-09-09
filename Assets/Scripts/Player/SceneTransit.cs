using UnityEngine.SceneManagement;

public static class SceneTransit
{
    public static string nextSpawnId = "Default";

    public static void Go(string sceneName, string spawnId = "Default")
    {
        nextSpawnId = spawnId;
        SceneManager.LoadScene(sceneName);
    }
}
