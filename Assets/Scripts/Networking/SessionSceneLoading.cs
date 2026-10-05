using System.Collections;
using UnityEngine.SceneManagement;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        IEnumerator LoadGameScene()
        {
            if (!SceneManager.GetSceneByName(Config.GameScene).isLoaded)
                yield return SceneManager.LoadSceneAsync(Config.GameScene, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(Config.GameScene));
        }
    }
}
