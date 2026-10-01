using PirateSlop.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop
{
    public sealed partial class PlayerInventory
    {
        NetworkLootChest lockpickChest;
        float lockpickAngle, lockpickOpenedAt, nextLockInput;
        int lockpickRound = -1;
        bool lockpickPressing;
        float lockSoundAngle, lockSoundRotation, lockSoundStress;
        float nextLockMoveSound, nextLockTurnSound, nextLockJamSound;

        bool HandleRaftLockpick(NetworkLootChest chest)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (lockpickChest != chest)
            {
                lockpickChest = chest;
                lockpickOpenedAt = Time.unscaledTime;
                lockpickAngle = 0f;
                lockpickRound = chest.LockRound;
                nextLockInput = 0f;
                lockSoundAngle = lockSoundRotation = lockSoundStress = 0f;
                nextLockMoveSound = nextLockTurnSound = nextLockJamSound = 0f;
            }
            if (lockpickRound != chest.LockRound)
            {
                lockpickRound = chest.LockRound;
                lockpickPressing = false;
            }
            bool cancel = !motor.InputActive || keyboard == null || mouse == null;
            if (keyboard != null)
                cancel |= keyboard.qKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame ||
                    (Time.unscaledTime - lockpickOpenedAt > .25f && keyboard.eKey.wasPressedThisFrame);
            if (cancel)
            {
                network.LootInput(false);
                lockpickPressing = false;
                return true;
            }
            float direction = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            lockpickAngle = Mathf.Clamp(lockpickAngle + mouse.delta.ReadValue().x * .35f + direction * 80f * Time.deltaTime, -85f, 85f);
            lockpickPressing = mouse.leftButton.isPressed || keyboard.spaceKey.isPressed;
            UpdateRaftLockSounds(chest);
            if (Time.unscaledTime >= nextLockInput)
            {
                network.RaftLockInput(lockpickAngle, lockpickPressing, chest.LockRound);
                nextLockInput = Time.unscaledTime + .08f;
            }
            return true;
        }

        void UpdateRaftLockSounds(NetworkLootChest chest)
        {
            float now = Time.unscaledTime;
            if (Mathf.Abs(lockpickAngle - lockSoundAngle) >= 4f && now >= nextLockMoveSound)
            {
                GameAudio.Play(SoundCue.LockpickMove, Vector3.zero, 1f, true);
                lockSoundAngle = lockpickAngle;
                nextLockMoveSound = now + .18f;
            }
            if (chest.LockRotation < lockSoundRotation) lockSoundRotation = chest.LockRotation;
            if (lockpickPressing && chest.LockRotation - lockSoundRotation >= 6f && now >= nextLockTurnSound)
            {
                GameAudio.Play(SoundCue.LockpickTurn, Vector3.zero, .85f, true);
                lockSoundRotation = chest.LockRotation;
                nextLockTurnSound = now + .3f;
            }
            if (lockpickPressing && chest.LockStress > .03f && chest.LockStress > lockSoundStress + .005f && now >= nextLockJamSound)
            {
                GameAudio.Play(SoundCue.LockpickJam, Vector3.zero, Mathf.Lerp(.65f, 1f, chest.LockStress), true);
                nextLockJamSound = now + .48f;
            }
            lockSoundStress = chest.LockStress;
        }

        void DrawRaftLockpick(NetworkLootChest chest)
        {
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            float scale = Mathf.Min(1f, Mathf.Min(Screen.width / 760f, Screen.height / 560f));
            GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width * .5f, Screen.height * .5f, 0), Quaternion.identity, Vector3.one * scale);
            PirateHudStyle.Fill(new Rect(-Screen.width / scale, -Screen.height / scale, Screen.width * 2f / scale, Screen.height * 2f / scale), new Color(0, 0, 0, .65f));
            PirateHudStyle.Fill(new Rect(-340, -245, 680, 490), new Color(.025f, .05f, .065f, .98f));
            PirateHudStyle.Label(new Rect(-320, -222, 640, 38), "ВЗЛОМ СУНДУКА", PirateHudStyle.Gold, true);
            PirateHudStyle.Label(new Rect(-310, -185, 620, 32), "Найди угол, при котором замок свободно поворачивается", PirateHudStyle.Paper);
            var center = new Vector2(0, -12);
            LockCircle(center, 106, 12, new Color(.30f, .24f, .13f));
            LockCircle(center, 87, 8, PirateHudStyle.Gold);
            LockCircle(center, 66, 48, new Color(.11f, .13f, .14f));
            for (int i = 0; i < 12; i++)
            {
                float bearing = i * Mathf.PI / 6f;
                var screw = center + new Vector2(Mathf.Sin(bearing), Mathf.Cos(bearing)) * 98f;
                PirateHudStyle.Fill(new Rect(screw.x - 3, screw.y - 3, 6, 6), PirateHudStyle.Ink);
            }
            var rotor = GUI.matrix;
            GUI.matrix = rotor * Matrix4x4.Translate(center) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, chest.LockRotation)) * Matrix4x4.Translate(-center);
            PirateHudStyle.Fill(new Rect(-10, center.y - 37, 20, 74), Color.black);
            PirateHudStyle.Fill(new Rect(-5, center.y - 29, 10, 58), new Color(.18f, .19f, .19f));
            LockLine(center, center + Vector2.down * 155f, 6, new Color(.46f, .48f, .50f));
            GUI.matrix = rotor;
            float angle = lockpickAngle * Mathf.Deg2Rad;
            var end = center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * 175f;
            float shake = chest.LockStress * Mathf.Sin(Time.unscaledTime * 70f) * 3f;
            end.x += shake;
            LockLine(center, end, 4, Color.Lerp(PirateHudStyle.Gold, new Color(1f, .25f, .14f), chest.LockStress));
            LockLine(end, end + new Vector2(12f, 5f), 4, PirateHudStyle.Paper);
            string feedback = chest.LockStress > .25f ? "ЗАЕДАЕТ — отпусти ЛКМ и измени угол" : lockpickPressing ? "Проверяешь замок…" : "Мышь / A, D — угол отмычки";
            PirateHudStyle.Label(new Rect(-300, 112, 600, 28), feedback, chest.LockStress > .25f ? new Color(1f, .42f, .28f) : PirateHudStyle.Paper);
            PirateHudStyle.Bar(new Rect(-210, 155, 420, 18), chest.LockStress, Color.Lerp(PirateHudStyle.Gold, Color.red, chest.LockStress));
            PirateHudStyle.Label(new Rect(-280, 176, 560, 27), "Отмычки: " + chest.LockPicks + " / 3 · Напряжение: " + Mathf.RoundToInt(chest.LockStress * 100f) + "%", PirateHudStyle.Muted);
            PirateHudStyle.Label(new Rect(-320, 208, 640, 28), "Удерживай ЛКМ / пробел — повернуть · Q / E / Esc — выйти", PirateHudStyle.Paper);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        static void LockCircle(Vector2 center, float radius, float width, Color color)
        {
            for (int i = 0; i < 96; i++)
            {
                float a = i * Mathf.PI * 2f / 96f, b = (i + 1) * Mathf.PI * 2f / 96f;
                LockLine(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color);
            }
        }

        static void LockLine(Vector2 from, Vector2 to, float width, Color color)
        {
            var matrix = GUI.matrix;
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            GUI.matrix = matrix * Matrix4x4.TRS(from, Quaternion.Euler(0, 0, angle), Vector3.one);
            PirateHudStyle.Fill(new Rect(0, -width * .5f, Vector2.Distance(from, to), width), color);
            GUI.matrix = matrix;
        }
    }
}
