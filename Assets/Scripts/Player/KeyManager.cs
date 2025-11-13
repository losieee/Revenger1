using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyManager : MonoBehaviour
{
    public static KeyManager i;

    public int keyCount;
    public bool canInBedroom;

    private void Awake()
    {
        if (i != null && i != this)
        {
            Destroy(gameObject);
            return;
        }
        i = this;
    }

    public void AddKey(int amount = 1)
    {
        keyCount += amount;

        if (keyCount >= 7)
        {
            canInBedroom = true;
        }
    }
}
