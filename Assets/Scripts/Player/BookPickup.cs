using UnityEngine;

public class BookPickup : MonoBehaviour
{
    public PlayerMov player;
    public Camera cam;
    public float maxDistance = 5f;
    public LayerMask pickableMask; // Book + ReturnSpot 포함

    void Awake()
    {
        // cam이 비어있다면 MainCamera를 자동으로 찾음
        if (cam == null)
        {
            cam = Camera.main;
        }

        // player 자동 연결 (혹시 누락될 경우 대비)
        if (player == null)
        {
            player = FindObjectOfType<PlayerMov>();
        }
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (cam == null)
        {
            cam = Camera.main;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, pickableMask))
            return;

        // 1) 아이템 줍기 / 되돌리기
        var pickup = hit.collider.GetComponentInParent<PickupItem>();
        if (pickup && pickup.TryPickupOrReturn(player))
            return;

        // 2) ReturnSpot 처리
        var spot = hit.collider.GetComponentInParent<BookReturnSpot>();
        if (spot != null)
        {
            var inv = PlayerInventory.Instance;
            if (inv != null && inv.TryTakeById(spot.BookId, out ItemInfo taken))
            {
                taken.gameObject.SetActive(true);
                taken.transform.position = taken.startPos;
                taken.transform.rotation = taken.startRot;
                spot.gameObject.SetActive(false);
            }
        }
    }
}
