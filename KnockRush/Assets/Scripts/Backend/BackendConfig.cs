using UnityEngine;

namespace KnockRush.Backend
{
    /// <summary>
    /// Firebase connection settings (Resources/BackendConfig.asset). Development builds talk to the
    /// local Firebase Emulator Suite (project demo-knockrush); production builds talk to the real
    /// project. The Web API key is public by design (it identifies the project, it is not a secret).
    /// </summary>
    [CreateAssetMenu(menuName = "KnockRush/Config/Backend Config", fileName = "BackendConfig", order = 2)]
    public sealed class BackendConfig : ScriptableObject
    {
        public const string ResourcesPath = "BackendConfig";

        [Header("Firebase project")]
        [SerializeField] private string projectId = "demo-knockrush";
        [SerializeField] private string webApiKey = "fake-api-key";
        [SerializeField] private string functionsRegion = "europe-west1";

        [Header("Emulator (development)")]
        [SerializeField] private bool useEmulator = true;
        [Tooltip("127.0.0.1 for editor/desktop; 10.0.2.2 from the Android emulator; your PC's LAN IP from a phone.")]
        [SerializeField] private string emulatorHost = "127.0.0.1";
        [SerializeField] private int authEmulatorPort = 9099;
        [SerializeField] private int functionsEmulatorPort = 5001;

        [Header("Behaviour")]
        [Tooltip("Sign in (anonymous guest) automatically at startup.")]
        [SerializeField] private bool autoSignIn = true;
        [SerializeField, Min(2f)] private float requestTimeoutSeconds = 10f;

        public string ProjectId => projectId;
        public string WebApiKey => webApiKey;
        public string FunctionsRegion => functionsRegion;
        public bool UseEmulator => useEmulator;
        public bool AutoSignIn => autoSignIn;
        public float RequestTimeoutSeconds => requestTimeoutSeconds;

        public string AuthBaseUrl => useEmulator
            ? $"http://{emulatorHost}:{authEmulatorPort}/identitytoolkit.googleapis.com/v1"
            : "https://identitytoolkit.googleapis.com/v1";

        public string TokenBaseUrl => useEmulator
            ? $"http://{emulatorHost}:{authEmulatorPort}/securetoken.googleapis.com/v1"
            : "https://securetoken.googleapis.com/v1";

        public string FunctionUrl(string name) => useEmulator
            ? $"http://{emulatorHost}:{functionsEmulatorPort}/{projectId}/{functionsRegion}/{name}"
            : $"https://{functionsRegion}-{projectId}.cloudfunctions.net/{name}";

        public static BackendConfig Load()
        {
            var config = Resources.Load<BackendConfig>(ResourcesPath);
            return config != null ? config : CreateInstance<BackendConfig>();
        }

        public void ConfigureEmulator(string host, bool autoSignInAtStartup)
        {
            useEmulator = true;
            emulatorHost = host;
            autoSignIn = autoSignInAtStartup;
        }
    }
}
