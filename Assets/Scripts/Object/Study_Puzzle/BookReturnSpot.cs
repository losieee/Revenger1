using UnityEngine;

public class BookReturnSpot : MonoBehaviour
{
    [SerializeField] private string bookId;
    public string BookId => bookId;     // ClickToPickup에서 읽어 씀

    // 책을 먹을 때 활성화할 용도
    public void ActivateSpot(string id)
    {
        bookId = id;
        gameObject.SetActive(true);
    }
}