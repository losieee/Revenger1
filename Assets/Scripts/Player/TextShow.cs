using UnityEngine;

public class TextShow : MonoBehaviour
{
    Animator anim;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }
    private void OnEnable()
    {
        anim.SetTrigger("Show");
    }
}
