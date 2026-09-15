using UnityEngine;
using Mirror;

public class LobbyGameStarter : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    public string gameSceneName = "chapter1";

    public void OnStartGameButtonClicked()
    {

        if (!DemoManager.AllowsScene(gameSceneName)) return;
        if (NetworkServer.active)
        {
            Debug.Log($"모든 플레이어를 데리고 {gameSceneName} 씬으로 이동합니다!");
            NetworkManager.singleton.ServerChangeScene(gameSceneName);
        }
        else
        {
            Debug.LogWarning("게임 시작은 방장(호스트)만 할 수 있습니다!");
        }
    }
}