using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager i;

    [HideInInspector] public bool canSwitch = false;

    private void Awake()
    {
        i = this;
    }

    public void OnClickCrowbar()
    {
        canSwitch = true;
    }
}
