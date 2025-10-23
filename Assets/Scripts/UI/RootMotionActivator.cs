using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RootMotionActivator : MonoBehaviour
{
    // 깜빡일 6개의 UI 오브젝트를 Inspector에서 할당합니다.
    public List<GameObject> uiObjectsToFlicker = new List<GameObject>();
    public int flickerCount = 2; // 깜빡일 횟수
    public float onDuration = 0.1f; // 켜져 있을 시간 간격
    public float offDuration = 0.1f; // 꺼져 있을 시간 간격

    // 패널이 활성화될 때 이 함수를 호출합니다.
    public void StartFlicker()
    {
        // 이미 실행 중인 코루틴이 있다면 중지 (선택 사항)
        StopAllCoroutines();
        StartCoroutine(FlickerRoutine());
    }

    private IEnumerator FlickerRoutine()
    {
        // 초기 상태를 모두 켜짐으로 설정 (만약 패널 활성화 시 바로 깜빡임을 시작하려면)
        SetObjectsActive(true);

        for (int i = 0; i < flickerCount; i++)
        {
            // 끄기
            SetObjectsActive(false);
            yield return new WaitForSeconds(offDuration); // 꺼진 상태 유지

            // 켜기
            SetObjectsActive(true);
            yield return new WaitForSeconds(onDuration); // 켜진 상태 유지
        }

        // 최종적으로 모두 켜진 상태로 유지 (깜빡임 루프가 끝났으므로)
        SetObjectsActive(true);
    }

    // 6개 오브젝트의 활성화 상태를 한 번에 설정하는 헬퍼 함수
    private void SetObjectsActive(bool isActive)
    {
        foreach (GameObject uiObject in uiObjectsToFlicker)
        {
            if (uiObject != null)
            {
                uiObject.SetActive(isActive);
            }
        }
    }

    // (선택 사항) 패널 활성화 시 자동으로 깜빡임을 시작하려면
    private void OnEnable()
    {
        // StartFlicker(); // 패널 GameObject에 이 스크립트가 있다면 활성화 시 자동 시작
    }
}