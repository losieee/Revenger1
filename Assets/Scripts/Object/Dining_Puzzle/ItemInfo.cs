using UnityEngine;

public class ItemInfo : MonoBehaviour
{
    public enum ItemType { Generic, Book, Key, Etc }

    [Header("기본 정보")]
    public string itemId;
    public string displayName;
    public Sprite icon;

    [Header("분류")]
    public ItemType type = ItemType.Generic;
    [SerializeField] public AudioClip pickupClip;

    [HideInInspector] public Vector3 startPos;
    [HideInInspector] public Quaternion startRot;

    public BookReturnSpot returnSpot;

    void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
    }
}
