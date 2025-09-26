using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager i;

    [HideInInspector] public bool canCrowbarSwitch = false;
    [HideInInspector] public bool canGunSwitch = false;

    public enum WeaponType { None, Crowbar, Gun }
    public WeaponType SelectedWeapon { get; private set; } = WeaponType.None;

    public static event Action<WeaponType> OnWeaponChosen;

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; } // 중복 방지
        i = this;
        DontDestroyOnLoad(gameObject); // 씬 넘어가도 유지
    }

    public void OnClickCrowbar()
    {
        canCrowbarSwitch = true;
        SelectedWeapon = WeaponType.Crowbar;
        OnWeaponChosen?.Invoke(SelectedWeapon);
    }

    public void OnClickGun()
    {
        canGunSwitch = true;
        SelectedWeapon = WeaponType.Gun;
        OnWeaponChosen?.Invoke(SelectedWeapon);
    }
}
