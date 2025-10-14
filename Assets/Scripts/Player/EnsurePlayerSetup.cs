using UnityEngine;

[DefaultExecutionOrder(-2000)]
public class EnsurePlayerSetup : MonoBehaviour
{
    [Header("원하는 태그/레이어 이름")]
    public string playerTag = "Player";
    public string playerLayer = "Player";

    void Awake()
    {
        // 태그 강제
        if (gameObject.tag != playerTag)
            gameObject.tag = playerTag;

        // 레이어 강제
        int layer = LayerMask.NameToLayer(playerLayer);
        if (layer >= 0 && gameObject.layer != layer)
            gameObject.layer = layer;

        // 자식들에 Player 태그가 섞여있으면 충돌 나므로 전부 Untagged로 바꿔줌
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t == transform) continue;
            if (t.CompareTag(playerTag)) t.tag = "Untagged";
        }
    }
}