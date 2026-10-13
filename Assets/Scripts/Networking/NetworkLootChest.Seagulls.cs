using PirateSlop.World;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        readonly SeagullVisual[] sunkenGulls = new SeagullVisual[3];
        readonly SyncVar<SeagullFall> sunkenFallA = new(), sunkenFallB = new(), sunkenFallC = new();
        readonly SyncVar<byte> sunkenShotMask = new();
        readonly float[] sunkenShotAt = new float[3];
        SyncVar<SeagullFall> SunkenFall(int index) => index == 0 ? sunkenFallA : index == 1 ? sunkenFallB : sunkenFallC;
        float nextSunkenFallSync;

        void CreateSunkenGulls()
        {
            for (int i = 0; i < sunkenGulls.Length; i++)
            {
                int index = i;
                sunkenGulls[i] = SeagullVisual.Create(eventVisual.transform, i * 113 + Mathf.RoundToInt(eventPoint.Value.x));
                sunkenGulls[i].name = "SunkenChestSeagull";
                sunkenGulls[i].FirearmHit = direction => ShootSunkenGull(index, direction);
                sunkenGulls[i].SurfaceContact = fall => LandSunkenGull(index, fall);
            }
            UpdateSunkenGulls();
        }

        void UpdateSunkenGulls()
        {
            float time = OceanSurface.Instance != null ? OceanSurface.Instance.WaveTime : Time.time;
            bool syncFalls = IsServerInitialized && Time.time >= nextSunkenFallSync;
            if (syncFalls) nextSunkenFallSync = Time.time + .2f;
            for (int i = 0; i < sunkenGulls.Length; i++)
            {
                var bird = sunkenGulls[i];
                if (bird == null) continue;
                if ((sunkenShotMask.Value & (1 << i)) != 0)
                {
                    var synced = SunkenFall(i);
                    var fall = synced.Value;
                    if (syncFalls && fall.Age < fall.Seconds + SeagullVisual.SinkSeconds)
                    {
                        fall.Age = Mathf.Min(fall.Seconds + SeagullVisual.SinkSeconds, Time.time - sunkenShotAt[i]);
                        synced.Value = fall;
                    }
                    bird.BeginFall(fall);
                    continue;
                }
                bool show = phase.Value != SeaLootState.Ready;
                bird.gameObject.SetActive(show);
                if (!show) continue;
                float angle = SunkenGullAngle(i, time);
                bird.transform.position = SunkenGullPosition(i, time);
                bird.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle))) * Quaternion.Euler(0, 0, -12f);
                bird.Animate(time, SeagullVisual.FlightPower(time, i * 113), 0, 0);
            }
        }

        float SunkenGullAngle(int index, float time) => time * .45f + index * Mathf.PI * 2f / 3f + eventPoint.Value.x * .01f;

        Vector3 SunkenGullPosition(int index, float time)
        {
            float angle = SunkenGullAngle(index, time);
            float radius = 6f;
            var center = eventPoint.Value;
            var point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (OceanSurface.Instance != null) point.y = OceanSurface.Instance.Height(point);
            return point + Vector3.up * 3f;
        }

        void ShootSunkenGull(int index, Vector3 direction)
        {
            if (!IsServerInitialized || !IsSpawned || Kind != SeaLootKind.Sunken || phase.Value == SeaLootState.Ready
                || index < 0 || index >= sunkenGulls.Length || (sunkenShotMask.Value & (1 << index)) != 0) return;
            var bird = sunkenGulls[index];
            if (bird == null || bird.Shot || !bird.gameObject.activeSelf) return;
            float time = OceanSurface.Instance != null ? OceanSurface.Instance.WaveTime : Time.time;
            var fall = bird.CaptureFall((SunkenGullPosition(index, time + .04f) - SunkenGullPosition(index, time)) / .04f, direction);
            SunkenFall(index).Value = fall;
            sunkenShotAt[index] = Time.time;
            sunkenShotMask.Value |= (byte)(1 << index);
            bird.BeginFall(fall);
        }

        void LandSunkenGull(int index, SeagullFall fall)
        {
            if (!IsServerInitialized || !IsSpawned || !fall.Landed || index < 0 || index >= sunkenGulls.Length
                || (sunkenShotMask.Value & (1 << index)) == 0) return;
            fall.Age = Time.time - sunkenShotAt[index];
            SunkenFall(index).Value = fall;
            sunkenGulls[index].BeginFall(fall);
        }
    }
}
