using System.Collections;
using UnityEngine;

public class OutToHole : MonoBehaviour
{
    bool canWarp = false;
    void Update()
    {
        if (canWarp && Input.GetKeyDown(KeyCode.E))
        {
            SceneTransit.Go("1_stage_outToGarden", "GardenUnderGround");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canWarp = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            canWarp = false;
        }
    }
}
