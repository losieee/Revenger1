using UnityEngine;

public class ItemInfo : MonoBehaviour
{
    public string itemId;
    public string displayName;
    public Sprite icon;             // ID별로 다른 아이콘(스프라이트)

    [HideInInspector] public Vector3 startPos;
    [HideInInspector] public Quaternion startRot;

    void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
    }
}
