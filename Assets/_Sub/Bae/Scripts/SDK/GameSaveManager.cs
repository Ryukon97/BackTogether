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
        // [DEMO VERSION] 데모용 세이브 파일 이름으로 변경하여 본편과 세이브 연동을 분리합니다.
        string saveFileName = "BackTogetherDemoSaveData.json";

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
        // [DEMO VERSION] 2챕터 클리어 시 3챕터가 열리는 것을 물리적으로 원천 차단합니다.
        // 저장 파일에도 1챕터까지만 클리어 된 것으로 기록합니다. (즉 2챕터까지만 개방됨)
        if (chapterNumber >= 1)
        {
            chapterNumber = 1; // 최대치를 1챕터 클리어로 강제 고정
        }

        // 1. 내 최고 기록 갱신 및 저장
        if (chapterNumber > currentData.maxClearedChapter)
        {
            currentData.maxClearedChapter = chapterNumber;
            SaveGame();
        }

        // 🌟 2. 추가된 로직: 현재 파놓은 방의 "선택된 챕터 한계치"도 같이 올려줍니다!
        // [DEMO VERSION] 단, 3챕터 이상으로 열리는 것은 막습니다. (최대 2까지만 허용)
        int nextChapter = chapterNumber + 1;
        if (nextChapter > 2) nextChapter = 2; // 다음 챕터가 3이 되려고 하면 2로 억제

        if (PrivateLobbyManager.selectedChapter <= chapterNumber)
        {
            PrivateLobbyManager.selectedChapter = nextChapter;
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

            // [DEMO VERSION] 개발자들의 기존 올클리어 세이브 데이터로 인해 
            // 로비 UI나 물리적 문이 열리는 것을 방지하기 위해 로드 즉시 최대 클리어 챕터를 1로 깎습니다.
            if (currentData.maxClearedChapter > 1)
            {
                currentData.maxClearedChapter = 1;
            }

            Debug.Log($"[Load] 세이브 로드 성공! 최고 챕터: {currentData.maxClearedChapter}");
        }
        else
        {
            Debug.Log("[Load] 세이브 파일이 없어 새로 생성합니다.");
            SaveGame();
        }
    }
}