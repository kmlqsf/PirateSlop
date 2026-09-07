using UnityEngine;

namespace PirateSlop.World
{
    public enum Landform { Island, Mountain, Crescent, Atoll, Reef, SeaStack, SupplyIsland }

    [System.Serializable]
    public sealed class LocationSettings
    {
        public string Id = "island";
        public Landform Shape;
        public float Weight = 1;
        public Vector2 Radius = new Vector2(45, 85);
        public Vector2 Height = new Vector2(8, 18);
        [Range(0, 1)] public float Roughness = .3f;
        public Color Sand = new Color(.68f, .59f, .38f);
        public Color Ground = new Color(.23f, .34f, .13f);
        public Color Rock = new Color(.3f, .31f, .3f);
    }

    [System.Serializable]
    public sealed class LocationPointRule
    {
        public string Tag = "land_spawn";
        [Min(0)] public int Count = 3;
        public Vector2 Height = new Vector2(2, 20);
        [Range(0, 60)] public float MaxSlope = 25;
        [Min(1)] public float Spacing = 8;
        public GameObject StaticPrefab;
        public bool AtLocationOrigin;
        public Vector3 LocalOffset;
        [Min(1)] public int PrefabVersion = 1;
    }

    [CreateAssetMenu(menuName = "PirateSlop/World/Location Type")]
    public sealed class LocationDefinition : ScriptableObject
    {
        public LocationSettings Settings = new LocationSettings();
        public LocationPointRule[] Points = { new LocationPointRule() };
    }
}
