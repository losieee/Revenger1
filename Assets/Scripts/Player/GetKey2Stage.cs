using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetKey2Stage : MonoBehaviour
{
    private bool canGetKey;

    private void Update()
    {
        if (canGetKey && KeyBindings.GetKeyDown(GameAction.Interaction))
        {
            if (KeyManager.i != null)
            {
                KeyManager.i.Add2StageKey(1);
                SoundManager.i.PlaySFX(PlayerSfx.GetKey, SfxBus.Effect, 1f);
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
