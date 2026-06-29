using UnityEngine;
using TMPro; 
using Mirror;

public class ClientJoinUI : MonoBehaviour
{
    public TMP_InputField roomCodeInput;

    public void OnJoinByCodeButtonClicked()
    {
        string roomCode = roomCodeInput.text.Trim();

        if (!string.IsNullOrEmpty(roomCode))
        {
            NetworkManager.singleton.networkAddress = roomCode;
            NetworkManager.singleton.StartClient();
        }
        else
        {
            Debug.LogWarning("방 코드를 입력해주세요!");
        }
    }
}