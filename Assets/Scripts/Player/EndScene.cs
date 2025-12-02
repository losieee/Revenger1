using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndScene : MonoBehaviour
{
    bool goEnd;

    // Update is called once per frame
    void Update()
    {
        if (goEnd && KeyBindings.GetKeyDown(GameAction.Interaction))
        {
            SceneManager.LoadScene("EndScene");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
            goEnd = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            goEnd = false;
    }
}
