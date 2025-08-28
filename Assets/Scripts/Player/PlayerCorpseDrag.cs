using UnityEngine;

public class PlayerCorpseDrag : MonoBehaviour
{
    [Header("����")]
    public KeyCode toggleKey = KeyCode.E;   // ��� Ű
    public float grabRange = 2f;            // ��ü ��� ����
    public LayerMask corpseHandleMask;      // ���� �� �ִ� Handle ���� Layer
    public Transform dragAnchor;            // �÷��̾� ���� ��Ŀ (������ �ڵ� ����)

    private PlayerMov playerMov;            // �÷��̾� �̵� ��ũ��Ʈ ����
    private DraggableCorpse grabbedCorpse;  // ���� ���� ��ü
    private bool isDragging = false;        // �巡�� ����

    void Start()
    {
        playerMov = GetComponent<PlayerMov>();

        // DragAnchor�� ������ �ڵ� ���� (�÷��̾� ���ʿ� ��ġ)
        if (!dragAnchor)
        {
            GameObject go = new GameObject("DragAnchor");
            dragAnchor = go.transform;
            dragAnchor.SetParent(transform);
            dragAnchor.localPosition = new Vector3(0, 0.1f, -0.8f);
        }
    }

    void Update()
    {
        // ���Ű �Է�
        if (Input.GetKeyDown(toggleKey))
        {
            if (isDragging) ReleaseCorpse();
            else TryGrabNearestCorpse();
        }
    }

    // �ֺ� ��ü �ڵ� ã�� ��� �õ�
    void TryGrabNearestCorpse()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, grabRange, corpseHandleMask);
        DraggableCorpse best = null;
        float bestDist = float.MaxValue;

        foreach (var h in hits)
        {
            var corpse = h.GetComponentInParent<DraggableCorpse>();
            if (corpse && corpse.IsDeadAndDraggable)
            {
                float d = Vector3.Distance(transform.position, h.ClosestPoint(transform.position));
                if (d < bestDist)
                {
                    best = corpse;
                    bestDist = d;
                }
            }
        }

        if (best != null && best.TryGrab(dragAnchor))
        {
            grabbedCorpse = best;
            isDragging = true;
            playerMov.OnDragStart();
        }
    }

    // ��ü ����
    void ReleaseCorpse()
    {
        if (grabbedCorpse) { grabbedCorpse.Release(); grabbedCorpse = null; }
        isDragging = false;
        playerMov.OnDragStop();
    }
}
