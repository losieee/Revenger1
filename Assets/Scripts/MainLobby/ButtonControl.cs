using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class ButtonControl : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject backText;

    // 인스펙터에서 OnClick에 연결하고, 씬 이름을 문자열로 넣어주세요.
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        
        SceneManager.LoadScene(sceneName);

        Time.timeScale = 1.0f;
    }

    public void OnPointerEnter(PointerEventData eventData) => backText?.SetActive(true);
    public void OnPointerExit(PointerEventData eventData) => backText?.SetActive(false);
}
