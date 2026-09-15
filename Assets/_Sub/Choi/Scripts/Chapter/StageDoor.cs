using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class StageDoor : NetworkBehaviour
{
    [Header("설정")]
    public int currentStageNumber = 1;
    public GameObject fanfarePrefab;
    public string lobbySceneName = "Lobby";
    public float floatSpeed = 2f;

    [Header("UI 요소")]
    public GameObject clearTextUI;
    public TMP_Text countText;

    private bool clearStarted;
    private HashSet<uint> arrivedPlayers = new HashSet<uint>();

    void Start()
    {
        if (countText != null) countText.gameObject.SetActive(false);
        if (clearTextUI != null) clearTextUI.SetActive(false);
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (clearStarted) return;
        NetworkIdentity identity = collision.GetComponent<NetworkIdentity>();
        if (identity != null && collision.CompareTag("Player"))
        {
            if (!arrivedPlayers.Contains(identity.netId))
            {
                arrivedPlayers.Add(identity.netId);
                int totalPlayers = CoopPlayerIdentity.players.Count;

                int displayTargetCount = (currentStageNumber == 4) ? 1 : NetworkServer.connections.Count;
                RpcUpdateCount(arrivedPlayers.Count, displayTargetCount);

                bool isClearConditionMet = false;

                if (currentStageNumber == 4)
                {
                    if (arrivedPlayers.Count >= 1)
                    {
                        isClearConditionMet = true;
                        Debug.Log("[서버] 4스테이지 특수 조건 발동: 1명 도착으로 즉시 클리어!");
                    }
                }
                else
                {
                    if (arrivedPlayers.Count >= totalPlayers)
                    {
                        isClearConditionMet = true;
                    }
                }

                if (isClearConditionMet)
                {
                    clearStarted = true;
                    bool demoEnding = DemoManager.IsDemoMode && currentStageNumber == 2;
                    // Reliable RPC marks intentional exit on every peer before shutdown.
                    // The persistent manager survives the door's scene being unloaded.
                    RpcTriggerClearEffect(demoEnding);
                    if (demoEnding)
                    {
                        if (!NetworkClient.active) DemoManager.BeginEnding();
                        return;
                    }
                    StartCoroutine(WaitAndLoadScene());
                }
            }
        }
    }

    [ClientRpc]
    private void RpcUpdateCount(int current, int total)
    {
        if (countText != null)
        {
            countText.gameObject.SetActive(true);
            countText.text = $"{current} / {total}";
        }
    }

    [ClientRpc]
    private void RpcTriggerClearEffect(bool demoEnding)
    {
        if (demoEnding) DemoManager.BeginEnding();
        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.ClearChapter(currentStageNumber);
        }

        if (PlatformManager.Instance != null)
        {
            PlatformManager.Instance.UnlockAchievement($"CLEAR_CH{currentStageNumber}");
        }

        if (countText != null) countText.gameObject.SetActive(false);

        if (fanfarePrefab != null)
            Instantiate(fanfarePrefab, transform.position, Quaternion.identity);

        if (clearTextUI != null)
        {
            clearTextUI.SetActive(true);
            StartCoroutine(FloatTextRoutine());
        }
    }

    private IEnumerator FloatTextRoutine()
    {
        float timer = 0f;
        Vector3 startPos = clearTextUI.transform.localPosition;

        while (timer < 1.5f)
        {
            timer += Time.deltaTime;
            clearTextUI.transform.Translate(Vector3.up * floatSpeed * Time.deltaTime);
            yield return null;
        }

        clearTextUI.SetActive(false);
        clearTextUI.transform.localPosition = startPos;
    }

    private IEnumerator WaitAndLoadScene()
    {
        if (DemoManager.IsDemoMode && currentStageNumber == 2) yield break;
        yield return new WaitForSeconds(2.0f);
        if (isServer && NetworkManager.singleton != null)
        {
            string targetLobbyScene = string.IsNullOrEmpty(NetworkManager.singleton.onlineScene)
                ? lobbySceneName
                : NetworkManager.singleton.onlineScene;

            NetworkManager.singleton.ServerChangeScene(targetLobbyScene);
        }
    }
}
