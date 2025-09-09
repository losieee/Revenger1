using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class SceneSpawnPoint : MonoBehaviour
{
    public string id = "Default";
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
    }
}
