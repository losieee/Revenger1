using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyPickUp : MonoBehaviour
{
    private bool canGetKey;
    private bool closeText = false;

    private void Update()
    {
        if (canGetKey && KeyBindings.GetKeyDown(GameAction.Interaction))
        {
            if (KeyManager.i != null)
            {
                KeyManager.i.AddKey(1);
                SoundManager.i.PlaySFX(PlayerSfx.GetKey, SfxBus.Effect, 1f);
            }

            if(closeText) KeyManager.i.GetComponent<PlayerMov>().nearNPC.gameObject.SetActive(false);

            transform.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canGetKey = true;
            other.GetComponent<PlayerMov>().nearNPC.gameObject.SetActive(true);
            closeText = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canGetKey = false;
        }
    }
}
