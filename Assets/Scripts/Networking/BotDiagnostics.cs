using System;
using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Networking
{
    public struct BotDebugRow
    {
        public int Number, Team;
        public bool Removed;
        public string State;
    }

    public struct BotDebugSnapshot
    {
        public int Number, Revision;
        public Vector3 Position;
        public string Goal, Reason, Action, Navigation, Perception, Crew, Failure, Candidates;
        public string[] History;
    }

    public sealed class BotDebugJournal
    {
        public const int Capacity = 64;
        readonly Queue<string> history = new();
        public BotDebugRow Row;
        public BotDebugSnapshot Snapshot;
        public NetworkPlayer Player;

        public BotDebugJournal(NetworkPlayer player) : this(player.BotNumber, player.TeamId.Value)
        {
            Player = player;
        }

        public BotDebugJournal(int number, int team)
        {
            Row = new BotDebugRow { Number = number, Team = team, State = "Ожидание" };
            Snapshot = new BotDebugSnapshot {
                Number = number, Goal = "Не назначена", Reason = "Ожидание явной задачи; автоматический выбор ещё не реализован",
                Action = "Ожидание задачи", Navigation = "Маршрут не запрашивался",
                Perception = "Восприятие ещё не реализовано", Crew = "Задачи и резервирования ещё не реализованы",
                Failure = "Нет зарегистрированных ошибок", Candidates = "Оценка задач ещё не реализована"
            };
            Add("Создан серверный бот");
        }

        public static string Limit(string value) => string.IsNullOrEmpty(value) ? "—" : value.Length > 240 ? value.Substring(0, 240) : value;

        public void Add(string message)
        {
            if (history.Count == Capacity) history.Dequeue();
            history.Enqueue($"{Time.time:F1} с · {Limit(message)}");
            Snapshot.Revision++;
        }

        public void Sample()
        {
            if (Row.Removed) return;
            if (Player == null || !Player.IsSpawned) { Remove("Удалён из сессии"); return; }
            Snapshot.Position = Player.transform.position;
            var motor = Player.Motor;
            string state = motor.IsDead ? "Мёртв" : motor.IsKnockedBack ? "Отброшен" : motor.IsSwimming ? "В воде" : motor.IsClimbing ? "На лестнице" : Player.BotStatus;
            if (Row.State != state) { Row.State = state; Add("Состояние: " + state); }
        }

        public void Remove(string reason)
        {
            if (Row.Removed) return;
            Row.Removed = true;
            Row.State = "Удалён";
            Add(reason);
            Player = null;
        }

        public void Decision(string goal, string reason, string action, string navigation, string perception, string crew, string failure, string candidates)
        {
            bool changed = Snapshot.Goal != Limit(goal) || Snapshot.Reason != Limit(reason) || Snapshot.Action != Limit(action) || Snapshot.Failure != Limit(failure);
            Snapshot.Goal = Limit(goal); Snapshot.Reason = Limit(reason); Snapshot.Action = Limit(action);
            Snapshot.Navigation = Limit(navigation); Snapshot.Perception = Limit(perception); Snapshot.Crew = Limit(crew);
            Snapshot.Failure = Limit(failure); Snapshot.Candidates = Limit(candidates);
            if (changed) Add("Решение: " + Snapshot.Action + "; " + Snapshot.Failure);
        }

        public BotDebugSnapshot Read(int knownRevision)
        {
            var result = Snapshot;
            result.History = knownRevision == Snapshot.Revision ? Array.Empty<string>() : history.ToArray();
            return result;
        }
    }
}
