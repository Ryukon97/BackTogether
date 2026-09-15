using UnityEngine;
using Mirror;
using System.Collections;

public class ChapterDoor : NetworkBehaviour
{
    [Header("씬 설정")]
    [Tooltip("이동할 다음 씬의 이름")]
    public string chapterSceneName = "Lobby";

    [Header("페이드 딜레이 설정")]
    [Tooltip("페이드 아웃이 진행되는 시간 (초)")]
    public float fadeDuration = 1.5f;

    [Header("효과 (선택 사항)")]
    [Tooltip("문 통과 시 생성될 파티클 이펙트")]
    public GameObject fanfarePrefab;

    private bool isWarping = false;

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!DemoManager.AllowsScene(chapterSceneName)) return;
        if (isWarping) return; // 중복 트리거 방지

        if (collision.CompareTag("Player"))
        {
            isWarping = true;

            // 모든 클라이언트에게 페이드 아웃 및 이펙트 재생 명령
            RpcStartFadeOut();

            // 서버에서 페이드 시간 동안 대기 후 씬 전환
            StartCoroutine(ServerWarpRoutine());
        }
    }

    [ClientRpc]
    private void RpcStartFadeOut()
    {
        if (fanfarePrefab != null)
        {
            Instantiate(fanfarePrefab, transform.position, Quaternion.identity);
        }

        // 싱글톤 ScreenFader를 통해 확실하게 페이드 아웃 실행
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeOut(fadeDuration);
        }
        else
        {
            Debug.LogWarning("ScreenFader 인스턴스를 씬에서 찾을 수 없습니다!");
        }
    }

    private IEnumerator ServerWarpRoutine()
    {
        yield return new WaitForSeconds(fadeDuration);

        if (isServer && NetworkManager.singleton != null)
        {
            NetworkManager.singleton.ServerChangeScene(chapterSceneName);
        }
    }
}