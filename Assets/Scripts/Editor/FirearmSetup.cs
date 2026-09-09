using System.Linq;
using UnityEngine;
using UnityEditor;
using PirateSlop.Networking;

namespace PirateSlop.EditorTools
{
    public static class FirearmSetup
    {
        [MenuItem("PirateSlop/Configure Firearm Foundation")]
        public static void Configure()
        {
            if(!AssetDatabase.IsValidFolder("Assets/Settings/Weapons")) AssetDatabase.CreateFolder("Assets/Settings","Weapons");
            var pistol=Definition("Pistol");
            var musket=Definition("Musket");
            var shotgun=Definition("DoubleBarrel");
            string path="Assets/Prefabs/Networking/NetworkPlayer.prefab";
            var player=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var handling=player.GetComponent<FirearmHandling>() ?? player.AddComponent<FirearmHandling>();
                handling.Pistol=pistol;handling.Musket=musket;handling.Shotgun=shotgun;
                PrefabUtility.SaveAsPrefabAsset(player,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            const string markPath="Assets/Resources/BulletMark.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(markPath)==null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("PirateSlop/BulletMark")),markPath);
            var bank=Resources.Load<GameAudioBank>("GameAudioBank");
            var entries=bank.Entries.ToList();
            AddImpact(entries,SoundCue.BulletWood,.65f,"chop");
            AddImpact(entries,SoundCue.BulletMetal,.55f,"metalPot1","metalPot2","metalPot3");
            AddImpact(entries,SoundCue.BulletStone,.4f,"footstep06","footstep07");
            AddImpact(entries,SoundCue.BulletFlesh,.4f,"chop");
            bank.Entries=entries.ToArray();EditorUtility.SetDirty(bank);
            var config=AssetDatabase.LoadAssetAtPath<SessionConfig>("Assets/Settings/Networking/SessionConfig.asset");
            config.ProtocolVersion=Mathf.Max(config.ProtocolVersion,48);EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
        static void AddImpact(System.Collections.Generic.List<GameAudioBank.Entry> entries,SoundCue cue,float volume,params string[] names)
        {
            if(entries.Any(e=>e.Cue==cue)) return;
            entries.Add(new GameAudioBank.Entry { Cue=cue,Volume=volume,Distance=35,Clips=names.Select(n=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Foley/"+n+".ogg")).Where(c=>c!=null).ToArray() });
        }
        static FirearmDefinition Definition(string name)
        {
            string path="Assets/Settings/Weapons/"+name+".asset";
            var definition=AssetDatabase.LoadAssetAtPath<FirearmDefinition>(path);
            if(definition!=null) return definition;
            definition=ScriptableObject.CreateInstance<FirearmDefinition>();
            if(name=="Pistol")
            {
                definition.DamageCap=70;definition.CameraKick=1.7f;definition.AimFov=56;
                definition.HipPosition=new Vector3(.22f,-.17f,.36f);definition.MuzzleOffset=new Vector3(0,.11f,.41f);
                definition.AimPosition=new Vector3(0,-.10f,.35f);
                definition.AudibleDistance=120;
            }
            if(name=="Musket")
            {
                definition.Sound=SoundCue.Musket;definition.Scope=true;
                definition.Ballistics=new FirearmSettings { Range=240,NearDamage=70,FarDamage=45,NearHeadDamage=100,FarHeadDamage=70,FalloffStart=90,FalloffEnd=240,ShotInterval=.65f,ReloadDuration=3.2f };
                definition.HipSpread=2;definition.AimSpread=0;definition.MovingSpread=.2f;
                definition.CameraKick=3;definition.CameraYaw=.35f;definition.KickDegrees=17;definition.KickDistance=.10f;definition.KickRecovery=.38f;
                definition.AimSeconds=.24f;definition.FlashPower=1.2f;definition.TracerSpeed=600;
                definition.AimPosition=new Vector3(0,-.075f,.3f);definition.MuzzleOffset=new Vector3(0,.075f,1.15f);
            }
            if(name=="DoubleBarrel")
            {
                definition.Sound=SoundCue.DoubleBarrel;definition.Capacity=2;definition.Pellets=14;definition.DamageCap=70;
                definition.Ballistics=new FirearmSettings { Range=65,NearDamage=5,FarDamage=1.5f,NearHeadDamage=5,FarHeadDamage=1.5f,FalloffStart=12,FalloffEnd=55,ShotInterval=.55f,ReloadDuration=3.6f };
                definition.HipSpread=5;definition.AimSpread=2.5f;definition.MovingSpread=.7f;
                definition.CameraKick=5;definition.CameraYaw=.65f;definition.KickDegrees=24;definition.KickDistance=.16f;definition.KickRecovery=.48f;
                definition.FlashPower=1.4f;definition.ShooterKnockback=1.5f;definition.AimFov=58;definition.TracerWidth=.016f;
                definition.AimPosition=new Vector3(0,-.079f,.3f);definition.MuzzleOffset=new Vector3(0,.079f,.74f);
            }
            AssetDatabase.CreateAsset(definition,path);
            return definition;
        }
    }
}
