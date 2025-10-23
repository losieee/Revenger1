using UnityEngine;

public class ReturnSpot : MonoBehaviour
{
    [Header("되돌릴 아이템 ID")]
    public string itemId;

    [Header("드롭 좌표")]
    public Transform dropPoint;

    public bool TryInteract()
    {
        var inv = PlayerInventory.Instance;
        if (!inv) return false;

        // 인벤토리에 해당 ID가 없으면 실패
        if (!inv.TryTakeById(itemId, out var item)) return false;

        // 월드로 복귀
        Vector3 pos = dropPoint ? dropPoint.position : item.startPos;
        Quaternion rot = dropPoint ? dropPoint.rotation : item.startRot;

        var go = item.gameObject;
        go.transform.SetPositionAndRotation(pos + Vector3.up * 0.02f, rot);

        // 콜라이더/물리 복구
        var col = go.GetComponent<Collider>(); if (col) col.enabled = true;
        var rb = go.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        go.SetActive(true);

        return true;
    }
}
