using UnityEngine;
using TMPro; // TextMeshPro를 사용하는 경우

public class TextChanger : MonoBehaviour
{
    // TextMeshPro 컴포넌트 참조
    public TMP_Text targetText;

    void Awake()
    {
        // 스크립트가 붙어있는 오브젝트에서 컴포넌트를 자동으로 찾습니다.
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    // 애니메이션 이벤트에서 호출될 함수 (텍스트 내용을 변경합니다)
    public void SetLoadingText(string newText)
    {
        if (targetText != null)
        {
            targetText.text = newText;
        }
    }
}