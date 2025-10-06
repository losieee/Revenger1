using UnityEngine;

public class ItemInfo : MonoBehaviour
{
    [Tooltip("슬롯에서 요구하는 ID와 일치해야 놓을 수 있음")]
    public string itemId;
    [Tooltip("인벤토리에 보일 이름(선택)")]
    public string displayName;

    // 원위치로 되돌릴 때 사용 (선택)
    [HideInInspector] public Vector3 startPos;
    [HideInInspector] public Quaternion startRot;

    void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
    }
}
