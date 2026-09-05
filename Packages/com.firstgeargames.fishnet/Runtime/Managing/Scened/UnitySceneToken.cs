using System.Collections.Generic;
using UnityEngine.SceneManagement;
namespace FishNet.Managing.Scened
{
    // FishNet scene IDs are process-local integers, not Unity's opaque 64-bit handles.
    // Allocate collision-free tokens rather than truncate SceneHandle.GetRawData().
    internal static class UnitySceneToken
    {
        static readonly Dictionary<Scene, int> Tokens = new();
        static int next = 1;
        public static int Get(Scene scene)
        {
            if (!scene.IsValid()) return 0;
            if (!Tokens.TryGetValue(scene, out int token)) Tokens.Add(scene, token = next++);
            return token;
        }
    }
}
