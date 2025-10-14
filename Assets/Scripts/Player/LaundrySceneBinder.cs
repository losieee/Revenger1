using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class LaundrySceneBinder : MonoBehaviour
{
    [Header("씬에 있는 회전 타겟들 (Cube1~Cube8)")]
    public Transform[] targetCubes;

    [Header("씬에 있는 Result 오브젝트")]
    public GameObject resultObjectInScene;

    [Header("씬에 있는 pivot 오브젝트")]
    public Transform cameraPivotInScene;

    void Awake()
    {
        // 플레이어 / 매니저 찾기
        var player = GameBootstrap.i?.player ?? FindObjectOfType<PlayerMov>();
        var mgr = player ? player.GetComponentInChildren<LaundryPuzzleManager>(true) : null;
        if (!mgr || !player) return;

        // 피벗이 비어있으면 이름으로도 한 번 더 시도
        if (!cameraPivotInScene)
        {
            var go = GameObject.Find("LaundryPivot");
            if (go) cameraPivotInScene = go.transform;
        }

        // 씬 타겟을 GameObject 배열로 변환
        var targets = targetCubes?.Select(t => t ? t.gameObject : null).ToArray();

        // 1) 퍼즐 타겟/리절트 바인딩(+ 카메라 피벗 같이 전달)
        mgr.BindSceneObjects(targets, resultObjectInScene, cameraPivotInScene);

        // 2) 플레이어 쪽에도 직접 세팅(퍼즐 뷰 진입 시 사용)
        if (cameraPivotInScene)
        {
            player.laundryCamTarget = cameraPivotInScene; // ← 이것 때문에 null이면 이동 후 끊겨 보임
            player.BindCameraPivot(cameraPivotInScene);   // 카메라 팔로우 피벗도 교체(선택)
        }
    }
}
