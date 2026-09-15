using UnityEngine;
using Mirror;

public class ChapterBlocker : NetworkBehaviour
{
    [Header("설정")]
    [Tooltip("이 벽이 가로막고 있는 챕터 번호 (예: 2챕터 입구라면 2)")]
    public int targetChapterNumber;

    [Tooltip("오픈 시 재생할 소리 (선택 사항)")]
    public AudioClip openSound;

    [Tooltip("오픈 시 파티클 이펙트 (선택 사항)")]
    public GameObject openEffectPrefab;
    [SyncVar(hook = nameof(OnDoorStateChanged))]
    public bool isOpen = false;
    public override void OnStartServer()
    {
        // [DEMO VERSION] 데모 버전에선 3챕터 이상으로 통하는 물리적 문(Blocker)이 절대 열리지 않도록 강제로 막습니다.
        if (targetChapterNumber >= 3)
        {
            isOpen = false;
            return;
        }

        int maxCleared = 0;
        if (GameSaveManager.Instance != null)
        {
            maxCleared = GameSaveManager.Instance.currentData.maxClearedChapter;
        }
        int roomSelectedChapter = PrivateLobbyManager.selectedChapter;
        bool isExBlocker = targetChapterNumber >= 7;
        bool isExRoom = roomSelectedChapter >= 7;

        // =========================================================================
        // 🌟 챕터 벽 개방 조건 (2가지를 모두 만족해야 함):
        // 1. 방장이 이 챕터 직전까지 클리어한 기록이 있는가? (maxCleared >= targetChapterNumber - 1)
        // 2. 방장이 이번 방을 설정할 때 이 챕터 이하로 선택했는가? (targetChapterNumber <= roomSelectedChapter)
        // 단, EX 방(7번 이상)은 커맨드로 열 수 있으므로 세이브 클리어 기록 대신 방 선택값으로 개방합니다.
        // =========================================================================
        if ((isExBlocker && isExRoom && targetChapterNumber <= roomSelectedChapter) ||
            (!isExBlocker && maxCleared >= targetChapterNumber - 1 && targetChapterNumber <= roomSelectedChapter))
        {
            Debug.Log($"[ChapterBlocker] {targetChapterNumber} 챕터 개방! (클리어 기록: {maxCleared}, 선택한 챕터: {roomSelectedChapter})");
            isOpen = true;
        }
        else
        {
            Debug.Log($"[ChapterBlocker] {targetChapterNumber} 챕터 잠금! (클리어 기록: {maxCleared}, 선택한 챕터: {roomSelectedChapter})");
            isOpen = false;
        }
    }

    private void OnDoorStateChanged(bool oldState, bool newState)
    {
        if (newState == true)
        {
            OpenBlocker();
        }
    }

    private void OpenBlocker()
    {
        if (openEffectPrefab != null)
        {
            Instantiate(openEffectPrefab, transform.position, Quaternion.identity);
        }

        if (openSound != null)
        {
            AudioSource.PlayClipAtPoint(openSound, transform.position);
        }
        gameObject.SetActive(false);
    }
}
