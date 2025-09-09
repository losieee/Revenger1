using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager i;

    [HideInInspector] public bool canSwitch = false;

    void Awake()
    {
        if (i != null && i != this) { Destroy(gameObject); return; } // �ߺ� ����
        i = this;
        DontDestroyOnLoad(gameObject); // �� �Ѿ�� ����
    }

    public void OnClickCrowbar()
    {
        canSwitch = true;
    }
}
