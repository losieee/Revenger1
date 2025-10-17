using UnityEngine;

public class BookshelfPuzzle : MonoBehaviour
{
    [Header("왼→오 순서대로 슬롯 4개")]
    public BookSlot[] slots = new BookSlot[4];

    [Header("정답 정의 (둘 중 하나만 쓰면 됨)")]
    public string[] answerIds = new string[4];                         // 명시적 ID 조합
    public ItemInfo.ItemType[] answerTypes = new ItemInfo.ItemType[4]; // 타입 조합

    [Header("퍼즐 완료 시켜야 할 것들(문 열기 등)")]
    public GameObject onSuccessEnable;
    public GameObject onFailFlash;

    [Header("SFX")]
    public AudioClip successClip;
    public AudioClip failClip;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    public bool IsReady =>
        slots != null && slots.Length == 4 && System.Array.TrueForAll(slots, s => s != null);

    // BookPlaceController에서 부르는 함수 (4칸 다 채워지면 자동 판정)
    public void ValidateIfFull()
    {
        if (!IsReady) return;
        for (int i = 0; i < 4; i++) if (!slots[i].IsFilled) return;
        Validate();
    }

    // 슬롯이 4칸 다 찼을 때 호출하면 됨
    public void Validate()
    {
        if (!IsReady) return;
        for (int i = 0; i < 4; i++) if (!slots[i].IsFilled) return;

        bool ok = CheckAnswer();

        if (ok) Success();
        else FailAndReturnAll();
    }

    bool CheckAnswer()
    {
        // 1) ID 기준
        bool allIdsGiven = answerIds != null && answerIds.Length == 4 &&
                           System.Array.TrueForAll(answerIds, id => !string.IsNullOrEmpty(id));
        if (allIdsGiven)
        {
            for (int i = 0; i < 4; i++)
            {
                var cur = slots[i].current;
                if (!cur || cur.itemId != answerIds[i]) return false;
            }
            return true;
        }

        // 2) 타입 기준
        bool allTypesGiven = answerTypes != null && answerTypes.Length == 4;
        if (allTypesGiven)
        {
            for (int i = 0; i < 4; i++)
            {
                var cur = slots[i].current;
                if (!cur || cur.type != answerTypes[i]) return false;
            }
            return true;
        }

        Debug.LogWarning("[BookshelfPuzzle] 정답이 설정되지 않았습니다.");
        return false;
    }

    void Success()
    {
        PlaySfx(successClip);

        if (onSuccessEnable) onSuccessEnable.SetActive(true);
        PlayerInventory.PickupsLocked = true;

        Debug.Log("BookshelfPuzzle: SUCCESS");
    }

    void FailAndReturnAll()
    {
        PlaySfx(failClip);

        for (int i = 0; i < 4; i++)
        {
            var taken = slots[i].Take();
            if (taken)
            {
                PlayerInventory.Instance.Return(taken);
            }
        }

        if (onFailFlash) onFailFlash.SetActive(true);
        Debug.Log("BookshelfPuzzle: FAIL → all books returned.");
    }

    void PlaySfx(AudioClip clip)
    {
        if (!clip) return;
        AudioSource.PlayClipAtPoint(clip, transform.position, sfxVolume);
    }

    // 편의: 현재 꽂힌 수
    public int FilledCount()
    {
        int c = 0;
        for (int i = 0; i < slots.Length; i++) if (slots[i] && slots[i].IsFilled) c++;
        return c;
    }
}
