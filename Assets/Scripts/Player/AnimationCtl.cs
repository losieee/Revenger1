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

    public void JumpforHold()
    {
        if (player != null)
            player.MoveUpDuringHold(0.5f, 0.1f);
    }

    public void ForwardClimb()
    {
        if (player != null)
            player.MoveForwardAfterClimb(0.5f,0.38f);
    }

    public void OnClimbEnd()
    {
        if (player != null)
            player.OnClimbEnd();
    }
}
