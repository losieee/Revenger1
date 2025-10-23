using UnityEngine;

public class BookReturnSpot : MonoBehaviour
{
    [SerializeField] private string bookId;
    private bool hasBook = false;       // 현재 책이 놓여 있는가

    public string BookId => bookId;
    public bool HasBook() => hasBook;

    public void ActivateSpot(string id)
    {
        bookId = id;
        gameObject.SetActive(true);
        hasBook = false;            // 활성화되면 비어 있는 상태로 시작
    }

    public void MarkFilled(string id)
    {
        bookId = id;
        hasBook = true;
    }
}
