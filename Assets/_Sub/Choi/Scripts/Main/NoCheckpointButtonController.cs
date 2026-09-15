using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DG.Tweening; // DOTween 네임스페이스 추가

public class NoCheckpointButtonController : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Button noCheckpointButton;
    [SerializeField] private GameObject noCheckpointCheckImage; // 체크 표시용 이미지 오브젝트
    [SerializeField] private Image bgImage;                     // 알파값을 조절할 BG 이미지 컴포넌트
    [SerializeField] private TextMeshProUGUI tmpText;           // 알파값을 조절 및 붉게 변할 TMP 텍스트 컴포넌트
    [SerializeField] private GameObject warningTextObject;      // 챕터 미달 시 띄울 경고 TMP 오브젝트

    [Header("알파값 설정")]
    [Range(0f, 1f)][SerializeField] private float lockedAlpha = 0.4f;   // 챕터 미달 시 흐린 정도 (0~1)
    [Range(0f, 1f)][SerializeField] private float normalAlpha = 1.0f;   // 해금 시 선명한 정도 (0~1)

    [Header("씬 설정")]
    [SerializeField] private string defaultLobbySceneName = "Lobby";
    [SerializeField] private string noCheckpointLobbySceneName = "NLobby";

    private bool isChecked = false;
    private bool canUseNoCheckpoint = false;
    private Coroutine warningCoroutine;

    public bool IsNoCheckpointSelected => isChecked;

    private void Start()
    {
        ResetSelectionToDefault();
        if (warningTextObject != null) warningTextObject.SetActive(false);

        // 게임 시작 시점에 세이브 데이터가 이미 존재한다면 바로 클리어 여부 반영
        if (GameSaveManager.Instance != null && GameSaveManager.Instance.currentData != null)
        {
            int maxCleared = GameSaveManager.Instance.currentData.maxClearedChapter;
            canUseNoCheckpoint = (maxCleared >= 6) || PrivateLobbyManager.IsExStageUnlockedForCurrentPlayer();
        }

        // [DEMO VERSION] 데모 버전에서는 노체크포인트 모드를 무조건 비활성화합니다.
        canUseNoCheckpoint = false;

        UpdateVisuals();
    }

    private void Update()
    {
        // 1. 챕터 6 클리어 여부 실시간 확인
        if (GameSaveManager.Instance != null && GameSaveManager.Instance.currentData != null)
        {
            int maxCleared = GameSaveManager.Instance.currentData.maxClearedChapter;
            canUseNoCheckpoint = (maxCleared >= 6) || PrivateLobbyManager.IsExStageUnlockedForCurrentPlayer();

            // [DEMO VERSION] 데모 버전에서는 무조건 비활성화합니다.
            canUseNoCheckpoint = false;

            if (!canUseNoCheckpoint && isChecked)
            {
                isChecked = false;
                UpdateVisuals();
                ApplySceneChange();
            }
        }

        // 2. 키보드 또는 패드로 해당 버튼이 선택(포커스)된 상태에서의 입력 감지
        if (noCheckpointButton != null && noCheckpointButton.gameObject.activeInHierarchy)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == noCheckpointButton.gameObject)
            {
                bool actionPressed = false;

                if (Keyboard.current != null)
                {
                    actionPressed |= Keyboard.current.enterKey.wasPressedThisFrame ||
                                     Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                                     Keyboard.current.spaceKey.wasPressedThisFrame;
                }

                if (Gamepad.current != null)
                {
                    actionPressed |= Gamepad.current.buttonSouth.wasPressedThisFrame;
                }

                if (actionPressed)
                {
                    TryToggleOrReject();
                }
            }
        }
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (tmpText != null) tmpText.DOKill();
    }

    public void TryToggleOrReject()
    {
        // 챕터 6을 안 깼을 때는 강한 흔들림 + 텍스트 붉은색 깜빡임 + 경고 오브젝트 표시
        if (!canUseNoCheckpoint)
        {
            PlayLockedRejectEffects();
            ShowWarningObject();
            return;
        }

        isChecked = !isChecked;
        UpdateVisuals();
        ApplySceneChange();
    }

    private void PlayLockedRejectEffects()
    {
        // 1. 버튼 흔들기
        transform.DOKill();
        transform.DOShakePosition(0.4f, new Vector3(30f, 0f, 0f), 20, 90, false, true);

        // 2. 버튼 안의 TMP 텍스트를 잠시 붉게 만들었다가 원래 lockedAlpha 색상으로 복구
        if (tmpText != null)
        {
            tmpText.DOKill();
            tmpText.color = Color.red;
            Color targetColor = new Color(1f, 1f, 1f, lockedAlpha);
            tmpText.DOColor(targetColor, 0.4f);
        }
    }

    private void ShowWarningObject()
    {
        if (warningTextObject == null) return;

        warningTextObject.SetActive(true);

        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
        }
        warningCoroutine = StartCoroutine(HideWarningRoutine(2f));
    }

    private IEnumerator HideWarningRoutine(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (warningTextObject != null)
        {
            warningTextObject.SetActive(false);
        }
    }

    private void UpdateVisuals()
    {
        // 체크 이미지 켜고 끄기
        if (noCheckpointCheckImage != null)
        {
            noCheckpointCheckImage.SetActive(isChecked);
        }

        // 해금 여부에 따라 BG 이미지와 TMP 텍스트의 알파값 조절
        float targetAlpha = canUseNoCheckpoint ? normalAlpha : lockedAlpha;

        if (bgImage != null)
        {
            Color color = bgImage.color;
            color.a = targetAlpha;
            bgImage.color = color;
        }

        if (tmpText != null)
        {
            Color color = tmpText.color;
            color.a = targetAlpha;
            tmpText.color = color;
        }
    }

    private void ApplySceneChange()
    {
        if (NetworkManager.singleton != null)
        {
            NetworkManager.singleton.onlineScene = isChecked ? noCheckpointLobbySceneName : defaultLobbySceneName;
        }
    }

    public void ApplySelectionToNetworkManager()
    {
        ApplySceneChange();
    }

    public void ResetSelectionToDefault()
    {
        isChecked = false;
        ApplySceneChange();
        UpdateVisuals();
    }
}
