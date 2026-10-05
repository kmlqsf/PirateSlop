using System.Collections;
using UnityEngine.SceneManagement;
using PirateSlop.World;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        bool boatAttackTestRequested;
        bool oceanaTestRequested;

        IEnumerator LoadWaterScene(string sceneName)
        {
            foreach (var otherName in new[] { Config.GameScene, BoatAttackWaterTest.SceneName, OceanaWaterTest.SceneName })
            {
                if (otherName == sceneName) continue;
                var other = SceneManager.GetSceneByName(otherName);
                if (other.isLoaded) yield return SceneManager.UnloadSceneAsync(other);
            }
            if (!SceneManager.GetSceneByName(sceneName).isLoaded)
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        }
    }
}
