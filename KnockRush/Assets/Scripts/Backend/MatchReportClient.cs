using System;
using System.Text;
using System.Threading.Tasks;
using KnockRush.Core;
using KnockRush.Utilities;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace KnockRush.Backend
{
    /// <summary>
    /// Dedicated server → backend result reporting. The server key comes from the environment
    /// (KNOCKRUSH_MATCH_REPORT_KEY) or -matchReportKey; it never ships in client builds, so only
    /// dedicated servers can grant rewards. Retries transient failures; the backend is idempotent.
    /// </summary>
    public static class MatchReportClient
    {
        private const string Tag = "MatchReport";

        public static string ServerKey =>
            CommandLine.Get("-matchReportKey") ?? Environment.GetEnvironmentVariable("KNOCKRUSH_MATCH_REPORT_KEY");

        public static bool CanReport => !string.IsNullOrEmpty(ServerKey);

        public static async Task<bool> ReportAsync(MatchReport report, int attempts = 3)
        {
            if (!CanReport)
            {
                Log.Info(Tag, "No server key configured: results not reported (peer-hosted or practice match).");
                return false;
            }

            var config = BackendConfig.Load();
            var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(report));
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                using (var request = new UnityWebRequest(config.FunctionUrl("reportMatchResult"), UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(payload) { contentType = "application/json" };
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("x-knockrush-server-key", ServerKey);
                    request.timeout = 15;
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Delay(20);
                    }

                    if (request.responseCode == 200)
                    {
                        Log.Info(Tag, $"Match {report.MatchId} reported: {request.downloadHandler.text}");
                        return true;
                    }

                    Log.Warning(Tag, $"Report attempt {attempt} failed: {request.responseCode} {request.error} {request.downloadHandler.text}");
                    if (request.responseCode == 400 || request.responseCode == 403)
                    {
                        return false; // not retryable
                    }
                }

                await Task.Delay(1000 * attempt);
            }

            return false;
        }
    }
}
