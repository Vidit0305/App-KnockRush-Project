using System.Threading.Tasks;

namespace KnockRush.Core.SceneManagement
{
    public interface ISceneLoader
    {
        bool IsLoading { get; }

        string CurrentSceneName { get; }

        /// <summary>
        /// Loads a Build Settings scene in Single mode. Once started, a load cannot be
        /// cancelled (Unity limitation) and the returned task completes when the new scene is active.
        /// Calls made while another load is in progress are rejected with a warning.
        /// </summary>
        Task LoadSceneAsync(string sceneName);
    }
}
