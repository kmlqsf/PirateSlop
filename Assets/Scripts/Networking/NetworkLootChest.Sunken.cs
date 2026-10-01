using FishNet.Object.Synchronizing;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SyncVar<Vector3> sunkenFloor = new();
        readonly SyncVar<Vector3> sunkenRopeEnd = new();
        readonly SyncVar<float> sunkenDirection = new();
        float sunkenPreviousAngle;
        Vector3 sunkenPreviousPosition, shownRopeEnd;
        Material sunkenRopeMaterial;
        GameObject sunkenGrip;
        Vector3 SunkenFreeEnd
        {
            get
            {
                float direction = sunkenDirection.Value == 0f ? 1f : sunkenDirection.Value;
                float angle = -direction * (1f - progress.Value) * Mathf.Clamp(Catalog.SunkenUnwrapTurns, 1, 5) * Mathf.PI * 2f;
                return transform.TransformPoint(new Vector3(Mathf.Cos(angle) * 1.3f, .45f, Mathf.Sin(angle) * 1.3f));
            }
        }

        void InitializeSunkenRope()
        {
            var bottom = eventPoint.Value;
            bottom.y = ProceduralWorld.Instance.GroundHeight(bottom) + .08f;
            sunkenFloor.Value = bottom;
            sunkenRopeEnd.Value = SunkenFreeEnd;
        }

        bool ValidSunkenPosition(NetworkWeapon player)
        {
            var motor = player.GetComponent<AdvancedPlayerController>();
            var offset = player.transform.position + Vector3.up - transform.position;
            float radius = new Vector2(offset.x, offset.z).magnitude;
            return motor.IsSwimming && radius >= .9f && radius <= 3.3f && Mathf.Abs(offset.y - .4f) <= 1.4f && player.transform.position.y + 1.65f < eventPoint.Value.y;
        }

        float SunkenAngle(Vector3 position)
        {
            var offset = position - transform.position;
            return Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
        }

        void BeginSunkenUnwrap()
        {
            sunkenPreviousPosition = worker.transform.position;
            sunkenPreviousAngle = SunkenAngle(sunkenPreviousPosition);
            sunkenRopeEnd.Value = sunkenPreviousPosition + Vector3.up * 1.1f;
        }

        void TickSunkenUnwrap()
        {
            var position = worker.transform.position;
            float angle = SunkenAngle(position);
            float delta = Mathf.DeltaAngle(sunkenPreviousAngle, angle);
            float limit = Time.deltaTime * 8f + .2f;
            if (Vector3.Distance(position, sunkenPreviousPosition) > limit)
            { StopWork(); return; }
            sunkenPreviousPosition = position;
            sunkenPreviousAngle = angle;
            sunkenRopeEnd.Value = position + Vector3.up * 1.1f + worker.transform.forward * .2f;
            if (sunkenDirection.Value == 0f && Mathf.Abs(delta) > .1f) sunkenDirection.Value = Mathf.Sign(delta);
            progress.Value = Mathf.Clamp01(progress.Value + delta * sunkenDirection.Value / (360f * Mathf.Clamp(Catalog.SunkenUnwrapTurns, 1, 5)));
            if (progress.Value >= 1f) Unlock();
        }

        internal PlayerCommand SunkenOrbitCommand(Vector3 position)
        {
            var offset = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            float radius = offset.magnitude;
            var radial = radius > .01f ? offset / radius : Vector3.right;
            float direction = sunkenDirection.Value == 0f ? 1f : sunkenDirection.Value;
            var velocity = new Vector3(-radial.z, 0, radial.x) * direction + radial * Mathf.Clamp((1.8f - radius) * 1.5f, -1f, 1f);
            float height = transform.position.y - .6f - position.y;
            return new PlayerCommand { Yaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg, Move = Vector2.up, Rise = height > .2f, Crouch = height < -.2f };
        }

        void CreateSunkenRope()
        {
            sunkenRopeMaterial = new Material(markerMaterial);
            sunkenRopeMaterial.SetColor("_BaseColor", new Color(.45f, .32f, .16f));
            rope = eventVisual.AddComponent<LineRenderer>();
            rope.sharedMaterial = sunkenRopeMaterial;
            rope.useWorldSpace = true;
            rope.widthMultiplier = .045f;
            rope.numCornerVertices = 4;
            rope.numCapVertices = 4;
            sunkenGrip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sunkenGrip.name = "SunkenRopeEnd";
            sunkenGrip.transform.SetParent(transform, false);
            sunkenGrip.transform.localScale = Vector3.one * .12f;
            sunkenGrip.GetComponent<Renderer>().sharedMaterial = sunkenRopeMaterial;
            shownRopeEnd = sunkenRopeEnd.Value;
            UpdateSunkenRope();
        }

        void UpdateSunkenRope()
        {
            if (rope == null) return;
            bool locked = phase.Value == SeaLootState.Locked;
            rope.enabled = locked;
            sunkenGrip.SetActive(locked && !occupied.Value);
            if (!locked) return;
            float turns = Mathf.Clamp(Catalog.SunkenUnwrapTurns, 1, 5);
            float remaining = (1f - progress.Value) * turns * Mathf.PI * 2f;
            int segments = Mathf.Max(1, Mathf.CeilToInt(remaining * 16f));
            rope.positionCount = segments + 3;
            rope.SetPosition(0, sunkenFloor.Value);
            float direction = sunkenDirection.Value == 0f ? 1f : sunkenDirection.Value;
            var box = chestCollider as BoxCollider;
            float halfX = box != null ? box.size.x * .5f + .035f : .635f;
            float halfZ = box != null ? box.size.z * .5f + .035f : .435f;
            for (int i = 0; i <= segments; i++)
            {
                float bearing = -direction * remaining * i / segments;
                float x = Mathf.Cos(bearing), z = Mathf.Sin(bearing);
                float radius = 1f / Mathf.Max(Mathf.Abs(x) / halfX, Mathf.Abs(z) / halfZ);
                float height = .18f + .44f * (1f - progress.Value) * i / segments;
                var local = new Vector3(x * radius, height, z * radius);
                if (box != null) { local.x += box.center.x; local.z += box.center.z; }
                rope.SetPosition(i + 1, transform.TransformPoint(local));
            }
            var target = occupied.Value ? sunkenRopeEnd.Value : SunkenFreeEnd;
            shownRopeEnd = Vector3.Lerp(shownRopeEnd, target, 1f - Mathf.Exp(-15f * Time.deltaTime));
            rope.SetPosition(segments + 2, shownRopeEnd);
            sunkenGrip.transform.position = SunkenFreeEnd;
        }
    }
}
