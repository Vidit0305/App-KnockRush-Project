using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace KnockRush.Backend
{
    /// <summary>Error returned by Firebase (status like "UNAUTHENTICATED", "ALREADY_EXISTS").</summary>
    public sealed class BackendException : Exception
    {
        public BackendException(string status, string message) : base($"{status}: {message}")
        {
            Status = status;
        }

        public string Status { get; }
    }

    /// <summary>
    /// Minimal Firebase client over REST: Auth (anonymous sign-in, token refresh, identity-provider
    /// linking) and Callable Cloud Functions. Works on every platform with UnityWebRequest, needs no
    /// native SDK, and talks to the Emulator Suite in development.
    /// </summary>
    public sealed class FirebaseRestClient
    {
        private const string RefreshTokenKey = "knockrush.auth.refresh";
        private const string UidKey = "knockrush.auth.uid";

        private readonly BackendConfig config;
        private string idToken;
        private DateTime idTokenExpiresUtc;
        private string refreshToken;

        public FirebaseRestClient(BackendConfig config)
        {
            this.config = config;
            refreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
            Uid = PlayerPrefs.GetString(UidKey, string.Empty);
        }

        public string Uid { get; private set; }

        public bool IsSignedIn => !string.IsNullOrEmpty(Uid) && !string.IsNullOrEmpty(refreshToken);

        /// <summary>Restores the saved session or creates a new anonymous (guest) account.</summary>
        public async Task<string> SignInAsync()
        {
            if (IsSignedIn)
            {
                try
                {
                    await GetIdTokenAsync(forceRefresh: true);
                    return Uid;
                }
                catch (BackendException)
                {
                    SignOut(); // stale session (account deleted / emulator reset): start fresh
                }
            }

            var json = await PostJsonAsync($"{config.AuthBaseUrl}/accounts:signUp?key={config.WebApiKey}",
                new JObject { ["returnSecureToken"] = true }, null);
            StoreSession(json["localId"]?.ToString(), json["idToken"]?.ToString(), json["refreshToken"]?.ToString(), json["expiresIn"]?.ToString());
            return Uid;
        }

        /// <summary>
        /// Links the guest account to Google / Apple using the ID token returned by the platform
        /// sign-in plugin (providerId "google.com" or "apple.com"), keeping all progress.
        /// </summary>
        public async Task LinkWithProviderAsync(string providerId, string providerIdToken)
        {
            var token = await GetIdTokenAsync();
            var json = await PostJsonAsync($"{config.AuthBaseUrl}/accounts:signInWithIdp?key={config.WebApiKey}", new JObject
            {
                ["idToken"] = token,
                ["postBody"] = $"id_token={providerIdToken}&providerId={providerId}",
                ["requestUri"] = "http://localhost",
                ["returnSecureToken"] = true,
                ["returnIdpCredential"] = true
            }, null);
            StoreSession(json["localId"]?.ToString(), json["idToken"]?.ToString(), json["refreshToken"]?.ToString(), json["expiresIn"]?.ToString());
        }

        public void SignOut()
        {
            Uid = string.Empty;
            idToken = null;
            refreshToken = string.Empty;
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(UidKey);
            PlayerPrefs.Save();
        }

        public async Task<string> GetIdTokenAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(idToken) && DateTime.UtcNow < idTokenExpiresUtc)
            {
                return idToken;
            }

            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new BackendException("UNAUTHENTICATED", "Not signed in.");
            }

            var form = $"grant_type=refresh_token&refresh_token={UnityWebRequest.EscapeURL(refreshToken)}";
            var json = await SendAsync($"{config.TokenBaseUrl}/token?key={config.WebApiKey}", Encoding.UTF8.GetBytes(form), "application/x-www-form-urlencoded", null);
            StoreSession(json["user_id"]?.ToString(), json["id_token"]?.ToString(), json["refresh_token"]?.ToString(), json["expires_in"]?.ToString());
            return idToken;
        }

        /// <summary>Calls a Callable Cloud Function and returns its "result".</summary>
        public async Task<JToken> CallAsync(string function, object data)
        {
            var token = await GetIdTokenAsync();
            var json = await PostJsonAsync(config.FunctionUrl(function), new JObject { ["data"] = data != null ? JToken.FromObject(data) : new JObject() }, token);
            if (json["error"] is JObject error)
            {
                throw new BackendException(error["status"]?.ToString() ?? "INTERNAL", error["message"]?.ToString() ?? "Unknown error");
            }

            return json["result"];
        }

        public async Task<T> CallAsync<T>(string function, object data)
        {
            var result = await CallAsync(function, data);
            return result == null ? default : result.ToObject<T>();
        }

        private void StoreSession(string uid, string token, string refresh, string expiresIn)
        {
            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token))
            {
                throw new BackendException("INTERNAL", "Malformed auth response.");
            }

            Uid = uid;
            idToken = token;
            if (!string.IsNullOrEmpty(refresh))
            {
                refreshToken = refresh;
            }

            var seconds = int.TryParse(expiresIn, out var parsed) ? parsed : 3600;
            idTokenExpiresUtc = DateTime.UtcNow.AddSeconds(seconds - 120);
            PlayerPrefs.SetString(RefreshTokenKey, refreshToken);
            PlayerPrefs.SetString(UidKey, Uid);
            PlayerPrefs.Save();
        }

        private Task<JObject> PostJsonAsync(string url, JObject body, string bearer)
        {
            return SendAsync(url, Encoding.UTF8.GetBytes(body.ToString(Formatting.None)), "application/json", bearer);
        }

        private async Task<JObject> SendAsync(string url, byte[] payload, string contentType, string bearer)
        {
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(payload) { contentType = contentType };
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", contentType);
                request.timeout = Mathf.CeilToInt(config.RequestTimeoutSeconds);
                if (!string.IsNullOrEmpty(bearer))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + bearer);
                }

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Delay(10);
                }

                var text = request.downloadHandler.text;
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    throw new BackendException("UNAVAILABLE", request.error);
                }

                JObject json;
                try
                {
                    json = string.IsNullOrEmpty(text) ? new JObject() : JObject.Parse(text);
                }
                catch (JsonException)
                {
                    throw new BackendException("INTERNAL", $"HTTP {request.responseCode}: {text}");
                }

                // Auth REST errors: {"error":{"code":400,"message":"..."}}; callable errors carry "status".
                if (request.responseCode >= 400 && json["error"] is JObject error && error["status"] == null)
                {
                    throw new BackendException("UNAUTHENTICATED", error["message"]?.ToString() ?? $"HTTP {request.responseCode}");
                }

                return json;
            }
        }
    }
}
