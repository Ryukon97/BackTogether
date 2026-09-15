using System.Collections;
using System.IO;
using EpicTransport;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Put this component on the Demo object in Main. The Inspector checkbox is
// sampled once at startup; an optional UI Toggle takes precedence.
[DefaultExecutionOrder(-10000)]
public class DemoManager : MonoBehaviour
{
    [SerializeField] private bool demoMode = true;
    [SerializeField] private Toggle demoToggle;
    [Header("직접 제작한 UI (Demo 오브젝트 아래 Canvas에 배치)")]
    [Tooltip("씬 이동 후에도 유지되도록 Demo 오브젝트의 자식 Canvas 아래에 배치하세요.")]
    [SerializeField] private GameObject demoEndPanel;
    [SerializeField] private Button endWishlistButton;
    [SerializeField] private Button returnToMainButton;
    [SerializeField] private Button screenWishlistButton;
    [Tooltip("직접 설정한 TMP의 폰트와 번역 문구를 그대로 사용하며 표시 여부만 제어합니다.")]
    [SerializeField] private TMP_Text demoLabel;
    [SerializeField] private string mainSceneName = "Main";
    public const string StoreUrl = "https://store.steampowered.com/app/5086830/BACK_TOGETHER/";
    public static DemoManager Instance { get; private set; }
    public static bool IsDemoMode { get; private set; }
    public static bool Ending { get; private set; }
    private bool endingVisible;
    private bool returning;
    private Coroutine focusRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Instance = null;
        IsDemoMode = false;
        Ending = false;
        DemoEndScreen.shouldShowEndScreen = false;
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        IsDemoMode = demoToggle != null ? demoToggle.isOn : demoMode;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (Instance != this) return;
        if (endWishlistButton != null) endWishlistButton.onClick.AddListener(OpenWishlist);
        if (screenWishlistButton != null) screenWishlistButton.onClick.AddListener(OpenWishlist);
        if (returnToMainButton != null) returnToMainButton.onClick.AddListener(ReturnToMain);
        RefreshUI();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (endWishlistButton != null) endWishlistButton.onClick.RemoveListener(OpenWishlist);
        if (screenWishlistButton != null) screenWishlistButton.onClick.RemoveListener(OpenWishlist);
        if (returnToMainButton != null) returnToMainButton.onClick.RemoveListener(ReturnToMain);
        if (Instance == this) Instance = null;
    }

    public static bool AllowsChapter(int chapter) => !IsDemoMode || (chapter >= 1 && chapter <= 2);
    public static bool AllowsScene(string scene)
    {
        if (!IsDemoMode) return true;
        string name = Path.GetFileNameWithoutExtension(scene).ToLowerInvariant();
        if (name.StartsWith("n")) name = name.Substring(1);
        if (name.StartsWith("ex")) return false;
        if (!name.StartsWith("chapter")) return true;
        string digits = "";
        foreach (char c in name.Substring(7)) { if (!char.IsDigit(c)) break; digits += c; }
        return int.TryParse(digits, out int chapter) && AllowsChapter(chapter);
    }

    public static void OpenWishlist() { Application.OpenURL(StoreUrl); }

    public static void BeginEnding()
    {
        if (Instance == null || Ending) return;
        Ending = true;
        // Set the destination before another peer can shut down the session.
        if (NetworkManager.singleton != null)
            NetworkManager.singleton.offlineScene = Instance.mainSceneName;
        var handler = FindAnyObjectByType<HostDisconnectHandler>();
        if (handler != null) handler.SetIntentionalExit();
        Instance.StartCoroutine(Instance.ShowEndingAfterClear());
    }

    private IEnumerator ShowEndingAfterClear()
    {
        yield return new WaitForSecondsRealtime(2f);
        yield return ReturnRoutine(true);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        bool showEnd = Ending && endingVisible &&
            SceneManager.GetActiveScene().name == mainSceneName;
        if (demoEndPanel != null) demoEndPanel.SetActive(showEnd);
        if (screenWishlistButton != null)
            screenWishlistButton.gameObject.SetActive(IsDemoMode && !showEnd);
        if (demoLabel != null)
            demoLabel.gameObject.SetActive(IsDemoMode && !showEnd &&
                SceneManager.GetActiveScene().name == mainSceneName);
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        focusRoutine = null;
        if (showEnd && demoEndPanel != null && demoEndPanel.activeInHierarchy)
            focusRoutine = StartCoroutine(FocusEndingPanel());
    }

    private IEnumerator FocusEndingPanel()
    {
        // Let sceneLoaded handlers finish resetting the global focus first.
        yield return null;
        if (!Ending || !endingVisible || demoEndPanel == null || !demoEndPanel.activeInHierarchy)
            yield break;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        EmojiRadialMenu.Instance?.ForceClose();
        var input = GlobalSceneInputManager.Instance;
        if (input != null)
        {
            input.UnlockUI();
            input.SetFocusScope(demoEndPanel);
        }
        else if (EventSystem.current != null && returnToMainButton != null &&
                 returnToMainButton.isActiveAndEnabled && returnToMainButton.IsInteractable())
        {
            EventSystem.current.SetSelectedGameObject(returnToMainButton.gameObject);
        }
        focusRoutine = null;
    }

    public void ReturnToMain()
    {
        if (returning) return;
        if (SceneManager.GetActiveScene().name == mainSceneName &&
            !NetworkServer.active && !NetworkClient.active)
        {
            CloseEndingPanel();
            return;
        }
        if (!Ending) StartCoroutine(ReturnRoutine(false));
    }

    private IEnumerator ReturnRoutine(bool showEnding)
    {
        returning = true;
        bool wasServer = NetworkServer.active;
        var handler = FindAnyObjectByType<HostDisconnectHandler>();
        if (handler != null) handler.SetIntentionalExit();
        // Clients receive the reliable door RPC and leave first. Keep the host
        // alive while they process it, rather than racing StopHost against delivery.
        if (showEnding && wasServer)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (HasRemoteClients() && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (HasRemoteClients())
                Debug.LogWarning("[Demo] Client departure timed out; completing host shutdown.");
        }
        var lobby = FindAnyObjectByType<EOSLobby>(FindObjectsInactive.Include);
        if (lobby != null && lobby.ConnectedToLobby)
        {
            if (wasServer) lobby.DestroyLobby();
            else lobby.LeaveLobby();
            float timeout = 5f;
            while (lobby != null && lobby.IsLeavingLobby && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }
        var manager = NetworkManager.singleton;
        if (manager != null)
        {
            manager.offlineScene = mainSceneName;
            if (NetworkServer.active && NetworkClient.active) manager.StopHost();
            else if (NetworkClient.active) manager.StopClient();
            else if (NetworkServer.active) manager.StopServer();
        }
        // Let Mirror finish its asynchronous offline scene transition first.
        yield return null;
        while (NetworkManager.loadingSceneAsync != null && !NetworkManager.loadingSceneAsync.isDone) yield return null;
        if (SceneManager.GetActiveScene().name != mainSceneName)
            yield return SceneManager.LoadSceneAsync(mainSceneName);
        // Main scene Start methods must finish before applying the modal scope.
        yield return null;
        returning = false;
        if (showEnding)
        {
            endingVisible = true;
            RefreshUI();
        }
        else CloseEndingPanel();
    }

    private static bool HasRemoteClients()
    {
        if (!NetworkServer.active) return false;
        foreach (var connection in NetworkServer.connections.Values)
            if (connection != null && connection != NetworkServer.localConnection) return true;
        return false;
    }

    private void CloseEndingPanel()
    {
        Ending = false;
        DemoEndScreen.shouldShowEndScreen = false;
        endingVisible = false;
        returning = false;
        RefreshUI();
        if (GlobalSceneInputManager.Instance != null)
        {
            GlobalSceneInputManager.Instance.ClearFocusScope();
            GlobalSceneInputManager.Instance.RefreshAllSelectables();
        }
    }

}
