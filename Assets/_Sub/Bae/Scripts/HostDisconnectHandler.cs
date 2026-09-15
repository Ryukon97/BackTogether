using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EpicTransport;

public class HostDisconnectHandler : MonoBehaviour
{
    [Header("UI 연결")]
    public GameObject disconnectPanel;
    public Button disconnectButton; // 패널 내부의 [로비로 돌아가기] 버튼 연결용

    [Header("설정")]
    public string lobbySceneName = "Main";

    private bool wasConnected = false;
    private bool isIntentionalExit = false;
    private bool isConnecting = false;
    private bool isReturningToLobby = false;

    private void Start()
    {
        // 시작 시 디스커넥트 팝업 비활성화
        if (disconnectPanel != null)
            disconnectPanel.SetActive(false);

        // disconnectButton 인스펙터 미할당 대비 자동 탐색
        if (disconnectButton == null && disconnectPanel != null)
        {
            disconnectButton = disconnectPanel.GetComponentInChildren<Button>();
        }

        // 처음에는 아직 실제 게임 연결을 감시하지 않음
        wasConnected = false;
        isConnecting = false;
        isIntentionalExit = false;
    }

    private void Update()
    {
        if (DemoManager.Ending) return;
        // 0. 디스커넥트 팝업이 열려 있는 동안 입력 처리
        if (disconnectPanel != null && disconnectPanel.activeSelf)
        {
            if (IsSubmitPressed())
            {
                GoBackToLobby();
                return;
            }
            return;
        }

        // 호스트 자신은 감지하지 않음
        if (NetworkServer.active)
            return;

        // 의도적인 퇴장이면 무시
        if (isIntentionalExit)
            return;

        // ---------------------------------------------------------
        // ★ 중요:
        // 새로운 방에 접속하는 중에는 NetworkClient.isConnected가
        // 잠깐 false가 되는 것을 호스트 Disconnect로 판단하지 않는다.
        // ---------------------------------------------------------
        if (isConnecting)
        {
            // 실제 연결에 성공하면 접속 완료 상태로 전환
            if (NetworkClient.isConnected)
            {
                isConnecting = false;
                wasConnected = true;

                Debug.Log("[HostDisconnectHandler] 새 방 연결 성공 → Disconnect 감시 시작");
            }

            return;
        }

        // ---------------------------------------------------------
        // 1. 정상적으로 연결되어 있는 상태
        // ---------------------------------------------------------
        if (NetworkClient.isConnected)
        {
            wasConnected = true;
            return;
        }

        // ---------------------------------------------------------
        // 2. 실제로 연결되어 있다가 끊긴 경우만 감지
        // ---------------------------------------------------------
        if (!wasConnected)
            return;

        wasConnected = false;

        Debug.Log("[HostDisconnectHandler] 실제 연결 이후 Disconnect 감지");

        // ---------------------------------------------------------
        // 호스트 Disconnect 패널 활성화
        // ---------------------------------------------------------
        if (disconnectPanel != null && !disconnectPanel.activeSelf)
        {
            EmojiRadialMenu.Instance?.ForceClose();
            PlayerEmojiController.ForceHideAll();

            disconnectPanel.SetActive(true);

            if (disconnectButton == null)
            {
                disconnectButton = disconnectPanel.GetComponentInChildren<Button>();
            }

            if (GlobalSceneInputManager.Instance != null)
            {
                GlobalSceneInputManager.Instance.SetFocusScope(disconnectPanel);
            }

            FocusDisconnectButton();
        }
    }

    private void LateUpdate()
    {
        // EventSystem이나 외부 인풋 스크립트에 의해 포커스가 해제되거나 뒤쪽 UI로 빠지는 것을 강제 보정
        if (disconnectPanel != null && disconnectPanel.activeSelf)
        {
            if (EventSystem.current != null)
            {
                GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

                // 포커스가 null이거나 disconnectPanel 외부 UI를 가리키고 있을 때 고정
                if (currentSelected == null || !currentSelected.transform.IsChildOf(disconnectPanel.transform))
                {
                    FocusDisconnectButton();
                }
            }
        }
    }

    /// <summary>
    /// Input System 패키지를 사용한 키보드 및 게임패드 입력 체크
    /// </summary>
    private bool IsSubmitPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    private void FocusDisconnectButton()
    {
        if (EventSystem.current != null && disconnectButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(disconnectButton.gameObject);
        }
    }

    /// <summary>
    /// 디스커넥트 팝업 창의 [확인 / 로비로 돌아가기] 버튼 OnClick에 연결
    /// </summary>
    public void GoBackToLobby()
    {
        if (isReturningToLobby) return;
        StartCoroutine(GoBackToLobbyRoutine());
    }

    private IEnumerator GoBackToLobbyRoutine()
    {
        isReturningToLobby = true;

        EmojiRadialMenu.Instance?.ForceClose();
        PlayerEmojiController.ForceHideAll();

        wasConnected = false;
        isConnecting = false;
        isIntentionalExit = false;

        EOSLobby eosLobby = FindAnyObjectByType<EOSLobby>(FindObjectsInactive.Include);
        if (eosLobby != null && eosLobby.ConnectedToLobby)
        {
            eosLobby.LeaveLobby();

            float leaveTimeout = 5f;
            while (eosLobby.IsLeavingLobby && leaveTimeout > 0f)
            {
                leaveTimeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (NetworkManager.singleton != null && NetworkClient.active)
        {
            NetworkManager.singleton.StopClient();
        }

        if (disconnectPanel != null)
            disconnectPanel.SetActive(false);

        yield return new WaitForSecondsRealtime(0.2f);

        float sceneChangeTimeout = 5f;
        while (SceneManager.GetActiveScene().name != lobbySceneName &&
               NetworkManager.loadingSceneAsync != null &&
               sceneChangeTimeout > 0f)
        {
            sceneChangeTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (SceneManager.GetActiveScene().name != lobbySceneName)
        {
            SceneManager.LoadScene(lobbySceneName);
        }

        isReturningToLobby = false;
    }

    /// <summary>
    /// Pause 메뉴 등에서 유저가 스스로 방을 나갈 때 호출
    /// </summary>
    public void SetIntentionalExit()
    {
        isIntentionalExit = true;
    }

    /// <summary>
    /// ★ 에러의 원인이었던 함수! (ClientLobbyManager가 부르는 이름과 똑같이 맞췄습니다)
    /// 새로운 방에 접속하기 시작할 때 호출.
    /// 이전 연결의 Disconnect 상태를 초기화하고 새로운 연결이 완료될 때까지 감지를 막습니다.
    /// </summary>
    public void BeginConnectionAttempt()
    {
        isConnecting = true;
        wasConnected = false;
        isIntentionalExit = false;

        if (disconnectPanel != null)
            disconnectPanel.SetActive(false);

        Debug.Log("[HostDisconnectHandler] 새 방 접속 시작 → Disconnect 감시 초기화");
    }
}
