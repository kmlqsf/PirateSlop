using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PirateSlop.EditorTools
{
    public static class MenuPresentationSetup
    {
        [MenuItem("PirateSlop/Refresh Main Menu Presentation")]
        public static void Configure()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/NetworkMenu.unity" || scene.isDirty)
                throw new System.InvalidOperationException("Open the saved NetworkMenu scene before refreshing its presentation.");
            var backdrop = Object.FindFirstObjectByType<MenuBackdrop>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkShip.prefab");
            var ocean = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ocean.mat");
            if (backdrop == null || source == null || ocean == null) throw new System.InvalidOperationException("Menu presentation assets are missing.");
            var replacement = new GameObject("MenuShip");
            replacement.transform.SetParent(backdrop.Content.transform, false);
            replacement.layer = 30;
            foreach (Transform child in source.transform)
            {
                if (child.name == "ShipDestructionSections" || child.name == "MainShipCollision") continue;
                CopyVisual(child, replacement.transform);
            }
            if (replacement.GetComponentsInChildren<MeshRenderer>().Length == 0)
            {
                Object.DestroyImmediate(replacement);
                throw new System.InvalidOperationException("The ship has no presentation geometry.");
            }
            Object.DestroyImmediate(backdrop.Ship.gameObject);
            backdrop.Ship = replacement.transform;
            replacement.transform.localRotation = Quaternion.Euler(0, -24f, 0);
            var bounds = new Bounds(replacement.transform.position, Vector3.zero);
            foreach (var renderer in replacement.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            backdrop.FocusPoint = backdrop.transform.InverseTransformPoint(bounds.center);
            backdrop.FramingRadius = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z));

            var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MenuSea.mat");
            water.shader = ocean.shader;
            water.CopyPropertiesFromMaterial(ocean);
            water.name = "MenuSea";
            water.SetFloat("_UseWaveTime", 0f);
            water.SetFloat("_SunStrength", 3.2f);
            water.SetFloat("_SunSharpness", 32f);
            water.SetFloat("_ReflectionStrength", 0.9f);
            water.SetFloat("_Smouthness", 0.98f);
            water.SetFloat("_NormalStrength", 0.38f);
            EditorUtility.SetDirty(water);
            backdrop.Water = water;
            var sea = backdrop.Content.transform.Find("MenuSea");
            sea.localPosition = Vector3.zero;
            sea.localRotation = Quaternion.identity;
            sea.localScale = Vector3.one;
            sea.GetComponent<MeshRenderer>().sharedMaterial = water;
            sea.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            sea.GetComponent<MeshFilter>().sharedMesh = SeaMesh();
            backdrop.View.fieldOfView = 40f;
            backdrop.View.farClipPlane = 6000f;
            backdrop.View.cullingMask = 1 << 30;
            backdrop.View.clearFlags = CameraClearFlags.Skybox;
            var cameraData = backdrop.View.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null) { cameraData.requiresDepthTexture = true; cameraData.requiresColorTexture = true; }
            backdrop.PositionView(0f);
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/MaritimeSky.mat");
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.88f,.58f,.38f);
            RenderSettings.fogDensity = .0009f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.78f,.48f,.42f);
            RenderSettings.ambientEquatorColor = new Color(.85f,.52f,.32f);
            RenderSettings.ambientGroundColor = new Color(.14f,.16f,.22f);
            RenderSettings.ambientIntensity = 1.25f;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "MenuLight") continue;
                var light = root.GetComponent<Light>();
                light.color = new Color(1f,.60f,.28f);
                light.intensity = 3.4f;
                light.transform.rotation = Quaternion.Euler(2.8f,137f,0);
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.95f;
                light.cullingMask = 1 << 30;
                RenderSettings.sun = light;
            }
            var existingBeam = backdrop.Content.transform.Find("ShipSunBeam");
            GameObject beamObj = existingBeam != null ? existingBeam.gameObject : new GameObject("ShipSunBeam");
            beamObj.name = "ShipSunBeam";
            beamObj.layer = 30;
            beamObj.transform.SetParent(backdrop.Content.transform, false);
            beamObj.transform.position = backdrop.Ship.position + new Vector3(-25f, 32f, 30f);
            beamObj.transform.rotation = Quaternion.Euler(26f, 138f, 0f);
            var spot = beamObj.GetComponent<Light>();
            if (spot == null) spot = beamObj.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 80f;
            spot.spotAngle = 65f;
            spot.innerSpotAngle = 35f;
            spot.color = new Color(1f, 0.68f, 0.32f);
            spot.intensity = 6.5f;
            spot.shadows = LightShadows.Soft;
            spot.cullingMask = 1 << 30;
            EditorUtility.SetDirty(backdrop);
            AssetDatabase.SaveAssetIfDirty(water);
            AssetDatabase.SaveAssetIfDirty(sea.GetComponent<MeshFilter>().sharedMesh);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void CopyVisual(Transform source, Transform parent)
        {
            if (!source.gameObject.activeSelf) return;
            var copy = new GameObject(source.name);
            copy.layer = 30;
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            var filter = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && filter.sharedMesh != null && renderer != null)
            {
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var visual = copy.AddComponent<MeshRenderer>();
                visual.sharedMaterials = renderer.sharedMaterials;
                visual.shadowCastingMode = ShadowCastingMode.On;
                visual.receiveShadows = true;
            }
            foreach (Transform child in source) CopyVisual(child, copy.transform);
        }

        static Mesh SeaMesh()
        {
            const int count = 192;
            var vertices = new Vector3[(count+1)*(count+1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[count*count*6];
            for (int z=0;z<=count;z++) for (int x=0;x<=count;x++)
            {
                float u=(x-count*.5f)/(count*.5f), v=(z-count*.5f)/(count*.5f);
                vertices[z*(count+1)+x]=new Vector3(u*(100f+3900f*Mathf.Pow(Mathf.Abs(u),3)),0,v*(100f+3900f*Mathf.Pow(Mathf.Abs(v),3)));
                uv[z*(count+1)+x]=new Vector2(x/(float)count,z/(float)count);
            }
            int index=0;
            for(int z=0;z<count;z++) for(int x=0;x<count;x++)
            {
                int a=z*(count+1)+x,b=a+count+1;
                triangles[index++]=a; triangles[index++]=b; triangles[index++]=a+1;
                triangles[index++]=a+1; triangles[index++]=b; triangles[index++]=b+1;
            }
            var mesh=new Mesh {name="MenuSeaGrid",vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateNormals();
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(8100,20,8100));
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/MenuSeaGrid.asset");
            if(saved==null) { AssetDatabase.CreateAsset(mesh,"Assets/Models/MenuSeaGrid.asset"); return mesh; }
            EditorUtility.CopySerialized(mesh,saved);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
            return saved;
        }
    }
}
