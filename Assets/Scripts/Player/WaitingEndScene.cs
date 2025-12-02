using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaitingEndScene : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(WaitLoading());
    }

    IEnumerator WaitLoading()
    {
        yield return new WaitForSeconds(12f);

        SceneManager.LoadScene("Story Ending");
    }
}
