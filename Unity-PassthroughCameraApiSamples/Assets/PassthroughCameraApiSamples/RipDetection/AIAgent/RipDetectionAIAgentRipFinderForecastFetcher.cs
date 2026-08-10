using System.Collections;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public sealed class RipFinderForecastFetcher : MonoBehaviour
{
    [SerializeField]
    private RipFinderAssessmentCoordinator assessmentCoordinator;

    [SerializeField]
    private string forecastUrl =
        "https://forecast.weather.gov/product.php" +
        "?site=LOX&issuedby=LOX&product=SRF&format=txt&version=1";

    [SerializeField]
    private float refreshMinutes = 30f;

    private const int MaxForecastLength = 3000;

    private const string TargetSection =
        "San Luis Obispo County Beaches-";

    private void Start()
    {
        StartCoroutine(ForecastLoop());
    }

    private IEnumerator ForecastLoop()
    {
        while (true)
        {
            yield return FetchForecast();

            yield return new WaitForSeconds(
                refreshMinutes * 60f
            );
        }
    }

    private IEnumerator FetchForecast()
    {
        using UnityWebRequest request =
            UnityWebRequest.Get(forecastUrl);

        request.timeout = 15;

        request.SetRequestHeader(
            "User-Agent",
            "RipFinder/1.0"
        );

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning(
                "[RipFinder Forecast] Request failed: " +
                request.error
            );

            yield break;
        }

        string response =
            request.downloadHandler.text;

        if (string.IsNullOrWhiteSpace(response))
        {
            Debug.LogWarning(
                "[RipFinder Forecast] Response was empty."
            );

            yield break;
        }

        string fullForecast =
            ExtractForecastText(response);

        if (string.IsNullOrWhiteSpace(fullForecast))
        {
            Debug.LogWarning(
                "[RipFinder Forecast] Could not extract forecast text."
            );

            yield break;
        }

        string localForecast =
            ExtractSanLuisObispoSection(fullForecast);

        if (string.IsNullOrWhiteSpace(localForecast))
        {
            Debug.LogWarning(
                "[RipFinder Forecast] " +
                "San Luis Obispo forecast section was not found."
            );

            // Fallback so the app still functions.
            localForecast = fullForecast;
        }

        if (localForecast.Length > MaxForecastLength)
        {
            localForecast =
                localForecast.Substring(
                    0,
                    MaxForecastLength
                );
        }

        assessmentCoordinator.SetForecast(
            localForecast
        );

        Debug.Log(
            "[RipFinder Forecast] " +
            "San Luis Obispo forecast loaded successfully."
        );

        Debug.Log(
            "[RipFinder Forecast] Parsed length: " +
            localForecast.Length
        );

        Debug.Log(
            "[RipFinder Forecast] Preview:\n" +
            localForecast.Substring(
                0,
                Mathf.Min(
                    localForecast.Length,
                    1000
                )
            )
        );
    }

    private string ExtractForecastText(
        string response)
    {
        Match preMatch =
            Regex.Match(
                response,
                @"<pre[^>]*>(.*?)</pre>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline
            );

        string text;

        if (preMatch.Success)
        {
            text = preMatch.Groups[1].Value;
        }
        else
        {
            text = Regex.Replace(
                response,
                "<[^>]+>",
                " "
            );
        }

        text =
            WebUtility.HtmlDecode(text);

        text =
            text.Replace(
                "\r\n",
                "\n"
            );

        text =
            text.Replace(
                "\r",
                "\n"
            );

        text =
            Regex.Replace(
                text,
                @"\n{3,}",
                "\n\n"
            );

        return text.Trim();
    }

    private string ExtractSanLuisObispoSection(
        string fullForecast)
    {
        int start =
            fullForecast.IndexOf(
                TargetSection,
                System.StringComparison.OrdinalIgnoreCase
            );

        if (start < 0)
        {
            return null;
        }

        // NOAA separates forecast zones with "$$".
        int end =
            fullForecast.IndexOf(
                "$$",
                start,
                System.StringComparison.Ordinal
            );

        if (end < 0)
        {
            end = fullForecast.Length;
        }

        string section =
            fullForecast.Substring(
                start,
                end - start
            );

        return CleanSection(section);
    }

    private string CleanSection(string section)
{
    section =
        section.Replace(
            TargetSection,
            "San Luis Obispo County Beaches"
        );

    // Remove NOAA glossary / metadata after the actual forecast.
    int glossaryStart =
        section.IndexOf(
            "&&",
            System.StringComparison.Ordinal
        );

    if (glossaryStart >= 0)
    {
        section =
            section.Substring(
                0,
                glossaryStart
            );
    }

    section =
        Regex.Replace(
            section,
            @"^[A-Z]{3}\d{3}-\d{6}-\s*$",
            "",
            RegexOptions.Multiline
        );

    section =
        Regex.Replace(
            section,
            @"\n{3,}",
            "\n\n"
        );

    return section.Trim();
}
}