using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KeyManager : MonoBehaviour
{
    public static KeyManager i;

    [SerializeField] Text keyText;
    [SerializeField] Text stage2InsideText;

    public int keyCount;
    public int stage2Count;
    public bool canInBedroom;
    public bool canShoot;

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

    public  void Add2StageKey(int amount = 1)
    {
        stage2Count += amount;
        stage2InsideText.text = $"ㅇ 열쇠조각을 획득하여 침실 열쇠 획득 ({stage2Count.ToString()}/3)";

        if (stage2Count >= 3)
        {
            canShoot = true;
            stage2InsideText.text = $"ㅇ 사격장으로 이동하여 프리드리히 호프만 제압";
        }
    }
}
