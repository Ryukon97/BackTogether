using System.Collections;
using UnityEngine;
using TMPro;
using EpicTransport;

public class HostLobbyUI : MonoBehaviour
{
    public TextMeshProUGUI roomCodeText;

    void Start()
    {
        StartCoroutine(WaitForEOSProductID());
    }

    private IEnumerator WaitForEOSProductID()
    {
        while (string.IsNullOrEmpty(EOSSDKComponent.LocalUserProductIdString))
        {
            yield return null;
        }
        if (roomCodeText != null)
        {
            roomCodeText.text = "Room Code: " + EOSSDKComponent.LocalUserProductIdString;
            Debug.Log("방 코드 로드 완료: " + EOSSDKComponent.LocalUserProductIdString);
        }
    }

    public void OnCopyButtonClicked()
    {
        string currentCode = EOSSDKComponent.LocalUserProductIdString;

        if (!string.IsNullOrEmpty(currentCode))
        {
            GUIUtility.systemCopyBuffer = currentCode;
            Debug.Log("방 코드가 복사되었습니다: " + currentCode);
        }
        else
        {
            Debug.LogWarning("아직 방 코드가 발급되지 않았습니다. 잠시 후 다시 시도해주세요.");
        }
    }
}