using UnityEngine;

namespace PirateSlop.World
{
    public sealed class RiggingEditorPreview : MonoBehaviour
    {
        void Awake() { gameObject.SetActive(false); }
    }
}
