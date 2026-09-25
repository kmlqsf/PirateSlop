using UnityEngine;

namespace PirateSlop
{
    public sealed class WhaleYellowSign : MonoBehaviour
    {
        public string Title = "▼ ТОЧКА ЛУТА: РАНЕНЫЙ КИТ ▼";
        public string Subtitle = "ВЫТАЩИТЕ ГАРПУНЫ ЗА 30 СЕК";

        Transform signContainer;
        float baseHeight;

        void Awake()
        {
            baseHeight = transform.localPosition.y;
            BuildVisuals();
        }

        void BuildVisuals()
        {
            signContainer = new GameObject("SignContainer").transform;
            signContainer.SetParent(transform, false);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "YellowBoard";
            board.transform.SetParent(signContainer, false);
            board.transform.localScale = new Vector3(11f, 3.4f, 0.25f);

            var col = board.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var yellowMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            yellowMat.color = new Color(1f, 0.85f, 0.05f, 1f);
            if (yellowMat.HasProperty("_EmissionColor"))
            {
                yellowMat.EnableKeyword("_EMISSION");
                yellowMat.SetColor("_EmissionColor", new Color(0.9f, 0.7f, 0.02f) * 0.8f);
            }
            board.GetComponent<Renderer>().sharedMaterial = yellowMat;

            CreateText(signContainer, new Vector3(0f, 0.45f, -0.16f), Quaternion.identity, Title, 48, Color.black, 0.38f);
            CreateText(signContainer, new Vector3(0f, -0.55f, -0.16f), Quaternion.identity, Subtitle, 32, new Color(0.2f, 0.1f, 0f), 0.28f);

            CreateText(signContainer, new Vector3(0f, 0.45f, 0.16f), Quaternion.Euler(0f, 180f, 0f), Title, 48, Color.black, 0.38f);
            CreateText(signContainer, new Vector3(0f, -0.55f, 0.16f), Quaternion.Euler(0f, 180f, 0f), Subtitle, 32, new Color(0.2f, 0.1f, 0f), 0.28f);

            var lightGo = new GameObject("BeaconLight");
            lightGo.transform.SetParent(signContainer, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.1f);
            light.range = 35f;
            light.intensity = 3f;
        }

        void CreateText(Transform parent, Vector3 localPos, Quaternion localRot, string text, int fontSize, Color color, float charSize)
        {
            var go = new GameObject("SignText");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.fontStyle = FontStyle.Bold;
        }

        void Update()
        {
            if (signContainer == null) return;
            float bob = Mathf.Sin(Time.time * 1.5f) * 0.4f;
            transform.localPosition = new Vector3(transform.localPosition.x, baseHeight + bob, transform.localPosition.z);
            signContainer.Rotate(Vector3.up, 12f * Time.deltaTime, Space.World);
        }
    }
}
