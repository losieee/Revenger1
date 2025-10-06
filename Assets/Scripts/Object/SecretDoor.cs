using UnityEngine;

public class SecretDoor : MonoBehaviour
{
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void OpenSecret()
    {
        if (animator != null)
        {
            animator.SetTrigger("Open");
        }
    }
}
