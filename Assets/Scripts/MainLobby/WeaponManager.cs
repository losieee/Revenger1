using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager i;

    [HideInInspector] public bool canSwitch = false;

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; } // 중복 방지
        i = this;
        DontDestroyOnLoad(gameObject); // 씬 넘어가도 유지
    }

    public void OnClickCrowbar()
    {
        canSwitch = true;
    }
}
