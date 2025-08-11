using UnityEngine;
using System.Collections;

public class ForecastWebView : MonoBehaviour {
    private WebViewObject webView;

    // margins from left, top, right, bottom (pixels)
    public int left = 80, top = 160, right = 80, bottom = 240;

    void Start() {
        StartCoroutine(InitWebView());
    }

    IEnumerator InitWebView() {
        webView = (new GameObject("WebView")).AddComponent<WebViewObject>();

        webView.Init(
            cb: (msg) => { Debug.Log($"CallFromJS: {msg}"); },
            err: (msg) => { Debug.LogError($"WebView error: {msg}"); },
            started: (msg) => { Debug.Log($"WebView started: {msg}"); },
            hooked: (msg) => { Debug.Log($"WebView hooked: {msg}"); },
            enableWKWebView: true,
            transparent: false
        );

        yield return new WaitForEndOfFrame();

        webView.SetMargins(left, top, right, bottom);
        webView.SetVisibility(true);

        // NOAA forecast in plain text format (easier to read in headset)
        string url = "https://forecast.weather.gov/product.php?site=LOX&issuedby=LOX&product=SRF&format=txt&version=1";
        webView.LoadURL(url);
    }

    public void Show(bool show) {
        if (webView != null) {
            webView.SetVisibility(show);
        }
    }
}
