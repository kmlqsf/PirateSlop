using System;
using PirateSlop.Experimental;
using PirateSlop.Networking;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PirateSlop.EditorTools
{
    public sealed class UpperDestructionBuildProcessor : IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        public const string Define = "PIRATESLOP_UPPER_DESTRUCTION_V2";
        public static bool IncludeExperimental { get; private set; }
        public int callbackOrder => -9000;

        public static void Prepare(bool experimental)
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone);
            if (Array.IndexOf(symbols.Split(';'), Define) >= 0)
                throw new BuildFailedException("Remove the global upper destruction define. This feature is enabled per build only.");
            IncludeExperimental = experimental;
            if (!experimental) return;
            var settings = AssetDatabase.LoadAssetAtPath<UpperDestructionSettings>(UpperDestructionSettings.AssetPath);
            if (settings != null && settings.Ship != null && settings.Ship.GetComponent<UpperDestructionV2>() is { } prepared && prepared.AssetRevision < 6)
            {
                UpperDestructionAssetSetup.Prepare();
                settings = AssetDatabase.LoadAssetAtPath<UpperDestructionSettings>(UpperDestructionSettings.AssetPath);
            }
            if (settings == null || settings.Ship == null || settings.Prefabs == null || settings.Session == null || settings.Session.ProtocolVersion != 199)
                throw new BuildFailedException("UpperDestructionV2 assets are missing or invalid.");
            var original = AssetDatabase.LoadAssetAtPath<FishNet.Managing.Object.SinglePrefabObjects>("Assets/Settings/Networking/NetworkPrefabs.asset");
            var upper = settings.Ship.GetComponent<UpperDestructionV2>();
            if (original == null || settings.Prefabs == original || upper == null || upper.FallenCannon == null || upper.FallenMortar == null)
                throw new BuildFailedException("Upper destruction network prefab registry is incomplete.");
            SynchronizeNetworkPrefabs(settings, original, upper);
            for (int i = 0; i < original.Prefabs.Count; i++)
                if (original.Prefabs[i] != settings.Prefabs.Prefabs[i]) throw new BuildFailedException("An existing network prefab index changed.");
            int tail = original.Prefabs.Count;
            if (settings.Prefabs.Prefabs[tail] != settings.Ship || settings.Prefabs.Prefabs[tail + 1] != upper.FallenCannon.GetComponent<FishNet.Object.NetworkObject>() || settings.Prefabs.Prefabs[tail + 2] != upper.FallenMortar.GetComponent<FishNet.Object.NetworkObject>())
                throw new BuildFailedException("Register the experimental prefab roots before building.");
            foreach (var assembly in upper.Assemblies)
                if ((assembly.Kind is UpperAssemblyKind.ForeMast or UpperAssemblyKind.MizzenMast) && (assembly.RepairPreview == null || assembly.MastVisuals.Length == 0))
                    throw new BuildFailedException("Prepare the persistent mast geometry and repair silhouettes before building.");
        }

        static void SynchronizeNetworkPrefabs(UpperDestructionSettings settings, FishNet.Managing.Object.SinglePrefabObjects original, UpperDestructionV2 upper)
        {
            int tail = original.Prefabs.Count;
            var expected = new FishNet.Object.NetworkObject[tail + 3];
            for (int i = 0; i < tail; i++) expected[i] = original.Prefabs[i];
            expected[tail] = settings.Ship;
            expected[tail + 1] = upper.FallenCannon.GetComponent<FishNet.Object.NetworkObject>();
            expected[tail + 2] = upper.FallenMortar.GetComponent<FishNet.Object.NetworkObject>();
            if (expected[tail + 1] == null || expected[tail + 2] == null)
                throw new BuildFailedException("Register the experimental prefab roots before building.");
            bool matches = settings.Prefabs.Prefabs.Count == expected.Length;
            for (int i = 0; matches && i < expected.Length; i++) matches = settings.Prefabs.Prefabs[i] == expected[i];
            if (matches) return;
            settings.Prefabs.Clear();
            settings.Prefabs.AddObjects(expected, false, false);
            EditorUtility.SetDirty(settings.Prefabs);
            AssetDatabase.SaveAssetIfDirty(settings.Prefabs);
        }

        public static void Finish() => IncludeExperimental = false;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            var settings = AssetDatabase.LoadAssetAtPath<UpperDestructionSettings>(UpperDestructionSettings.AssetPath);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var controller in root.GetComponentsInChildren<SessionController>(true))
                {
                    if (IncludeExperimental) settings.Apply(controller);
                    else if (controller.Config == null || controller.Config.ProtocolVersion != 198 || controller.ShipPrefab != null && controller.ShipPrefab.GetComponent<UpperDestructionV2>() != null)
                        throw new BuildFailedException("Release must use the legacy ship and protocol 198.");
                }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!IncludeExperimental)
                foreach (var packed in report.packedAssets)
                    foreach (var item in packed.contents)
                        if (!string.IsNullOrEmpty(item.sourceAssetPath) && item.sourceAssetPath.StartsWith(UpperDestructionSettings.Root, StringComparison.Ordinal))
                            throw new BuildFailedException("Experimental upper destruction assets were included in a legacy build: " + item.sourceAssetPath);
            Debug.Log("UPPER_DESTRUCTION_BUILD variant=" + (IncludeExperimental ? "v2 protocol=199" : "legacy protocol=198"));
        }
    }
}
