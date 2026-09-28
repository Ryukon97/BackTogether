using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class BGMManager : MonoBehaviour
{
    public static BGMManager instance;

    [System.Serializable]
    public class SceneTrack
    {
        public string sceneName;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("씬 이름 <-> 곡 매핑 (8개, 씬별 볼륨 포함)")]
    public SceneTrack[] tracks = new SceneTrack[]
    {
        new SceneTrack { sceneName = "Main" },
        new SceneTrack { sceneName = "Lobby" },
        new SceneTrack { sceneName = "chapter1" },
        new SceneTrack { sceneName = "chapter2" },
        new SceneTrack { sceneName = "chapter3" },
        new SceneTrack { sceneName = "chapter4" },
        new SceneTrack { sceneName = "chapter5" },
        new SceneTrack { sceneName = "chapter6" },
    };

    [Header("출력 믹서 그룹")]
    public AudioMixerGroup outputGroup;

    [Header("전체 마스터 배율 (씬별 volume에 곱해짐)")]
    [Range(0f, 1f)]
    public float targetVolume = 1f;

    private AudioSource source;
    private string currentSceneName;
    private bool isMutedForLoading = false;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        if (outputGroup != null) source.outputAudioMixerGroup = outputGroup;
    }

    void Start()
    {
        PlayForScene(SceneManager.GetActiveScene().name);
    }

    void Update()
    {
        // 로딩 패널이 떠 있는 동안은 이전 씬 브금이 계속 들리지 않게 음소거
        bool loadingActive = WalkingLoadingPanel.Instance != null && WalkingLoadingPanel.Instance.gameObject.activeInHierarchy;

        if (loadingActive != isMutedForLoading)
        {
            isMutedForLoading = loadingActive;
            source.mute = loadingActive;
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        PlayForScene(scene.name);
    }

    private void PlayForScene(string sceneName)
    {
        if (sceneName == currentSceneName) return;
        currentSceneName = sceneName;

        string trackSceneName = sceneName;
        if (sceneName == "NLobby")
        {
            trackSceneName = "Lobby";
        }
        else if (sceneName.StartsWith("Nchapter", System.StringComparison.OrdinalIgnoreCase) ||
                 sceneName.StartsWith("NEx", System.StringComparison.OrdinalIgnoreCase))
        {
            trackSceneName = sceneName.Substring(1);
        }

        SceneTrack track = null;
        foreach (SceneTrack t in tracks)
        {
            if (t.sceneName == trackSceneName)
            {
                track = t;
                break;
            }
        }

        if (track == null || track.clip == null)
        {
            source.Stop();
            return;
        }

        source.volume = targetVolume * track.volume;
        source.clip = track.clip;
        source.Play();
    }
}
