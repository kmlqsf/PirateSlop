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
    public sealed class UpperDestructionBuildProcessor : IPreprocessBuildWithReport, IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        public const string Define = "PIRATESLOP_UPPER_DESTRUCTION_V2";
        public int callbackOrder => -9000;

        public static void Prepare()
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone);
            if (Array.IndexOf(symbols.Split(';'), Define) < 0)
                throw new BuildFailedException("The shared gameplay define is missing from Standalone Player Settings: " + Define);
            var settings = AssetDatabase.LoadAssetAtPath<UpperDestructionSettings>(UpperDestructionSettings.AssetPath);
            if (settings != null && settings.Ship != null && settings.Ship.GetComponent<UpperDestructionV2>() is { } prepared && prepared.AssetRevision < 3)
            {
                UpperDestructionAssetSetup.Prepare();
                settings = AssetDatabase.LoadAssetAtPath<UpperDestructionSettings>(UpperDestructionSettings.AssetPath);
            }
            var session = AssetDatabase.LoadAssetAtPath<SessionConfig>(UpperDestructionSettings.SessionPath);
            if (settings == null || settings.Ship == null || settings.Prefabs == null || session == null || session.ProtocolVersion <= 0)
                throw new BuildFailedException("UpperDestructionV2 assets are missing or invalid.");
            if (settings.Session != session)
            {
                settings.Session = session;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
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

        public void OnPreprocessBuild(BuildReport report) => Prepare();

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            var settings = AssetDatabase.LoadAssetAtPath<UpperDestructionSettings>(UpperDestructionSettings.AssetPath);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var controller in root.GetComponentsInChildren<SessionController>(true))
                    settings.Apply(controller);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            var settings = AssetDatabase.LoadAssetAtPath<UpperDestructionSettings>(UpperDestructionSettings.AssetPath);
            Debug.Log("SHARED_GAMEPLAY_BUILD protocol=" + settings.Session.ProtocolVersion + " ship=" + AssetDatabase.GetAssetPath(settings.Ship) + " development=" + ((report.summary.options & BuildOptions.Development) != 0));
        }
    }
}
