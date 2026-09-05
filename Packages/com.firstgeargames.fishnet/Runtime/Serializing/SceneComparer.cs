using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Serializing.Helping
{
    internal sealed class SceneHandleEqualityComparer : EqualityComparer<Scene>
    {
        public override bool Equals(Scene a, Scene b)
        {
            return FishNet.Managing.Scened.UnitySceneToken.Get(a) == FishNet.Managing.Scened.UnitySceneToken.Get(b);
        }

        public override int GetHashCode(Scene obj)
        {
            return FishNet.Managing.Scened.UnitySceneToken.Get(obj);
        }
    }
}
