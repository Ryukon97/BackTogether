using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror; // 미러 네트워크 기능 사용

public class DemoEndScreen : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("메인 메뉴 씬에 띄워줄 데모 엔딩 패널")]
    public GameObject demoEndPanel;
    
    [Tooltip("스팀 위시리스트 버튼")]
    public Button wishlistButton;
    
    [Tooltip("패널 닫기 버튼 (누르면 메인 메뉴 화면이 보임)")]
    public Button closePanelButton;

    [Header("Store Settings")]
    private string steamStoreUrl = "https://store.steampowered.com/app/5086830/BACK_TOGETHER/";

    // 2챕터를 클리어했을 때 메인 메뉴로 넘어가기 직전에 true로 바뀝니다.
    public static bool shouldShowEndScreen = false;

    private void Start()
    {
        // 메인 메뉴 씬이 열릴 때 이 스크립트가 실행되면서 확인합니다.
        if (shouldShowEndScreen)
        {
            if (demoEndPanel != null)
                demoEndPanel.SetActive(true);
                
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            
            // 한 번 보여줬으면 다음에 메인 메뉴로 올 때는 안 뜨도록 초기화
            shouldShowEndScreen = false;
        }
        else
        {
            if (demoEndPanel != null)
                demoEndPanel.SetActive(false);
        }

        // 버튼 이벤트 연결
        if (wishlistButton != null)
            wishlistButton.onClick.AddListener(OpenSteamStore);
            
        if (closePanelButton != null)
            closePanelButton.onClick.AddListener(ClosePanel);
    }

    private void OpenSteamStore()
    {
        Debug.Log("스팀 상점 페이지로 이동합니다: " + steamStoreUrl);
        Application.OpenURL(steamStoreUrl);
    }

    private void ClosePanel()
    {
        // 유저는 이미 메인 메뉴로 안전하게 돌아온 상태이므로, 패널만 꺼주면 됩니다.
        if (demoEndPanel != null)
            demoEndPanel.SetActive(false);
    }

    /// <summary>
    /// [핵심 기능] 2챕터를 클리어했을 때 어디서든 이 함수를 호출해주세요!
    /// 예: DemoEndScreen.TriggerDemoEndAndDisconnect();
    /// </summary>
    public static void TriggerDemoEndAndDisconnect()
    {
        // 1. 메인 메뉴로 돌아갔을 때 패널을 띄우라고 신호를 남깁니다. (static 변수라 씬이 넘어가도 유지됨)
        shouldShowEndScreen = true;

        // 2. 호스트/클라이언트 연결을 안전하게 끊습니다.
        // 미러(Mirror)는 연결이 끊기면 자동으로 Offline 씬(메인 메뉴)으로 돌아갑니다.
        if (NetworkManager.singleton != null)
        {
            if (NetworkServer.active && NetworkClient.isConnected)
                NetworkManager.singleton.StopHost();
            else if (NetworkClient.isConnected)
                NetworkManager.singleton.StopClient();
            else if (NetworkServer.active)
                NetworkManager.singleton.StopServer();
        }
        else
        {
            // 혹시 오프라인 테스트 중일 경우를 위한 안전장치
            SceneManager.LoadScene(0);
        }
    }
}
