using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationCtl : MonoBehaviour
{
    public PlayerMov player;

    public void OnJumpingDownComplete()
    {
        if (player != null)
            player.OnJumpingDownComplete();
    }
}
