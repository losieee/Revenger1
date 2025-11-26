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
        keyText.text = $"ㅇ 열쇠조각을 획득하여 침실 열쇠 획득 ({keyCount.ToString()}/7)";

        if (keyCount >= 7)
        {
            canInBedroom = true;
            keyText.text = $"ㅇ 침실로 이동하여 베르너 슈타인 제압";
        }
    }
}
