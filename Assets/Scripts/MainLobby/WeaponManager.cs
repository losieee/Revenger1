using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager i;

    [HideInInspector] public bool canCrowbarSwitch = false;
    [HideInInspector] public bool canGunSwitch = false;
    [HideInInspector] public bool canBatSwitch = false;

    public enum WeaponType { None, Crowbar, Gun, Bat }
    public WeaponType SelectedWeapon { get; private set; } = WeaponType.None;

    public static event Action<WeaponType> OnWeaponChosen;

    public RectTransform target;
    public Vector2 newPosition;

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
        SoundManager.i?.PlaySFX(PlayerSfx.PickCrowbar, SfxBus.Effect, 1f);
        OnWeaponChosen?.Invoke(SelectedWeapon);
        
    }
    
    public void OnClickGun()
    {
        canGunSwitch = true;
        SelectedWeapon = WeaponType.Gun;
        SoundManager.i?.PlaySFX(PlayerSfx.PickGun, SfxBus.Effect, 1f);
        OnWeaponChosen?.Invoke(SelectedWeapon);
    }

    public void OnClickBat()
    {
        canBatSwitch = true;
        SelectedWeapon = WeaponType.Bat;
        SoundManager.i?.PlaySFX(PlayerSfx.PickBat, SfxBus.Effect, 1f);
        OnWeaponChosen?.Invoke(SelectedWeapon);
    }
    public void MoveToPosition()
    {
        if (target != null)
            target.anchoredPosition = newPosition;
    }

}
