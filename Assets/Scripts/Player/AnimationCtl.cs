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
            float baseWall = 1.87f;
            float baseJump = 0.181f;
            float remaining = player.remainingWallHeight;

            // 차이만큼 보정
            float jumpHeight = baseJump + (remaining - baseWall);
            jumpHeight = Mathf.Clamp(jumpHeight, 0.0f, 1.0f); // 0 이상으로

            player.MoveUpDuringHold(jumpHeight, 0.06f);
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

    public void AllowHoldCancel()
    {
        if (player != null)
            player.AllowHoldCancel();
    }

    public void BlockHoldCancel()
    {
        if (player != null)
            player.BlockHoldCancel();
    }

    public void JumpForWall()
    {
        if (player != null)
            player.AE_AttachToWall();
    }

    public void Crowbar_Assassinate_Hit()
    {
        if (player != null)
            player.OnAssassinationHit();
    }

    public void Crowbar_Assassinate_End()
    {
        if (player != null)
            player.OnAssassinationEnd();
    }

    public void PickupFinished()
    {
        if (player != null)
            player.AE_PickupFinished();
    }
    public void PickupLockOn()
    {
        if (player != null)
            player.AE_PickupLockOn();
    }
    public void PickupLockOff()
    {
        if (player != null)
            player.AE_PickupLockOff();
    }
}
