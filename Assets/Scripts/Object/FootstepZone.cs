using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FootstepZone : MonoBehaviour
{
    public FootEnv env = FootEnv.Indoor;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        var proxy = other.GetComponentInChildren<PlayerFootstepProxy>();
        proxy?.SetEnvironment(env);
    }
}
