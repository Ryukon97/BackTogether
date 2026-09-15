using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using EpicTransport;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // 씬 초기화용 네임스페이스 추가
using UnityEngine.UI;

public partial class ClientLobbyManager
{
    public void OnSearchInputChanged(string input) { ApplyFiltersAndRefresh(); }

    // ★ PrivateLobbyManager의 chapterNames 배열을 보호 수준 에러(CS0122) 없이 안전하게 가져오는 헬퍼 메서드 (리플렉션 사용)
    private string[] GetChapterNamesFromPrivateManager()
    {
        if (privateLobbyManager == null) return null;
        try
        {
            var field = privateLobbyManager.GetType().GetField("chapterNames", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(privateLobbyManager) as string[];
            }
            var prop = privateLobbyManager.GetType().GetProperty("chapterNames", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prop != null)
            {
                return prop.GetValue(privateLobbyManager) as string[];
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ClientLobbyManager] chapterNames 취득 실패: {e.Message}");
        }
        return null;
    }

    // PrivateLobbyManager의 챕터 개수를 동적으로 가져옴
    private int GetMaxChapterCount()
    {
        string[] names = GetChapterNamesFromPrivateManager();
        if (names != null && names.Length > 0)
        {
            return names.Length;
        }
        return 6; // 기본값
    }

    private bool IsExChapter(int chapterIndex)
    {
        if (chapterIndex <= 0) return false;
        int arrayIndex = chapterIndex - 1;
        string[] names = GetChapterNamesFromPrivateManager();
        string currentChapterName = (names != null && arrayIndex >= 0 && arrayIndex < names.Length) ? names[arrayIndex] : "";
        return currentChapterName.Contains("EX") || chapterIndex >= 7;
    }

    public void OnClick_PrevFilterChapter()
    {
        int maxCh = GetMaxChapterCount();
        selectedFilterChapter = GetNextFilterChapterIndex(selectedFilterChapter, -1, maxCh);

        UpdateFilterChapterUI();
        ApplyFiltersAndRefresh();
    }

    public void OnClick_NextFilterChapter()
    {
        int maxCh = GetMaxChapterCount();
        selectedFilterChapter = GetNextFilterChapterIndex(selectedFilterChapter, 1, maxCh);

        UpdateFilterChapterUI();
        ApplyFiltersAndRefresh();
    }

    private int GetNextFilterChapterIndex(int currentIndex, int direction, int maxCh)
    {
        if (maxCh <= 0) return 0;

        bool exStageUnlocked = PrivateLobbyManager.IsExStageUnlockedForCurrentPlayer();
        int candidate = currentIndex;
        int guard = maxCh + 1;

        do
        {
            candidate += direction;
            if (candidate < 0) candidate = maxCh;
            if (candidate > maxCh) candidate = 0;

            if (candidate == 0 || exStageUnlocked || !IsExChapter(candidate))
                return candidate;

            guard--;
        }
        while (guard > 0);

        return 0;
    }

    private void UpdateFilterChapterUI()
    {
        if (filterChapterText != null)
        {
            if (selectedFilterChapter == 0)
            {
                filterChapterText.text = "Chapter All";
            }
            else
            {
                int index = selectedFilterChapter - 1;
                string[] names = GetChapterNamesFromPrivateManager();
                if (names != null && index >= 0 && index < names.Length && !string.IsNullOrEmpty(names[index]))
                {
                    filterChapterText.text = names[index];
                }
                else
                {
                    filterChapterText.text = $"Chapter {selectedFilterChapter}";
                }
            }
        }
    }

    private void ApplyFiltersAndRefresh()
    {
        filteredLobbies.Clear();
        string searchKey = (searchInputField != null) ? searchInputField.text.Trim().ToLower() : "";

        string[] chapterNames = GetChapterNamesFromPrivateManager();

        bool exStageUnlocked = PrivateLobbyManager.IsExStageUnlockedForCurrentPlayer();

        foreach (var lobby in allFetchedLobbies)
        {
            if (lobby == null) continue;

            string roomName = GetLobbyAttribute(lobby, "ROOM_NAME", "");
            if (!string.IsNullOrEmpty(searchKey) && !roomName.ToLower().Contains(searchKey))
                continue;

            string chapterStr = GetLobbyAttribute(lobby, "CHAPTER", "");
            string chapterNameAttr = GetLobbyAttribute(lobby, "CHAPTER_NAME", "");

            int chapterNum = 0;
            int.TryParse(chapterStr, out chapterNum);
            if (!DemoManager.AllowsChapter(chapterNum)) continue;

            // ★ [핵심 요구사항] EX 스테이지 / 6챕 클리어 전까지 검색 결과에서 숨기기 로직
            bool isExStage = chapterStr.Contains("EX") || chapterNameAttr.Contains("EX") || chapterNum >= 7;
            if (!isExStage && chapterNames != null && chapterNum > 0 && chapterNum <= chapterNames.Length)
            {
                if (chapterNames[chapterNum - 1].Contains("EX"))
                {
                    isExStage = true;
                }
            }

            if (isExStage && !exStageUnlocked)
            {
                continue;
            }

            // ★ 기존 챕터 필터링 로직
            if (selectedFilterChapter > 0)
            {
                int targetChapter = selectedFilterChapter;
                int targetArrayIndex = targetChapter - 1;
                string targetName = (chapterNames != null && targetArrayIndex >= 0 && targetArrayIndex < chapterNames.Length)
                    ? chapterNames[targetArrayIndex]
                    : null;

                bool isChapterMatch = false;

                if (!string.IsNullOrEmpty(chapterStr))
                {
                    if (int.TryParse(chapterStr, out int chVal))
                    {
                        // 방 생성 시 CHAPTER는 1부터 저장한다. 0-based 값까지 허용하면
                        // Chapter 1 방이 Chapter 2 필터에도 포함되는 중복 검색이 발생한다.
                        isChapterMatch = chVal == targetChapter;
                    }

                    if (!isChapterMatch && !string.IsNullOrEmpty(targetName))
                    {
                        if (string.Equals(chapterStr.Trim(), targetName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                            isChapterMatch = true;

                        if (!isChapterMatch && string.Equals(chapterNameAttr.Trim(), targetName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                            isChapterMatch = true;
                    }

                    string normalizedChapter = chapterStr.Replace(" ", "").Trim();
                    if (!isChapterMatch && string.Equals(normalizedChapter, $"Chapter{targetChapter}", System.StringComparison.OrdinalIgnoreCase))
                    {
                        isChapterMatch = true;
                    }
                }

                if (!isChapterMatch)
                    continue;
            }

            filteredLobbies.Add(lobby);
        }

        currentPage = 0;
        RefreshUI();
        RestoreFocusAfterPublicListRefresh();
    }

}
