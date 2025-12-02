using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaitingEndScene : MonoBehaviour
{
    void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Destroy(player.gameObject);
        }

        var bootstrap = FindObjectOfType<GameBootstrap>();
        if (bootstrap != null)
        {
            Destroy(bootstrap.gameObject);
        }

        var cam = FindObjectOfType<CameraMov>();
        if (cam != null)
        {
            Destroy(cam.gameObject);
        }

        var sound = FindObjectOfType<SoundManager>();
        if (sound != null)
        {
            Destroy(sound.gameObject);
        }

        StartCoroutine(WaitLoading());
    }

    IEnumerator WaitLoading()
    {
        yield return new WaitForSeconds(12f);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SceneManager.LoadScene("Story Ending");
    }
}
