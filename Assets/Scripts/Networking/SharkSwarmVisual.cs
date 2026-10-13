using UnityEngine;

namespace PirateSlop.Networking
{
    [DefaultExecutionOrder(120)]
    public sealed class SharkSwarmVisual : MonoBehaviour
    {
        NetworkLootChest chest;
        readonly Transform[] sharks = new Transform[NetworkLootChest.SharkCount];
        readonly Animator[] animators = new Animator[NetworkLootChest.SharkCount];
        readonly SharkMotion[] poses = new SharkMotion[NetworkLootChest.SharkCount];
        readonly int[] bites = new int[NetworkLootChest.SharkCount];
        readonly AudioSource[] swimAudio = new AudioSource[NetworkLootChest.SharkCount];
        readonly float[] nextSwim = new float[NetworkLootChest.SharkCount], nextSplash = new float[NetworkLootChest.SharkCount];
        AudioSource chaseAudio;

        public void Initialize(NetworkLootChest owner)
        {
            chest = owner;
            var prefab = Resources.Load<GameObject>("SharkVisual");
            if (prefab == null) return;
            for (int i = 0; i < sharks.Length; i++)
            {
                var pose = owner.SharkPose(i); poses[i] = pose; bites[i] = pose.Bite;
                var model = Instantiate(prefab, pose.Position, pose.Rotation, transform);
                model.name = "Shark_" + (i + 1); sharks[i] = model.transform; animators[i] = model.GetComponent<Animator>();
                var hitbox = model.AddComponent<CapsuleCollider>();
                hitbox.isTrigger = true; hitbox.direction = 2; hitbox.center = new Vector3(0, .2f, 1.25f); hitbox.height = 5.1f; hitbox.radius = .6f;
                var target = model.AddComponent<SharkHitTarget>(); target.Chest = owner; target.Index = i;
                var surface = model.AddComponent<BulletSurface>(); surface.Kind = BulletSurfaceKind.Flesh; surface.Marks = false;
            }
        }

        public void Bite(int index, int sequence)
        {
            if (index < 0 || index >= animators.Length || sequence <= bites[index]) return;
            bites[index] = sequence;
            if (animators[index] != null) animators[index].SetTrigger("Bite");
        }

        void LateUpdate()
        {
            if (chest == null) return;
            UpdateChaseAudio();
            float blend = 1f - Mathf.Exp(-14f * Time.deltaTime);
            for (int i = 0; i < sharks.Length; i++)
            {
                if (sharks[i] == null) continue;
                var target = chest.SharkPose(i);
                UpdateSharkAudio(i, target);
                if (target.Bite > bites[i]) Bite(i, target.Bite);
                var pose = target;
                if (!chest.IsServerInitialized)
                {
                    pose.Position = Vector3.Lerp(poses[i].Position, target.Position, blend);
                    pose.Rotation = Quaternion.Slerp(poses[i].Rotation, target.Rotation, blend);
                }
                poses[i] = pose;
                if (animators[i] != null)
                {
                    animators[i].SetBool("Biting", false);
                    animators[i].speed = target.State == SharkMotionState.Attacking ? 1.9f : target.State == SharkMotionState.Diving || target.State == SharkMotionState.Returning ? 1.5f : target.State == SharkMotionState.Waiting ? .8f : 1.1f;
                }
            }
            if (!chest.IsServerInitialized)
                for (int pass = 0; pass < 8; pass++) for (int i = 0; i < sharks.Length; i++) for (int j = i + 1; j < sharks.Length; j++) NetworkLootChest.SeparateSharks(ref poses[i], ref poses[j]);
            for (int i = 0; i < sharks.Length; i++)
                if (sharks[i] != null)
                {
                    sharks[i].gameObject.SetActive(poses[i].State != SharkMotionState.Dispersing || poses[i].Position.y > chest.EventPoint.y - 26f);
                    sharks[i].SetPositionAndRotation(poses[i].Position, poses[i].Rotation);
                }
        }

        void UpdateChaseAudio()
        {
            if (!chest.IsClientInitialized) return;
            var pursued = chest.SharkPursuedPlayer;
            var ear = SpatialAudioTone.FindListener();
            bool active = pursued != null && ear != null && !SessionController.MenuOpen &&
                Vector3.Distance(ear.transform.position, pursued.transform.position) < 70f;
            bool wasPlaying = chaseAudio != null && chaseAudio.isPlaying;
            GameAudio.Loop(ref chaseAudio, SoundCue.SharkChase, transform, active ? 1f : 0f);
            if (chaseAudio == null) return;
            if (pursued != null) chaseAudio.transform.position = pursued.transform.position + Vector3.up * 1.5f;
            chaseAudio.minDistance = 12f;
            chaseAudio.priority = 40;
            if (active && !wasPlaying && chaseAudio.clip != null)
                chaseAudio.time = Mathf.Repeat(OceanSurface.Instance != null ? OceanSurface.Instance.WaveTime : Time.time, chaseAudio.clip.length);
        }

        void UpdateSharkAudio(int index, SharkMotion pose)
        {
            if (!chest.IsClientInitialized || sharks[index] == null) return;
            var ocean = OceanSurface.Instance;
            float surface = ocean != null ? ocean.Height(pose.Position) : chest.EventPoint.y;
            float depth = surface - pose.Position.y;
            var ear = SpatialAudioTone.FindListener();
            bool audible = ear != null && !SessionController.MenuOpen && Vector3.Distance(ear.transform.position, pose.Position) < 36f;
            bool swimming = audible && depth < 2.6f && (pose.State == SharkMotionState.Patrol || pose.State == SharkMotionState.Attacking || pose.State == SharkMotionState.Returning);
            GameAudio.Repeat(ref swimAudio[index], ref nextSwim[index], SoundCue.SharkSwim, sharks[index], swimming, 5.5f + index * .7f);
            if (swimAudio[index] != null) swimAudio[index].transform.localPosition = Vector3.zero;
            var previous = poses[index];
            float previousDepth = (ocean != null ? ocean.Height(previous.Position) : chest.EventPoint.y) - previous.Position.y;
            bool diving = pose.State == SharkMotionState.Diving && previous.State != SharkMotionState.Diving && previousDepth < 3f;
            bool surfacing = depth < 1.7f && previousDepth >= 1.7f && pose.State == SharkMotionState.Attacking;
            if (audible && (diving || surfacing) && Time.time >= nextSplash[index])
            {
                var point = diving ? previous.Position : pose.Position;
                point.y = ocean != null ? ocean.Height(point) : chest.EventPoint.y;
                GameAudio.Play(SoundCue.SharkSplash, point, diving ? 1f : .6f);
                CombatVfx.Splash(point);
                nextSplash[index] = Time.time + 3f;
            }
        }

        void OnDisable()
        {
            StaffAudio.Stop(ref chaseAudio);
            for (int i = 0; i < swimAudio.Length; i++) StaffAudio.Stop(ref swimAudio[i]);
        }
    }
}
