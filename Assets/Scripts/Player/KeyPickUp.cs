using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyPickUp : MonoBehaviour
{
    private bool canGetKey;

    private void Update()
    {
        if (canGetKey && KeyBindings.GetKeyDown(GameAction.Interaction))
        {
            if (KeyManager.i != null)
            {
                KeyManager.i.AddKey(1);
            }

            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            canGetKey = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            canGetKey = false;
    }
}
