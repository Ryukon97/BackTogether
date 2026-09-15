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
    public void JoinRoom(LobbyDetails lobby)
    {
        SubscribeEvents();

        if (IsConnecting)
        {
            Debug.LogWarning("[ClientLobbyManager] 이미 방 입장을 진행 중입니다.");
            return;
        }

        if (lobby == null)
            return;

        if (DemoManager.IsDemoMode &&
            (!int.TryParse(GetLobbyAttribute(lobby, "CHAPTER", ""), out int demoChapter) ||
             !DemoManager.AllowsChapter(demoChapter)))
        {
            // Demo-only rooms are filtered above; reject unsupported direct joins.
            return;
        }
        if (!EOSLobby.IsLobbyJoinable(
                lobby,
                out uint currentMembers,
                out uint maxMembers))
        {
            ShowFullRoomWarning();
            // ★ 방 입장 실패/거부 시 현재 선택된 버튼/요소의 포커스가 유실되거나 백그라운드로 새어나가지 않도록 현재 선택 상태 유지 또는 복구 처리
            return;
        }

        var eos = GetEOSLobby();

        if (eos == null)
        {
            ShowError("네트워크 시스템을 찾을 수 없습니다.");
            return;
        }

        HostDisconnectHandler disconnectHandler =
            FindAnyObjectByType<HostDisconnectHandler>();

        if (disconnectHandler != null)
        {
            disconnectHandler.BeginConnectionAttempt();
        }

        SetInteractableAll(false);
        IsConnecting = true;

        Debug.Log(
            $"[ClientLobbyManager] 방 입장 시작 | " +
            $"Members={currentMembers}/{maxMembers}"
        );

        eos.JoinLobby(lobby);
    }

    private void ShowFullRoomWarning()
    {
        if (fullRoomMessageText != null)
        {
            if (fullRoomMessageCoroutine != null) StopCoroutine(fullRoomMessageCoroutine);
            fullRoomMessageCoroutine = StartCoroutine(ShowFullRoomMessageRoutine());
        }
    }

    private IEnumerator ShowFullRoomMessageRoutine()
    {
        fullRoomMessageText.text = "인원수가 다 차서 못들어 갑니다";
        fullRoomMessageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(3f);
        fullRoomMessageText.gameObject.SetActive(false);
        fullRoomMessageCoroutine = null;
    }

    private void OnJoinLobbySucceeded(List<Epic.OnlineServices.Lobby.Attribute> attributes)
    {
        var eos = GetEOSLobby();
        if (eos != null && eos.ConnectedLobbyDetails != null)
        {
            Epic.OnlineServices.Lobby.Attribute attr;
            if (eos.ConnectedLobbyDetails.CopyAttributeByKey(new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = EOSLobby.hostAddressKey }, out attr) == Result.Success)
            {
                string hostAddress = attr.Data?.Value.AsUtf8 ?? "";
                if (!string.IsNullOrEmpty(hostAddress))
                {
                    EosTransport transport = NetworkManager.singleton.transport as EosTransport;
                    if (transport != null) transport.ResetIgnoreMessagesAtStartUpTimer();

                    HideAllPanels();

                    GameObject panel = GetLoadingPanel();
                    if (panel != null) panel.SetActive(true);

                    NetworkManager.singleton.networkAddress = hostAddress;
                    NetworkManager.singleton.StartClient();

                    if (connectionTimeoutCoroutine != null)
                        StopCoroutine(connectionTimeoutCoroutine);

                    connectionTimeoutCoroutine = StartCoroutine(CheckConnectionTimeout());

                    return;
                }
            }
        }

        if (eos != null && eos.ConnectedToLobby)
        {
            eos.LeaveLobby();
        }

        IsConnecting = false;
        SetInteractableAll(true);
        ShowError("방장의 주소 정보를 가져오지 못했습니다.");
    }

    private void OnJoinLobbyFailed(string error)
    {
        isLocalSearchRequest = false;

        isQuickJoining = false;
        IsConnecting = false;

        GameObject panel = GetLoadingPanel();
        if (panel != null) panel.SetActive(false);

        SetInteractableAll(true);

        if (NetworkManager.singleton != null && NetworkClient.active)
        {
            NetworkManager.singleton.StopClient();
        }

        var eos = GetEOSLobby();
        if (eos != null && eos.ConnectedToLobby)
        {
            eos.LeaveLobby();
        }

        ShowError("방 입장에 실패했습니다: " + error);
    }

}
