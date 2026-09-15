using UnityEngine;
using System.IO;

// ★ STOVE 빌드일 때만 스토브 SDK 네임스페이스를 참조합니다.
#if STOVE_BUILD
using static Stove.PCSDK.Base;
#endif

public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance { get; private set; }

    [System.Serializable]
    public class SaveData
    {
        public int maxClearedChapter = 0;
    }

    public SaveData currentData = new SaveData();
    private string saveFilePath;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        saveFilePath = GetSaveFilePath();

        LoadGame();
    }

    /// <summary>
    /// 플랫폼 환경에 맞는 세이브 파일 경로를 가져옵니다.
    /// 스토브 빌드에서는 SDK 클라우드 경로를 받아오고, 실패하거나 스팀 빌드인 경우 기본 LocalLow 경로를 사용합니다.
    /// </summary>
    private string GetSaveFilePath()
    {
        string saveFileName = DemoManager.IsDemoMode ? "BackTogetherDemoSaveData.json" : "BackTogetherSaveData.json";

#if STOVE_BUILD
        if (StovePCSDK3Manager.InstanceExists && StovePCSDK3Manager.Instance.isInitialized)
        {
            string cloudPath = string.Empty;
            uint length = 512;

            Result result = Base_GetCloudSavingPath(ref cloudPath, length);

            if (result.IsSuccessful() && !string.IsNullOrEmpty(cloudPath))
            {
                Debug.Log("[Stove] 스토브 클라우드 세이브 경로 획득 성공: " + cloudPath);
                return Path.Combine(cloudPath, saveFileName);
            }
            else
            {
                Debug.LogWarning("[Stove] 클라우드 경로 획득 실패. 기본 LocalLow 경로를 사용합니다.");
            }
        }
        else
        {
            Debug.LogWarning("[Stove] StovePCSDK3Manager가 초기화되지 않아 기본 LocalLow 경로를 사용합니다.");
        }
#endif

        // 스팀 빌드(STEAM_BUILD) 또는 스토브 경로 획득 실패 시 기본 LocalLow 경로 반환
        return Path.Combine(Application.persistentDataPath, saveFileName);
    }

    public void ClearChapter(int chapterNumber)
    {
        if (!DemoManager.AllowsChapter(chapterNumber)) return;
        // 1. 내 최고 기록 갱신 및 저장
        if (chapterNumber > currentData.maxClearedChapter)
        {
            currentData.maxClearedChapter = chapterNumber;
            SaveGame();
        }

        // 🌟 2. 추가된 로직: 현재 파놓은 방의 "선택된 챕터 한계치"도 같이 올려줍니다!
        // 이렇게 해야 메인으로 나가서 방을 다시 파지 않아도 다음 벽이 자동으로 열립니다.
        if (PrivateLobbyManager.selectedChapter <= chapterNumber)
        {
            PrivateLobbyManager.selectedChapter = DemoManager.IsDemoMode ? Mathf.Min(chapterNumber + 1, 2) : chapterNumber + 1;
        }
    }

    public void SaveGame()
    {
        string dir = Path.GetDirectoryName(saveFilePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonUtility.ToJson(currentData, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log($"[Save] 세이브 저장 완료! 경로: {saveFilePath}");
    }

    public void LoadGame()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            currentData = JsonUtility.FromJson<SaveData>(json);
            Debug.Log($"[Load] 세이브 로드 성공! 최고 챕터: {currentData.maxClearedChapter}");
        }
        else
        {
            Debug.Log("[Load] 세이브 파일이 없어 새로 생성합니다.");
            SaveGame();
        }
    }
}