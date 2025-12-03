using System;

[Serializable]
public class WeaponJsonData
{
    public int id;
    public string weaponName;
    public bool weaponClimbWhether;
    public float weaponAttackRange;
    public int weaponAttack;
    public float weaponAttackSpeed;
    public float weaponWalkSpeed;
    public float weaponRunSpeed;
    public float weaponSitSpeed;
    public float weaponCrawlSpeed;
}

[Serializable]
public class WeaponJsonWrapper
{
    public WeaponJsonData[] weapons;
}