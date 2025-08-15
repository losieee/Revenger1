using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class ButtonControl : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject backText;

    // �ν����Ϳ��� OnClick�� �����ϰ�, �� �̸��� ���ڿ��� �־��ּ���.
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        
        SceneManager.LoadScene(sceneName);

        Time.timeScale = 1.0f;
    }

    public void OnPointerEnter(PointerEventData eventData) => backText?.SetActive(true);
    public void OnPointerExit(PointerEventData eventData) => backText?.SetActive(false);
}
