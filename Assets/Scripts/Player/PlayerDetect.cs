using UnityEngine;

public static class PlayerDetect
{
    public static bool IsPlayer(GameObject go)
    {
        if (!go) return false;
        if (go.CompareTag("Player")) return true;
        var root = go.transform.root;
        if (root && root.CompareTag("Player")) return true;
        return go.GetComponentInParent<PlayerMov>() != null;
    }

    public static bool IsPlayer(Collider col)
    {
        if (!col) return false;
        if (col.CompareTag("Player")) return true;
        if (col.attachedRigidbody && col.attachedRigidbody.CompareTag("Player")) return true;
        if (col.transform.root && col.transform.root.CompareTag("Player")) return true;
        return col.GetComponentInParent<PlayerMov>() != null;
    }
}
