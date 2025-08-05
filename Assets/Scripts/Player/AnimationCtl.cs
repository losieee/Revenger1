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
        {
            float wallHeight = player.detectedWallHeight;
            float baseWall = 2.147f;
            float baseJump = 0.5f;

            float height = baseJump + (wallHeight - baseWall);
            height = Mathf.Clamp(height, 0.4f, 2.0f);

            player.MoveUpDuringHold(height, 0.1f);
        }
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

    public void BoxJumpEnd()
    {
        if (player != null)
            player.OnBoxJumpEnd();
    }

    public void MoveToBoxTop()
    {
        if (player != null)
            player.MoveToBoxTop(0.06f);
    }
    
    public void MoveToBoxTopRemaining()
    {
        if (player != null)
            player.MoveToBoxTopRemaining(0.07f);
    }
}
