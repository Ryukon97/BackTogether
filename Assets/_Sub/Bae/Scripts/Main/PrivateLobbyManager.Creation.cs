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
    public void OnClick_MakeRoom()
    {
        if (!DemoManager.AllowsChapter(selectedChapterIndex)) { UpdateChapterUI(); return; }
        SubscribeEvents();
        if (isCreatingLobby) return;

        selectedChapter = selectedChapterIndex;
        var lobby = GetEOSLobby();
        if (lobby == null) { ShowErrorPopup("네트워크 시스템이 준비되지 않았습니다."); return; }

        NoCheckpointButtonController noCheckpointController =
            FindAnyObjectByType<NoCheckpointButtonController>(FindObjectsInactive.Include);

        createWithoutCheckpoints = noCheckpointController != null &&
                                   noCheckpointController.IsNoCheckpointSelected;

        if (noCheckpointController != null)
        {
            noCheckpointController.ApplySelectionToNetworkManager();
        }
        else if (NetworkManager.singleton != null)
        {
            // 컨트롤러가 없거나 비활성인 씬에서도 일반 로비가 기본값이 되도록 보장한다.
            NetworkManager.singleton.onlineScene = lobbySceneName;
            createWithoutCheckpoints = false;
        }

        string roomTitle = (roomNameInputField != null && !string.IsNullOrEmpty(roomNameInputField.text))
     ? roomNameInputField.text
     : GetRandomDefaultRoomName();

        lastCreatedRoomName = roomTitle;
        lastCreatedRoomIsPublic = isPublicRoom;

        isCreatingLobby = true;
        createLobbySuccess = false;

        SetAllButtonsInteractable(false);
        currentPanel = GetLoadingPanel();
        if (currentPanel != null) currentPanel.SetActive(true);

        StartCoroutine(CreateLobbyWhenEOSReadyRoutine(lobby, roomTitle));
    }

    private IEnumerator CreateLobbyWhenEOSReadyRoutine(EOSLobby lobby, string roomTitle)
    {
        float eosReadyTimeout = 10f;
        while (!EOSSDKComponent.IsEOSReady() && eosReadyTimeout > 0f)
        {
            eosReadyTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!EOSSDKComponent.IsEOSReady() || EOSSDKComponent.LocalUserProductId == null)
        {
            isCreatingLobby = false;
            SetAllButtonsInteractable(true);
            if (currentPanel != null) currentPanel.SetActive(false);
            ShowErrorPopup("EOS 네트워크 초기화 시간이 초과되었습니다. 잠시 후 다시 시도해주세요.");
            yield break;
        }

        lobby.CreateLobby(4, LobbyPermissionLevel.Publicadvertised, false, null);
        yield return StartCoroutine(CreateLobbyAndSetAttributesRoutine(roomTitle));
    }

    private void OnCreateLobbySucceeded(List<Epic.OnlineServices.Lobby.Attribute> attributes)
    {
        createLobbySuccess = true;
        isCreatingLobby = false;
    }

    private void OnCreateLobbyFailed(string errorMessage)
    {
        createLobbySuccess = false;
        isCreatingLobby = false;
        SetAllButtonsInteractable(true);
        if (currentPanel != null) currentPanel.SetActive(false);
        ShowErrorPopup("방 생성 실패: " + errorMessage);
    }
    private void OnLobbyAttributesUpdateSucceeded() { attributeUpdateDone = true; attributeUpdateFailed = false; }
    private void OnLobbyAttributesUpdateFailed(string errorMessage) { attributeUpdateDone = true; attributeUpdateFailed = true; }

    private IEnumerator CreateLobbyAndSetAttributesRoutine(string roomTitle)
    {
        float timeout = 5f;
        // 결과(성공 또는 실패)가 나올 때까지 대기
        while (isCreatingLobby && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }

        // 타임아웃 처리
        if (isCreatingLobby)
        {
            SetAllButtonsInteractable(true);
            if (currentPanel != null) currentPanel.SetActive(false);
            ShowErrorPopup("방 생성 시간이 초과되었습니다.");
            yield break;
        }

        // ★ 실패했다면 여기서 코루틴을 중단해야 합니다!
        if (!createLobbySuccess)
        {
            yield break;
        }

        attributeUpdateDone = false; attributeUpdateFailed = false;
        int chapterArrayIndex = selectedChapterIndex - 1;
        string selectedChapterName =
            chapterNames != null &&
            chapterArrayIndex >= 0 &&
            chapterArrayIndex < chapterNames.Length &&
            !string.IsNullOrEmpty(chapterNames[chapterArrayIndex])
                ? chapterNames[chapterArrayIndex]
                : $"Chapter {selectedChapterIndex}";

        // 퍼블릭/프라이빗 상관없이 스팀 친구 초대를 위해 항상 숏코드를 발급합니다.
        currentShortCode = GenerateShortCode();

        List<AttributeData> attrDataList = new List<AttributeData>();
        attrDataList.Add(new AttributeData { Key = "ROOM_NAME", Value = roomTitle });
        attrDataList.Add(new AttributeData { Key = "CHAPTER", Value = selectedChapterIndex.ToString() });
        attrDataList.Add(new AttributeData { Key = "CHAPTER_NAME", Value = selectedChapterName });
        attrDataList.Add(new AttributeData { Key = "IS_PUBLIC", Value = isPublicRoom ? "1" : "0" });
        attrDataList.Add(new AttributeData { Key = "NO_CHECKPOINT", Value = createWithoutCheckpoints ? "1" : "0" });
        
        // 퍼블릭 방이어도 스팀 초대를 위해 숏코드 속성을 부여합니다.
        attrDataList.Add(new AttributeData { Key = "SHORTCODE", Value = currentShortCode });

        var lobby = GetEOSLobby();
        if (lobby != null) lobby.UpdateLobbyAttributes(attrDataList.ToArray());

        float attributeTimeout = 5f;
        while (!attributeUpdateDone && attributeTimeout > 0f) { attributeTimeout -= Time.unscaledDeltaTime; yield return null; }

        if (attributeUpdateFailed || attributeTimeout <= 0f)
        {
            if (lobby != null && lobby.ConnectedToLobby) lobby.DestroyLobby();
            SetAllButtonsInteractable(true);
            if (currentPanel != null) currentPanel.SetActive(false);
            ShowErrorPopup("방 정보를 설정하지 못했습니다. 다시 시도해주세요.");
            yield break;
        }

        EosTransport transport = NetworkManager.singleton.transport as EosTransport;
        if (transport != null) transport.ResetIgnoreMessagesAtStartUpTimer();
        NetworkManager.singleton.StartHost();

        Debug.Log($"[PrivateLobbyManager] 방 생성 완료. 초대 매니저 연동 시도 중... (Instance: {PlatformInviteManager.Instance != null}, isPublic: {isPublicRoom}, shortCode: {currentShortCode})");

        // [Steam / Stove 초대 지원] 퍼블릭/프라이빗 상관없이 항상 스팀 로비를 파서 우클릭 초대를 활성화합니다.
        if (PlatformInviteManager.Instance != null && !string.IsNullOrEmpty(currentShortCode))
        {
            PlatformInviteManager.Instance.SetLobbyDataForInvite(currentShortCode);
        }
        else
        {
            Debug.LogWarning("[PrivateLobbyManager] 초대 매니저 연동이 스킵되었습니다. 조건을 확인해주세요.");
        }

        yield return new WaitForSecondsRealtime(0.2f);
        SetAllButtonsInteractable(true);
        if (currentPanel != null) currentPanel.SetActive(false);
    }

}
