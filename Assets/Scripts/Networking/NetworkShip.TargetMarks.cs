using System.Collections.Generic;
using FishNet.Object;
using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkShip
    {
        [UnityEngine.SerializeField, Min(1f)] float targetMarkLifetime = 120f;
        [UnityEngine.SerializeField, Range(1, 16)] int maxTargetMarks = 8;
        sealed class TargetMark
        {
            public int Id;
            public Transform Target;
            public Vector3 LocalPoint;
            public float Expires;
            public bool Seagull;
            public NetworkObject SeagullEvent;
            public int BirdIndex = -1;
            public int AuthorId;
            public byte CrewColor;
        }
        readonly List<TargetMark> targetMarks = new();
        readonly Dictionary<int, byte> seagullCrewColors = new();
        int nextMarkId;

        public void AddTargetMark(RaycastHit hit, float lifetime = 0f, NetworkPlayer author = null)
        {
            if (!IsServerInitialized || hit.collider == null) return;
            var bird = hit.collider.GetComponentInParent<SeagullVisual>();
            if (bird != null) { AddSeagullTargetMark(bird, author); return; }
            if (lifetime <= 0f) lifetime = targetMarkLifetime;
            PruneTargetMarks();
            var networkTarget = hit.collider.GetComponentInParent<NetworkObject>();
            Transform target = networkTarget != null ? networkTarget.transform : hit.collider.transform;
            Vector3 localPoint = target.InverseTransformPoint(hit.point);
            foreach (var mark in targetMarks)
            {
                if (mark.Target != target || (networkTarget == null && Vector3.Distance(mark.LocalPoint, localPoint) > 5f)) continue;
                mark.LocalPoint = localPoint;
                mark.Expires = Mathf.Max(mark.Expires, Time.time + lifetime);
                return;
            }
            MakeTargetMarkRoom();
            targetMarks.Add(new TargetMark { Id = ++nextMarkId, Target = target, LocalPoint = localPoint, Expires = Time.time + lifetime });
        }

        public void AddSeagullTargetMark(SeagullVisual bird, NetworkPlayer author)
        {
            if (!IsServerInitialized || bird == null || bird.Shot || !bird.gameObject.activeInHierarchy || author == null || !author.IsSpawned
                || author.ParticipantId.Value <= 0 || author.Ship != this || author.TeamId.Value != TeamId.Value) return;
            PruneTargetMarks();
            foreach (var mark in targetMarks)
                if (mark.Seagull && mark.AuthorId == author.ParticipantId.Value) return;
            var owner = bird.GetComponentInParent<NetworkSeagullEvent>();
            byte color = SeagullCrewColor(author);
            MakeTargetMarkRoom();
            targetMarks.Add(new TargetMark { Id = ++nextMarkId, Target = bird.transform, LocalPoint = Vector3.zero, Expires = Time.time + 5f,
                Seagull = true, SeagullEvent = owner != null ? owner.NetworkObject : null, BirdIndex = owner != null ? owner.IndexOf(bird) : -1,
                AuthorId = author.ParticipantId.Value, CrewColor = color });
        }

        void MakeTargetMarkRoom()
        {
            if (targetMarks.Count < maxTargetMarks) return;
            int oldest = targetMarks.FindIndex(mark => !mark.Seagull);
            if (oldest >= 0) targetMarks.RemoveAt(oldest);
        }
        byte SeagullCrewColor(NetworkPlayer author)
        {
            var members = new List<int>();
            foreach (var player in NetworkPlayer.Active)
                if (player != null && player.IsSpawned && player.Ship == this && player.TeamId.Value == TeamId.Value && player.ParticipantId.Value > 0)
                    members.Add(player.ParticipantId.Value);
            if (!members.Contains(author.ParticipantId.Value)) members.Add(author.ParticipantId.Value);
            members.Sort();
            foreach (int id in new List<int>(seagullCrewColors.Keys)) if (!members.Contains(id)) seagullCrewColors.Remove(id);
            foreach (int id in members)
            {
                if (seagullCrewColors.ContainsKey(id)) continue;
                byte color = 0;
                while (color < 3 && seagullCrewColors.ContainsValue(color)) color++;
                seagullCrewColors[id] = (byte)(color % 3);
            }
            return seagullCrewColors[author.ParticipantId.Value];
        }

        void PruneTargetMarks() => targetMarks.RemoveAll(mark => mark.Target == null || !mark.Target.gameObject.activeInHierarchy || Time.time >= mark.Expires);

        public TargetMarkState[] GetTargetMarks()
        {
            PruneTargetMarks();
            var result = new TargetMarkState[targetMarks.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var mark = targetMarks[i];
                Vector3 position = mark.Target.TransformPoint(mark.LocalPoint);
                result[i] = new TargetMarkState { Id = mark.Id, Position = position, Distance = Vector3.Distance(transform.position, position), SecondsLeft = mark.Expires - Time.time,
                    Seagull = mark.Seagull, SeagullEvent = mark.SeagullEvent, BirdIndex = mark.BirdIndex, AuthorId = mark.AuthorId, CrewColor = mark.CrewColor };
            }
            return result;
        }
    }
}
