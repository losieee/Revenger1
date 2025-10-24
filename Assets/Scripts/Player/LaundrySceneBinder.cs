using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


[DefaultExecutionOrder(0)]
public class LaundrySceneBinder : MonoBehaviour
{
    [Header("세탁실 퍼즐용 회전 타겟들 (Cube1~8 등)")]
    public Transform[] targetCubes;

    [Header("세탁실 퍼즐 Result 오브젝트")]
    public GameObject resultObjectInScene;

    [Header("세탁실 퍼즐 카메라 타겟 (LaundryPivot)")]
    public Transform cameraPivotInScene;

    [Space(12)]
    [Header("다른 미션 카메라 타겟들")]
    public Transform foyerCamTarget;
    public Transform studyCamTarget;
    public Transform studyResultCamTarget;
    public Transform guestCamTarget;

    void Awake()
    {
        // 플레이어 / 매니저 찾기
        var player = GameBootstrap.i?.player ?? FindObjectOfType<PlayerMov>();
        var mgr = player ? player.GetComponentInChildren<LaundryPuzzleManager>(true) : null;
        if (!player) return;

        // 이름으로 기본 채우기
        if (!cameraPivotInScene)
            cameraPivotInScene = GameObject.Find("LaundryPivot")?.transform;
        if (!foyerCamTarget)
            foyerCamTarget = GameObject.Find("FoyerCameraPivot")?.transform;
        if (!studyCamTarget)
            studyCamTarget = GameObject.Find("Study_CamPivot")?.transform;
        if (!studyResultCamTarget)
            studyResultCamTarget = GameObject.Find("StudyResultCamPivot")?.transform;
        if (!guestCamTarget)
            guestCamTarget = GameObject.Find("GuestCameraPivot")?.transform;

        // 세탁실 퍼즐 매니저 바인딩
        if (mgr)
        {
            var targets = targetCubes?.Select(t => t ? t.gameObject : null).ToArray();
            mgr.BindSceneObjects(targets, resultObjectInScene, cameraPivotInScene);
        }

        //플레이어의 미션용 카메라 목적지만 세팅
        if (cameraPivotInScene) player.laundryCamTarget = cameraPivotInScene;
        if (foyerCamTarget) player.foyerCamTarget = foyerCamTarget;
        if (studyCamTarget) player.studyCamTarget = studyCamTarget;
        if (studyResultCamTarget) player.studyResultCamTarget = studyResultCamTarget;
        if (guestCamTarget) player.guestCamTarget = guestCamTarget;
    }
}
