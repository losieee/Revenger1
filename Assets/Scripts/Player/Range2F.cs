using UnityEngine;

public class Range2F : MonoBehaviour
{
    public static Range2F i;

    public bool inTwoFloor;

    private void Awake()
    {
        i = this;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            inTwoFloor = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            inTwoFloor = false;
        }
    }
}
