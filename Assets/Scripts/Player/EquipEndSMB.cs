using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EquipEndSMB : StateMachineBehaviour
{
    // 필요한 경우 특정 상태만 걸러내고 싶다면 해시/태그로 체크 가능
    public string targetLayerName = "TakeWeapon";

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 레이어 이름으로 필터(필요 없으면 이 부분 삭제)
        int takeLayer = animator.GetLayerIndex(targetLayerName);
        if (takeLayer >= 0 && layerIndex != takeLayer) return;

        var pm = animator.GetComponentInParent<PlayerMov>();
        if (pm) pm.EquipEndCleanup();
    }
}
