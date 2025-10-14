using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CatchBoxRelay : MonoBehaviour
{
    public EnemyMov owner;

    private void OnTriggerEnter(Collider other)
    {
        if (!owner) return;
        if (!other.CompareTag("Player")) return;
        owner.OnPlayerEnteredCatchBox(other.transform);
    }
}
