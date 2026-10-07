using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Broadcast;
using FishNet.Transporting;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct UpgradeIdentityMessage : IBroadcast { public string Token; }

    public sealed partial class SessionController
    {
        readonly Dictionary<string, PlayerUpgradeState> upgradePlayers = new();
        readonly Dictionary<int, int> teamChestNumbers = new();
        readonly System.Random upgradeRandom = new();
        RoguelikeCatalog upgradeCatalog;
        string UpgradeTokenKey => "RoguelikeResume:" + (steamSession ? "Steam:" + steamHost : address);
        string UpgradeResumeToken => PlayerPrefs.GetString(UpgradeTokenKey, "");

        System.Collections.IEnumerator SpawnTestUpgradeChest(NetworkShip ship)
        {
            yield return null;
            if (ship == null || !ship.IsSpawned || !EnvironmentTestActive || manager == null || !manager.ServerManager.Started || Config.Loot == null || Config.Loot.ChestPrefab == null) yield break;
            Physics.SyncTransforms();
            if (!TryFindTestChestPoint(ship, Config.Loot.ChestPrefab, out var point))
            {
                Debug.LogWarning("ROGUELIKE_TEST_CHEST: No free deck surface on the test ship.");
                yield break;
            }
            var chest = Instantiate(Config.Loot.ChestPrefab, point, ship.transform.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, ship.gameObject.scene);
            chest.name = "TestUpgradeChest";
            chest.Catalog = Config.Loot;
            var random = new PirateSlop.World.MapRandom(unchecked((uint)ship.ParticipantId.Value ^ 0x715ca3u));
            chest.Fill(ref random);
            chest.PlaceOnDeck(ship, point);
            manager.ServerManager.Spawn(chest.NetworkObject);
        }

        bool TryFindTestChestPoint(NetworkShip ship, NetworkLootChest prefab, out Vector3 point)
        {
            point = default;
            var box = prefab.GetComponent<BoxCollider>();
            Vector3 halfSize = box != null ? Vector3.Scale(box.size, prefab.transform.localScale) * .5f : new Vector3(.6f, .4f, .4f);
            Vector3 boxCenter = box != null ? Vector3.Scale(box.center, prefab.transform.localScale) : Vector3.up * .4f;
            var offsets = new[]
            {
                new Vector3(2.8f, 0, 0), new Vector3(-2.8f, 0, 0),
                new Vector3(0, 0, 2.8f), new Vector3(0, 0, -2.8f),
                new Vector3(2.8f, 0, 2.8f), new Vector3(-2.8f, 0, 2.8f),
                new Vector3(2.8f, 0, -2.8f), new Vector3(-2.8f, 0, -2.8f)
            };
            var colliders = ship.GetComponentsInChildren<Collider>();
            foreach (var offset in offsets)
            {
                var ray = new Ray(ship.transform.TransformPoint(Config.PlayerLocalSpawn + offset + Vector3.up * 2f), -ship.transform.up);
                Collider floor = null;
                RaycastHit nearest = default;
                float distance = 4f;
                foreach (var collider in colliders)
                {
                    if (!collider.enabled || collider.isTrigger || !collider.Raycast(ray, out var hit, distance) || Vector3.Dot(hit.normal, ship.transform.up) < .7f) continue;
                    float localHeight = ship.transform.InverseTransformPoint(hit.point).y;
                    if (localHeight > Config.PlayerLocalSpawn.y + .3f || localHeight < Config.PlayerLocalSpawn.y - 1.5f) continue;
                    floor = collider;
                    nearest = hit;
                    distance = hit.distance;
                }
                if (floor == null) continue;
                Vector3 candidate = nearest.point + ship.transform.up * .03f;
                Vector3 center = candidate + ship.transform.rotation * boxCenter + ship.transform.up * .08f;
                bool clear = true;
                foreach (var obstacle in Physics.OverlapBox(center, halfSize * .95f, ship.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (obstacle == floor || Vector3.Dot(obstacle.bounds.max - nearest.point, ship.transform.up) <= .15f) continue;
                    clear = false;
                    break;
                }
                if (!clear) continue;
                point = candidate;
                return true;
            }
            return false;
        }

        void ReceiveUpgradeIdentity(UpgradeIdentityMessage message, Channel channel)
        {
            if (string.IsNullOrEmpty(message.Token) || message.Token.Length > 80) return;
            PlayerPrefs.SetString(UpgradeTokenKey, message.Token);
            PlayerPrefs.Save();
        }

        void ResetUpgrades()
        {
            upgradePlayers.Clear();
            teamChestNumbers.Clear();
            upgradeCatalog = null;
            RoguelikeTuning.Load();
        }

        PlayerUpgradeState ResumeUpgrades(string token, int steamCrew)
        {
            if (string.IsNullOrEmpty(token) || token.Length > 80 || !upgradePlayers.TryGetValue(token, out var state) || state.SteamCrew != steamCrew) return null;
            return state;
        }

        void AttachUpgrades(NetworkPlayer player, PlayerUpgradeState previous, int steamCrew)
        {
            var state = previous ?? new PlayerUpgradeState { Token = Guid.NewGuid().ToString("N"), Team = player.TeamId.Value, SteamCrew = steamCrew };
            state.Player = player; state.SteamCrew = steamCrew;
            player.UpgradeState = state;
            upgradePlayers[state.Token] = state;
            player.RestoreUpgradeEffects();
            if (state.PendingPact) player.GetComponent<CombatHealth>().ApplySnapshot(0f, false);
        }

        public bool SlotUpgradeAvailable => upgradeCatalog != null;

        internal bool CanAwardPersonalUpgrade(PlayerUpgradeState state) => upgradeCatalog != null && state != null &&
            upgradeCatalog.Cards.Any(card => upgradeCatalog.Eligible(card, state.Taken));

        internal bool AwardPersonalUpgrade(PlayerUpgradeState state)
        {
            if (!CanAwardPersonalUpgrade(state)) return false;
            int ordinal = teamChestNumbers.TryGetValue(state.Team, out int count) ? Mathf.Max(1, count) : 1;
            state.Rewards.Add(new UpgradeReward { ChestNumber = ordinal, Weights = upgradeCatalog.WeightsAt(ordinal) });
            state.Revision++;
            EnsureUpgradeOffer(state);
            var player = state.Player;
            if (player != null && player.IsSpawned) player.SendUpgrades();
            return true;
        }

        internal void AwardChestUpgrade(NetworkPlayer opener)
        {
            if (upgradeCatalog == null || opener == null || !opener.IsServerInitialized || opener.TeamId.Value <= 0) return;
            int team = opener.TeamId.Value;
            int ordinal = teamChestNumbers.TryGetValue(team, out int count) ? count + 1 : 1;
            teamChestNumbers[team] = ordinal;
            foreach (var player in players.Values)
            {
                if (player == null || !player.IsSpawned || player.TeamId.Value != team || player.Eliminated.Value) continue;
                if (player.UpgradeState == null) AttachUpgrades(player, null, 0);
                var state = player.UpgradeState;
                if (!upgradeCatalog.Cards.Any(card => upgradeCatalog.Eligible(card, state.Taken))) continue;
                state.Rewards.Add(new UpgradeReward { ChestNumber = ordinal, Weights = upgradeCatalog.WeightsAt(ordinal) });
                state.Revision++;
                EnsureUpgradeOffer(state);
                if (player.IsBot.Value)
                {
                    while (state.Rewards.Count > 0)
                    {
                        EnsureUpgradeOffer(state);
                        if (state.Rewards.Count == 0) break;
                        ChooseUpgrade(player, state.Rewards[0].Offers[upgradeRandom.Next(state.Rewards[0].Offers.Length)]);
                    }
                }
                else player.SendUpgrades();
            }
        }

        void EnsureUpgradeOffer(PlayerUpgradeState state)
        {
            while (upgradeCatalog != null && state.Rewards.Count > 0)
            {
                var reward = state.Rewards[0];
                if (reward.Offers == null) reward.Offers = upgradeCatalog.Roll(state.Taken, reward.Weights, upgradeRandom);
                if (reward.Offers.Length > 0) return;
                state.Rewards.Clear();
                state.Revision++;
            }
        }

        internal void ChooseUpgrade(NetworkPlayer player, string id)
        {
            var state = player.UpgradeState;
            if (upgradeCatalog == null || state == null || !player.IsServerInitialized || !player.IsSpawned) return;
            EnsureUpgradeOffer(state);
            if (state.Rewards.Count == 0 || !state.Rewards[0].Offers.Contains(id)) return;
            var card = upgradeCatalog.Find(id);
            if (card == null || !upgradeCatalog.Eligible(card, state.Taken)) return;
            state.Taken.Add(id);
            state.Rewards.RemoveAt(0);
            state.Revision++;
            EnsureUpgradeOffer(state);
            player.NotifyUpgradeChosen(card);
        }

        internal string GrantDeveloperUpgrade(NetworkPlayer player, int index)
        {
            if (upgradeCatalog == null || player == null || !player.IsServerInitialized || !player.IsSpawned || player.UpgradeState == null)
                return "Улучшения ещё не готовы для этого игрока.";
            var cards = upgradeCatalog.Cards.OrderBy(card => card.id, StringComparer.Ordinal).ToArray();
            if (index < 0 || index >= cards.Length) return "Улучшение не найдено.";
            var card = cards[index];
            var state = player.UpgradeState;
            if (!state.Taken.Add(card.id)) return "Это улучшение уже получено.";
            foreach (var reward in state.Rewards) reward.Offers = null;
            state.Revision++;
            EnsureUpgradeOffer(state);
            player.NotifyUpgradeChosen(card);
            player.SendUpgrades();
            return "Получено: " + card.name;
        }

        int AvailableRerolls(PlayerUpgradeState state, UpgradeReward reward) => reward != null && state.Taken.Contains("LuckyCoin") && reward.ChestNumber % RoguelikeTuning.Current.rerollChestInterval == 0 ? Mathf.Max(0, RoguelikeTuning.Current.rerolls - reward.RerollsUsed) : 0;

        internal void RerollUpgrade(NetworkPlayer player)
        {
            var state = player.UpgradeState;
            if (!player.IsServerInitialized || !player.IsSpawned || upgradeCatalog == null || state == null) return;
            EnsureUpgradeOffer(state);
            if (state.Rewards.Count == 0 || AvailableRerolls(state, state.Rewards[0]) <= 0) return;
            var reward = state.Rewards[0];
            var excluded = new System.Collections.Generic.HashSet<string>(state.Taken);
            foreach (string id in reward.Offers) excluded.Add(id);
            var offers = upgradeCatalog.Roll(excluded, reward.Weights, upgradeRandom);
            reward.Offers = offers.Length > 0 ? offers : upgradeCatalog.Roll(state.Taken, reward.Weights, upgradeRandom);
            reward.RerollsUsed++; state.Revision++;
        }

        internal UpgradeSnapshot UpgradeSnapshotFor(NetworkPlayer player)
        {
            var state = player.UpgradeState;
            if (state == null || upgradeCatalog == null) return new UpgradeSnapshot();
            EnsureUpgradeOffer(state);
            var reward = state.Rewards.Count > 0 ? state.Rewards[0] : null;
            return new UpgradeSnapshot
            {
                revision = state.Revision,
                points = state.Rewards.Count,
                chestNumber = reward != null ? reward.ChestNumber : 0,
                rerolls = AvailableRerolls(state, reward),
                offers = reward != null ? reward.Offers.Select(upgradeCatalog.Find).ToArray() : Array.Empty<UpgradeCard>(),
                owned = state.Taken.Select(upgradeCatalog.Find).Where(card => card != null).ToArray()
            };
        }
    }
}
