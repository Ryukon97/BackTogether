using UnityEngine;
using EpicTransport;
using Mirror;

public class PrivateLobbyManager : MonoBehaviour
{
    [SerializeField] private EOSLobby eosLobby;

    public void OnStartPrivateHostClicked()
    {
        if (eosLobby == null)
        {
            Debug.LogError("EOSLobby 컴포넌트가 연결되지 않았습니다!");
            return;
        }

        uint maxPlayers = 4;
        Epic.OnlineServices.Lobby.LobbyPermissionLevel permissionLevel = Epic.OnlineServices.Lobby.LobbyPermissionLevel.Inviteonly;
        bool presenceEnabled = true;

        eosLobby.CreateLobby(maxPlayers, permissionLevel, presenceEnabled);
        NetworkManager.singleton.StartHost();
    }
}