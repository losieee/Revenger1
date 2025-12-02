using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class MinimapPos : MonoBehaviour
{
    [SerializeField] Transform stage2Out;
    [SerializeField] Transform stage1Out;
    [SerializeField] GameObject cam;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            cam.transform.position = stage1Out.transform.position;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            cam.transform.position = stage2Out.transform.position;
        }
    }
}
