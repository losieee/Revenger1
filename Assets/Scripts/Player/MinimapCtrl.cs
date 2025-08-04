using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinimapCtrl : MonoBehaviour
{
    Transform player;

    Vector3 offset;
    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.Find("Player").GetComponent<Transform>();

        offset = transform.position - player.transform.position;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (player != null)
            transform.position = player.position + offset;
    }
}
