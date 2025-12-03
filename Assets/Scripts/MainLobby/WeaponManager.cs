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

    [Header("JSON/Player Reference")]
    public WeaponConfigLoader config;
    public PlayerMov player;

    [Header("Weapon Id Mapping (JSON id)")]
    public int crowbarId = 2;
    public int gunId = 1;
    public int batId = 3;

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; } // 중복 방지
        i = this;
        DontDestroyOnLoad(gameObject); // 씬 넘어가도 유지
    }

    void Start()
    {
        if (config == null) config = WeaponConfigLoader.i;
        if (player == null) player = FindObjectOfType<PlayerMov>();
    }

    public void OnClickCrowbar()
    {
        canCrowbarSwitch = true;
        SelectedWeapon = WeaponType.Crowbar;
        SoundManager.i?.PlaySFX(PlayerSfx.PickCrowbar, SfxBus.Effect, 1f);
        ApplyWeaponByType(SelectedWeapon);
        OnWeaponChosen?.Invoke(SelectedWeapon);
        
    }
    
    public void OnClickGun()
    {
        canGunSwitch = true;
        SelectedWeapon = WeaponType.Gun;
        SoundManager.i?.PlaySFX(PlayerSfx.PickGun, SfxBus.Effect, 1f);
        ApplyWeaponByType(SelectedWeapon);
        OnWeaponChosen?.Invoke(SelectedWeapon);
    }

    public void OnClickBat()
    {
        canBatSwitch = true;
        SelectedWeapon = WeaponType.Bat;
        SoundManager.i?.PlaySFX(PlayerSfx.PickBat, SfxBus.Effect, 1f);
        ApplyWeaponByType(SelectedWeapon);
        OnWeaponChosen?.Invoke(SelectedWeapon);
    }

    public void MoveToPosition()
    {
        if (target != null)
            target.anchoredPosition = newPosition;
    }

    void ApplyWeaponByType(WeaponType type)
    {
        if (config == null || player == null)
            return;
        

        int id = 0;
        switch (type)
        {
            case WeaponType.Crowbar: id = crowbarId; break;
            case WeaponType.Gun: id = gunId; break;
            case WeaponType.Bat: id = batId; break;
            default: return;
        }

        var data = config.GetWeapon(id);
        if (data == null)
            return;
        
        player.ApplyWeaponStats(data);
    }
}
