using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KeyManager : MonoBehaviour
{
    public static KeyManager i;

    [SerializeField] Text keyText;

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
        keyText.text = keyCount.ToString();

        if (keyCount >= 7)
        {
            canInBedroom = true;
        }
    }
}
