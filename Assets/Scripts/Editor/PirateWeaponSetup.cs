using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class PirateWeaponSetup
    {
        const string ModelPath = "Assets/Models/PiratePistol/SM_PiratePistol.fbx";
        [MenuItem("PirateSlop/Equip Pirate Pistol")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var player = GameObject.Find("PlayerCharacter");
            if(player == null) throw new InvalidOperationException("Open SampleScene first.");
            Directory.CreateDirectory("Assets/Materials/PiratePistol"); AssetDatabase.Refresh();
            Apply(player,false);
            const string path = "Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try { Apply(prefab,true); PrefabUtility.SaveAsPrefabAsset(prefab,path); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(player.scene); EditorSceneManager.SaveScene(player.scene); AssetDatabase.SaveAssets();
        }
        static Transform AddModel(Transform pivot)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),pivot);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0,180,0) * model.transform.localRotation;
            var grip = model.GetComponentsInChildren<Transform>().Single(t=>t.name=="GripSocket");
            model.transform.position += pivot.position - grip.position;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source=>{
                    string path = "Assets/Materials/PiratePistol/"+source.name+".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=source.name;mat.SetColor("_BaseColor",source.color);mat.SetFloat("_Metallic",source.name.Contains("Steel")||source.name.Contains("Brass")||source.name.Contains("Edge")?.7f:0);mat.SetFloat("_Smoothness",.35f);AssetDatabase.CreateAsset(mat,path); }
                    return mat;
                }).ToArray();
            return model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Muzzle");
        }
        static void Apply(GameObject player,bool networked)
        {
            var motor = player.GetComponent<AdvancedPlayerController>();
            var hand = player.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Hand.R");
            var camera = player.GetComponentInChildren<Camera>(true);
            foreach(var t in player.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="PistolWorldPivot"||t.name=="PistolViewPivot").ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
            var world = new GameObject("PistolWorldPivot").transform;
            world.SetParent(hand,true);world.position=hand.position;world.rotation=player.transform.rotation;
            var view = new GameObject("PistolViewPivot").transform;
            view.SetParent(camera.transform,false);view.localPosition=new Vector3(.22f,-.17f,.36f);
            var weapon = player.GetComponent<PirateWeapon>();if(weapon==null)weapon=player.AddComponent<PirateWeapon>();
            weapon.WorldPivot=world;weapon.ViewPivot=view;weapon.WorldMuzzle=AddModel(world);weapon.ViewMuzzle=AddModel(view);
            foreach(var renderer in view.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            const string tracerPath="Assets/Materials/PiratePistol/Tracer.mat";
            var tracer=AssetDatabase.LoadAssetAtPath<Material>(tracerPath);
            if(tracer==null){tracer=new Material(Shader.Find("Universal Render Pipeline/Unlit"));tracer.SetColor("_BaseColor",new Color(1,.68f,.2f));AssetDatabase.CreateAsset(tracer,tracerPath);}
            weapon.EffectMaterial=tracer;
            if(networked && player.GetComponent<NetworkWeapon>()==null)player.AddComponent<NetworkWeapon>();
            EditorUtility.SetDirty(weapon);
        }
    }
}
