using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class ButtonControl : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject backText;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Home");
    }

    public void ClickOkay()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        backText.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        backText.SetActive(false);
    }
}
