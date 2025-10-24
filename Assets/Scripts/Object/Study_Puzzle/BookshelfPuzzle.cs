using System.Collections;
using UnityEngine;

public class BookshelfPuzzle : MonoBehaviour
{
    [Header("왼→오 순서대로 슬롯 4개")]
    public BookSlot[] slots = new BookSlot[4];

    [Header("정답 정의 (둘 중 하나만 쓰면 됨)")]
    public string[] answerIds = new string[4];                         // 명시적 ID 조합
    public ItemInfo.ItemType[] answerTypes = new ItemInfo.ItemType[4]; // 타입 조합

    [Header("비밀 정답 (맞추면 회전)")]
    public string[] secretIds = new string[4];
    public Transform secretTarget;             // 회전시킬 대상
    public Vector3 secretRotateAxis = Vector3.up;
    public float secretRotateAngle = -102f;
    public float secretRotateDuration = 0.4f;

    [Header("SFX")]
    public AudioClip normalSuccessClip;
    public AudioClip secretSuccessClip;
    public AudioClip failClip;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    public bool lockOnSuccess = true;                // 성공 시 잠금

    private bool solved;
    public bool IsSolved => solved;

    public bool IsReady =>
        slots != null && slots.Length == 4 && System.Array.TrueForAll(slots, s => s != null);

    // BookPlaceController에서 부르는 함수 (4칸 다 채워지면 자동 판정)
    public void ValidateIfFull()
    {
        if (solved) return;
        if (!IsReady) return;
        for (int i = 0; i < 4; i++) if (!slots[i].IsFilled) return;

        bool isSecret = CheckIds(secretIds);
        bool isNormal = CheckAnswer();

        if (isSecret)
        {
            RotateSecretTarget();
            PlaySfx(secretSuccessClip);
            MarkSolved();
        }

        if (isNormal)
        {
            PlaySfx(normalSuccessClip);
            MarkSolved();
        }
        else if (!isSecret)
        {
            FailAndReturnAll();
        }
    }

    // 슬롯이 4칸 다 찼을 때 호출하면 됨
    public void Validate()
    {
        if (solved) return;
        if (!IsReady) return;
        for (int i = 0; i < 4; i++) if (!slots[i].IsFilled) return;

        bool isSecret = CheckIds(secretIds);                        // 비밀 정답
        bool isNormal = CheckIds(answerIds) || CheckTypes();        // 일반 정답
            
        // 비밀 조합이면 회전
        if (isSecret)
        {
            RotateSecretTarget();
            PlaySfx(secretSuccessClip);
            MarkSolved();
        }

        if (isNormal)
        {
            PlaySfx(normalSuccessClip);
            MarkSolved();
        }
        else if (!isSecret)  // 둘 다 아니면 실패
        FailAndReturnAll();
    }

    void MarkSolved()
    {
        if (solved) return;
        solved = true;

        if (!lockOnSuccess) return;

        // 슬롯 상호작용 차단
        foreach (var s in slots)
        {
            if (!s) continue;

            // 1) 슬롯 자신의 상호작용 끄기
            s.enabled = false;

            // 2) 슬롯/자식의 클릭 스크립트가 따로 있다면 함께 끄기
            var click = s.GetComponentInChildren<BookSlotClick>(true);
            if (click) click.enabled = false;

            // 3) 슬롯/자식 콜라이더 끄기
            var colls = s.GetComponentsInChildren<Collider>(true);
            foreach (var c in colls) c.enabled = false;
        }
    }

    bool CheckIds(string[] ids)
    {
        if (ids == null || ids.Length != 4) return false;
        for (int i = 0; i < 4; i++)
        {
            if (string.IsNullOrEmpty(ids[i])) return false;
            var cur = slots[i].current;
            if (!cur || cur.itemId != ids[i]) return false;
        }
        return true;
    }

    bool CheckTypes()
    {
        if (answerTypes == null || answerTypes.Length != 4) return false;
        for (int i = 0; i < 4; i++)
        {
            var cur = slots[i].current;
            if (!cur || cur.type != answerTypes[i]) return false;
        }
        return true;
    }

    void RotateSecretTarget()
    {
        if (!secretTarget) return;
        StopAllCoroutines();
        StartCoroutine(RotateOverTime(secretTarget, secretRotateAxis.normalized, secretRotateAngle, secretRotateDuration));
    }

    IEnumerator RotateOverTime(Transform t, Vector3 axis, float angle, float duration)
    {
        Quaternion from = t.rotation;
        Quaternion to = Quaternion.AngleAxis(angle, axis) * from;

        float tmr = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (tmr < 1f)
        {
            tmr += Time.deltaTime / duration;
            t.rotation = Quaternion.Slerp(from, to, tmr);
            yield return null;
        }
        t.rotation = to;
    }

    bool CheckAnswer()
    {
        // 1) ID 기준
        bool allIdsGiven = answerIds != null && answerIds.Length == 4 &&
                       System.Array.TrueForAll(answerIds, id => !string.IsNullOrEmpty(id));
        if (allIdsGiven) return CheckIds(answerIds);


        // 2) 타입 기준
        bool allTypesGiven = answerTypes != null && answerTypes.Length == 4;
        if (allTypesGiven)
        {
            bool anySet = false;
            for (int i = 0; i < 4; i++)
            {
                if (answerTypes[i] != ItemInfo.ItemType.Generic)
                {
                    anySet = true;
                    break;
                }
            }
            if (anySet) return CheckTypes();
        }
        return false;
    }

    void FailAndReturnAll()
    {
        if (solved) return;
        PlaySfx(failClip);

        for (int i = 0; i < 4; i++)
        {
            var taken = slots[i].Take();
            if (taken)
            {
                PlayerInventory.Instance.Return(taken);
            }
        }
    }

    void PlaySfx(AudioClip clip)
    {
        if (!clip) return;
        AudioSource.PlayClipAtPoint(clip, transform.position, sfxVolume);
    }
}
