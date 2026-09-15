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
    private void SetAllButtonsInteractable(bool interactable)
    {
        if (mainHostButton != null) mainHostButton.interactable = interactable;
        if (makeRoomButton != null) makeRoomButton.interactable = interactable && DemoManager.AllowsChapter(selectedChapterIndex);
        if (prevChapterButton != null) prevChapterButton.interactable = interactable;
        if (nextChapterButton != null) nextChapterButton.interactable = interactable;
        if (prevRoomTypeButton != null) prevRoomTypeButton.interactable = interactable;
        if (nextRoomTypeButton != null) nextRoomTypeButton.interactable = interactable;
    }

    private GameObject GetLoadingPanel()
    {
        if (loadingPanel != null) return loadingPanel;
        if (WalkingLoadingPanel.Instance != null) { loadingPanel = WalkingLoadingPanel.Instance.gameObject; return loadingPanel; }
        return null;
    }

    private string GenerateShortCode()
    {
        string allowedChars = "0123456789";
        string code = "";
        for (int i = 0; i < 6; i++) code += allowedChars[Random.Range(0, allowedChars.Length)];
        return code;
    }

    private void ShowErrorPopup(string message)
    {
        if (errorPopupPanel != null)
        {
            if (errorMessageText != null) errorMessageText.text = message;
            errorPopupPanel.SetActive(true);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                if (errorCloseButton != null) EventSystem.current.SetSelectedGameObject(errorCloseButton.gameObject);
                else EventSystem.current.SetSelectedGameObject(errorPopupPanel);
            }
        }
    }

    public void OnClick_CloseErrorPopup()
    {
        if (errorPopupPanel != null) errorPopupPanel.SetActive(false);

        if (GlobalSceneInputManager.Instance != null)
        {
            if (hostPanel != null && hostPanel.activeSelf)
            {
                GlobalSceneInputManager.Instance.SetFocusScope(hostPanel);
            }
            else if (mainPanel != null && mainPanel.activeSelf)
            {
                GlobalSceneInputManager.Instance.SetFocusScope(mainPanel);
            }
        }
    }

    public void OnClick_ReturnToMain()
    {
        if (isReturningToMain) return;
        StartCoroutine(ReturnToMainRoutine());
    }

    private IEnumerator ReturnToMainRoutine()
    {
        isReturningToMain = true;

        NoCheckpointButtonController noCheckpointController =
            FindAnyObjectByType<NoCheckpointButtonController>(FindObjectsInactive.Include);
        if (noCheckpointController != null)
        {
            noCheckpointController.ResetSelectionToDefault();
        }

        var lobby = GetEOSLobby();
        if (lobby != null && lobby.ConnectedToLobby)
        {
            if (NetworkServer.active && NetworkClient.active) lobby.DestroyLobby();
            else if (NetworkClient.active) lobby.LeaveLobby();
            else lobby.DestroyLobby();

            float leaveTimeout = 5f;
            while (lobby.IsLeavingLobby && leaveTimeout > 0f)
            {
                leaveTimeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (NetworkManager.singleton != null)
        {
            if (NetworkServer.active)
            {
                NetworkManager.singleton.StopHost();
            }
            else if (NetworkClient.active)
            {
                NetworkManager.singleton.StopClient();
            }
        }

        CloseVirtualKeyboard();
        if (hostPanel != null) hostPanel.SetActive(false);
        if (logo != null) logo.SetActive(true);
        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
            if (GlobalSceneInputManager.Instance != null) GlobalSceneInputManager.Instance.SetFocusScope(mainPanel);
        }
        if (errorPopupPanel != null) errorPopupPanel.SetActive(false);

        isReturningToMain = false;
    }

    private static void CloseVirtualKeyboard()
    {
        VirtualKeyboardManager keyboardManager =
            FindAnyObjectByType<VirtualKeyboardManager>(FindObjectsInactive.Include);

        if (keyboardManager != null && keyboardManager.IsOpen)
        {
            keyboardManager.CloseKeyboard();
        }
    }

}
