using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadingWaiting : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(WaitLoading());
    }

    IEnumerator WaitLoading()
    {
        yield return new WaitForSeconds(5f);

        GameObject.Find("Player").GetComponentInChildren<ButtonControl>().LoadSceneSimple("1_stage_out");
    }
}
