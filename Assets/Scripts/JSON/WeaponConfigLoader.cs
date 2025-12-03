using System.Collections.Generic;
using UnityEngine;

public class WeaponConfigLoader : MonoBehaviour
{
    public static WeaponConfigLoader i;

    [Header("Weapon JSON")]
    public TextAsset weaponJson;

    public List<WeaponJsonData> weaponList = new List<WeaponJsonData>();
    public Dictionary<int, WeaponJsonData> weaponById = new Dictionary<int, WeaponJsonData>();

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; }
        i = this;
        DontDestroyOnLoad(gameObject);

        LoadWeaponJson();
    }

    void LoadWeaponJson()
    {
        if (weaponJson == null)
            return;
        

        string wrapped = "{\"weapons\":" + weaponJson.text + "}";

        WeaponJsonWrapper wrapper = JsonUtility.FromJson<WeaponJsonWrapper>(wrapped);

        if (wrapper == null || wrapper.weapons == null)
            return;
        

        weaponList.Clear();
        weaponById.Clear();

        foreach (var w in wrapper.weapons)
        {
            weaponList.Add(w);
            weaponById[w.id] = w;
        }
    }

    public WeaponJsonData GetWeapon(int id)
    {
        weaponById.TryGetValue(id, out var data);
        return data;
    }
}
