using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxOpen : MonoBehaviour
{
    [SerializeField] Animator anim;

    public void PlayBox()
    {
        anim.ResetTrigger("BoxOpen");
        anim.SetTrigger("BoxOpen");
    }
}
