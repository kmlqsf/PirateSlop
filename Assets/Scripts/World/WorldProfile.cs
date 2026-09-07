using UnityEngine;

namespace PirateSlop.World
{
    [CreateAssetMenu(menuName = "PirateSlop/World/Map Profile")]
    public sealed class WorldProfile : ScriptableObject
    {
        public int Seed = 17421;
        public bool RandomSeed = true;
        public TextAsset FixedLayout;
        [Min(600)] public float Radius = 1500;
        [Range(1, 80)] public int LocationCount = 24;
        [Range(32, 160)] public int Resolution = 96;
        [Min(35)] public float SeaDepth = 45;
        [Min(40)] public float ShippingGap = 90;
        [Min(50)] public float SpawnClearance = 65;
        [Min(100)] public float SpawnSpacing = 140;
        [Min(1)] public int CatalogRevision = 1;
        public LocationDefinition[] Locations;
        public Material TerrainMaterial;
    }
}
