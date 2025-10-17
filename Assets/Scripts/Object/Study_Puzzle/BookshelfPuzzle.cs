using UnityEngine;

public class BookshelfPuzzle : MonoBehaviour
{
    [Header("왼→오 순서대로 슬롯 4개")]
    public BookSlot[] slots = new BookSlot[4];

    [Header("정답 정의 (둘 중 하나만 쓰면 됨)")]
    public string[] answerIds = new string[4];                   // 정확히 특정 책 ID 조합
    public ItemInfo.ItemType[] answerTypes = new ItemInfo.ItemType[4]; // 타입 조합(예: 전부 Book 또는 섞어서)

    [Header("퍼즐 완료 시켜야 할 것들(문 열기 등)")]
    public GameObject onSuccessEnable;
    public GameObject onFailFlash;   // 실패 피드백용(선택)

    public bool IsReady => slots != null && slots.Length == 4 && System.Array.TrueForAll(slots, s => s != null);

    // 슬롯이 4칸 다 찼을 때 호출하면 됨
    public void Validate()
    {
        if (!IsReady) return;
        for (int i = 0; i < 4; i++) if (!slots[i].IsFilled) return; // 아직 다 안 꽂힘

        bool ok = CheckAnswer();

        if (ok) Success();
        else FailAndReturnAll();
    }

    bool CheckAnswer()
    {
        // 1) ID 기준 검증(모든 칸에 id 지정이 되어 있으면 이걸로 판정)
        bool allIdsGiven = answerIds != null && answerIds.Length == 4 && System.Array.TrueForAll(answerIds, id => !string.IsNullOrEmpty(id));
        if (allIdsGiven)
        {
            for (int i = 0; i < 4; i++)
            {
                var cur = slots[i].current;
                if (!cur || cur.itemId != answerIds[i]) return false;
            }
            return true;
        }

        // 2) 타입 기준 검증(타입 배열이 4칸이면 타입으로 판정)
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

        // 둘 다 정의 안하면 항상 true로 두지 말고 안전하게 false
        Debug.LogWarning("[BookshelfPuzzle] 정답이 설정되지 않았습니다.");
        return false;
    }

    void Success()
    {
        // 퍼즐 성공: 문 열기, 이펙트 등
        if (onSuccessEnable) onSuccessEnable.SetActive(true);
        // 더 이상 건드리지 못하게 하려면
        PlayerInventory.PickupsLocked = true;
        Debug.Log("BookshelfPuzzle: SUCCESS");
    }

    void FailAndReturnAll()
    {
        // 실패: 슬롯의 책들을 인벤토리로 되돌리고 슬롯 비우기
        for (int i = 0; i < 4; i++)
        {
            var taken = slots[i].Take();
            if (taken)
            {
                // 인벤토리로 복귀(비활성화 + 목록에 추가)
                PlayerInventory.Instance.Return(taken);
            }
        }

        if (onFailFlash) onFailFlash.SetActive(true); // 잠깐 켰다 끄는 연출은 별도 스크립트에서
        Debug.Log("BookshelfPuzzle: FAIL → all books returned.");
    }

    // 편의: 언제든 현재 꽂힌 수
    public int FilledCount()
    {
        int c = 0;
        for (int i = 0; i < slots.Length; i++) if (slots[i] && slots[i].IsFilled) c++;
        return c;
    }
}
