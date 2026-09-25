using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.EditorTools
{
    public static class ShipLodSetup
    {
        const string ShipPath = "Assets/Prefabs/Networking/NetworkShip.prefab";
        const string FrigatePrefabPath = "Assets/Prefabs/Ships/PirateFrigate.prefab";

        [MenuItem("PirateSlop/Configure Ship LODs")]
        public static void Configure()
        {
            ConfigureNetworkShip();
            ConfigurePirateFrigate();
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureNetworkShip()
        {
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            try
            {
                var lodGroup = root.GetComponent<LODGroup>() ?? root.AddComponent<LODGroup>();
                var allRenderers = root.GetComponentsInChildren<Renderer>(true);
                var lod0List = new List<Renderer>(allRenderers);

                var lod1List = new List<Renderer>();
                var lod1ExcludedNames = new HashSet<string>
                {
                    "F2_WireBack", "F2_WireFront", "F2_WireMid", "F2_Ropes", "F2_Anchor", "F2_Wheel"
                };

                var msv = root.transform.Find("MainShipVisual");
                if (msv != null)
                {
                    foreach (var r in msv.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!lod1ExcludedNames.Contains(r.name))
                        {
                            lod1List.Add(r);
                        }
                    }
                }

                var sds = root.transform.Find("ShipDestructionSections");
                if (sds != null)
                {
                    foreach (var r in sds.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.name == "Intact" || (r.transform.parent != null && r.transform.parent.name == "Intact"))
                        {
                            lod1List.Add(r);
                        }
                    }
                }

                var flood = root.transform.Find("FloodWater");
                if (flood != null)
                {
                    var r = flood.GetComponent<Renderer>();
                    if (r != null) lod1List.Add(r);
                }

                var lod2List = new List<Renderer>();
                var lod2AllowedNames = new HashSet<string>
                {
                    "F2_Body", "F2_MastFront", "F2_MastMid", "F2_MastBack",
                    "F2_SailFront", "F2_SailMid1", "F2_SailMid2", "F2_SailBack",
                    "F2_Flag1", "F2_Flag2", "F2_MainFlag", "F2_Rudder", "F2_Prow"
                };

                if (msv != null)
                {
                    foreach (var r in msv.GetComponentsInChildren<Renderer>(true))
                    {
                        if (lod2AllowedNames.Contains(r.name))
                        {
                            lod2List.Add(r);
                        }
                    }
                }

                var lods = new LOD[3];
                lods[0] = new LOD(0.50f, lod0List.ToArray());
                lods[1] = new LOD(0.15f, lod1List.ToArray());
                lods[2] = new LOD(0.02f, lod2List.ToArray());

                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();

                PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void ConfigurePirateFrigate()
        {
            var lod0Asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Ships/Frigate/PirateFrigate.fbx");
            var lod1Asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Ships/Frigate/PirateFrigate_LOD1.fbx");
            var lod2Asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Ships/Frigate/PirateFrigate_LOD2.fbx");

            if (lod0Asset == null || lod1Asset == null || lod2Asset == null) return;

            var root = new GameObject("PirateFrigate");

            var lod0Go = Object.Instantiate(lod0Asset, root.transform);
            lod0Go.name = "LOD0";
            var lod1Go = Object.Instantiate(lod1Asset, root.transform);
            lod1Go.name = "LOD1";
            var lod2Go = Object.Instantiate(lod2Asset, root.transform);
            lod2Go.name = "LOD2";

            lod0Go.transform.localPosition = Vector3.zero;
            lod0Go.transform.localRotation = Quaternion.identity;
            lod0Go.transform.localScale = Vector3.one;

            lod1Go.transform.localPosition = Vector3.zero;
            lod1Go.transform.localRotation = Quaternion.identity;
            lod1Go.transform.localScale = Vector3.one;

            lod2Go.transform.localPosition = Vector3.zero;
            lod2Go.transform.localRotation = Quaternion.identity;
            lod2Go.transform.localScale = Vector3.one;

            var lodGroup = root.AddComponent<LODGroup>();
            var lods = new LOD[3];
            lods[0] = new LOD(0.50f, lod0Go.GetComponentsInChildren<Renderer>(true));
            lods[1] = new LOD(0.15f, lod1Go.GetComponentsInChildren<Renderer>(true));
            lods[2] = new LOD(0.02f, lod2Go.GetComponentsInChildren<Renderer>(true));

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            PrefabUtility.SaveAsPrefabAsset(root, FrigatePrefabPath);
            Object.DestroyImmediate(root);
        }
    }
}
