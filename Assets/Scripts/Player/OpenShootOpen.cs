using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class OpenShootOpen : MonoBehaviour
{
    BoxCollider boxcol;
    Animator anim;

    bool canOpen;

    // Start is called before the first frame update
    void Start()
    {
        boxcol = GetComponent<BoxCollider>();
        anim = GetComponentInChildren<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if(canOpen && KeyManager.i.canShoot && KeyBindings.GetKeyDown(GameAction.Interaction))
        {
            anim.SetTrigger("Open");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canOpen = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canOpen = false;
        }
    }
}
