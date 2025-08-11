using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;

public class LiveSnapshotPanel : MonoBehaviour {
    [SerializeField] string imageUrl = "https://<your-service>.onrender.com/latest.png";
    [SerializeField] RawImage target;
    [SerializeField, Range(30f, 3600f)] float refreshSeconds = 900f; // 15 min
    [SerializeField] bool bustCache = true;

    Texture2D currentTex;

    void OnEnable() { StartCoroutine(Loop()); }
    void OnDisable() { if (currentTex) Destroy(currentTex); }

    IEnumerator Loop() {
        while (true) {
            var url = bustCache ? $"{imageUrl}?t={Time.realtimeSinceStartup}" : imageUrl;
            using (var req = UnityWebRequestTexture.GetTexture(url, nonReadable: false)) {
                req.timeout = 20;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) {
                    var tex = DownloadHandlerTexture.GetContent(req);
                    if (currentTex) Destroy(currentTex);
                    currentTex = tex;
                    target.texture = currentTex;
                } else {
                    Debug.LogWarning($"Snapshot fetch failed: {req.error}");
                }
            }
            yield return new WaitForSeconds(refreshSeconds);
        }
    }
}

