using System.Collections;
using System.Collections.Generic;
using Epic.OnlineServices.Lobby;
using EpicTransport;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class PrivateLobbyManager
{
    [Header("데모 잠금 안내 UI")]
    [Tooltip("직접 만든 TMP를 연결하세요. 폰트와 번역 문구는 변경하지 않고 표시 여부만 제어합니다.")]
    [SerializeField] private TMP_Text demoReleaseNotice;

    public void OnClick_PrevChapter()
    {
        selectedChapterIndex = GetNextSelectableChapterIndex(selectedChapterIndex, -1);
        UpdateChapterUI();
    }

    public void OnClick_NextChapter()
    {
        selectedChapterIndex = GetNextSelectableChapterIndex(selectedChapterIndex, 1);
        UpdateChapterUI();
    }

    private int GetNextSelectableChapterIndex(int currentIndex, int direction)
    {
        if (maxChapterCount <= 0) return 1;

        bool exStageUnlocked = IsExStageUnlocked();
        int candidate = currentIndex;
        int guard = maxChapterCount;

        do
        {
            candidate += direction;
            if (candidate < 1) candidate = maxChapterCount;
            if (candidate > maxChapterCount) candidate = 1;

            if (DemoManager.IsDemoMode || exStageUnlocked || !IsExChapter(candidate))
                return candidate;

            guard--;
        }
        while (guard > 0);

        return 1;
    }

    private bool IsExChapter(int chapterIndex)
    {
        int arrayIndex = chapterIndex - 1;
        string currentChapterName = (chapterNames != null && arrayIndex >= 0 && arrayIndex < chapterNames.Length) ? chapterNames[arrayIndex] : "";
        return currentChapterName.Contains("EX") || chapterIndex >= 7;
    }

    // PrivateLobbyManager.cs의 UpdateChapterUI 메서드 부분을 아래와 같이 보완합니다.

    private void UpdateChapterUI()
    {
        int displayChapter = selectedChapterIndex;
        int arrayIndex = selectedChapterIndex - 1;

        if (chapterDisplayText != null)
        {
            if (chapterNames != null && arrayIndex >= 0 && arrayIndex < chapterNames.Length && !string.IsNullOrEmpty(chapterNames[arrayIndex]))
            {
                chapterDisplayText.text = chapterNames[arrayIndex];
            }
            else
            {
                chapterDisplayText.text = $"Chapter {displayChapter}";
            }
        }

        if (chapterPreviewImage != null && chapterSprites != null && chapterSprites.Length > arrayIndex)
            chapterPreviewImage.sprite = chapterSprites[arrayIndex];

        bool isUnlocked = true;

        // ★ EX 스테이지 또는 특정 고난도 챕터(예: EX 6챕 등) 클리어 체크 조건 예시
        // 만약 챕터 이름에 "EX"가 포함되어 있거나 특정 인덱스 이상일 때의 조건 처리
        string currentChapterName = (chapterNames != null && arrayIndex >= 0 && arrayIndex < chapterNames.Length) ? chapterNames[arrayIndex] : "";
        bool isExStage = currentChapterName.Contains("EX") || displayChapter >= 7; // EX 스테이지 판별 조건 (프로젝트에 맞게 조절)

        if (GameSaveManager.Instance != null)
        {
            int maxCleared = GameSaveManager.Instance.currentData.maxClearedChapter;

            if (isExStage)
            {
                if (!IsExStageUnlocked())
                {
                    isUnlocked = false;
                }
            }
            else
            {
                if (maxCleared < arrayIndex) isUnlocked = false;
            }
        }

        bool demoLocked = !DemoManager.AllowsChapter(displayChapter);
        if (demoLocked) isUnlocked = false;
        if (demoReleaseNotice != null) demoReleaseNotice.gameObject.SetActive(demoLocked);
        if (chapterLockObject != null) chapterLockObject.SetActive(!isUnlocked);
        if (makeRoomButton != null) makeRoomButton.interactable = isUnlocked;
        if (chapterPreviewImage != null) chapterPreviewImage.color = isUnlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);
    }

}
