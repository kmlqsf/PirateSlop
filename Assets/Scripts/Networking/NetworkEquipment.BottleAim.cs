using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkEquipment
    {
        LineRenderer bottleArc;
        int bottleAimSlot = -1;
        InventoryItem bottleAimItem = InventoryItem.None;
        bool ThrowableBottle => Item == InventoryItem.FogBottle || Item == InventoryItem.VortexBottle;
        void CancelBottleAim()
        {
            bottleAimSlot = -1;
            bottleAimItem = InventoryItem.None;
            if (bottleArc != null) bottleArc.enabled = false;
        }
        bool ReadBottleInput(Mouse mouse)
        {
            if (!ThrowableBottle) { CancelBottleAim(); return false; }
            if (bottleAimSlot != inventory.SelectedSlot || bottleAimItem != Item) CancelBottleAim();
            if (inventory.InteractionUsed || inventory.ControlFocused || hands.CanPickUpBall() || action.Value != 0 ||
                (GetComponent<DirectShipControls>()?.BlocksPrimary ?? false) ||
                (GetComponent<PirateSlop.Ships.ShipSlotMachinePlayer>()?.ConsumedInput ?? false))
            {
                CancelBottleAim();
                return true;
            }
            if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextLocalFire)
            {
                bottleAimSlot = inventory.SelectedSlot;
                bottleAimItem = Item;
            }
            if (bottleAimSlot < 0) return true;
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                UseServerRpc(0, motor.AimDirection, motor.PlayerCamera.transform.position - transform.position, ++localSequence, false);
                nextLocalFire = Time.time + .6f;
                CancelBottleAim();
                return true;
            }
            if (!mouse.leftButton.isPressed) { CancelBottleAim(); return true; }
            DrawBottleTrajectory();
            return true;
        }
        void DrawBottleTrajectory()
        {
            float speed = Item == InventoryItem.FogBottle ? NetworkFogBottle.ThrowSpeed : NetworkVortexBottle.ThrowSpeed;
            if (!network.GetBottleLaunch(motor.AimDirection, motor.PlayerCamera.transform.position - transform.position, speed, out var point, out var velocity))
            {
                if (bottleArc != null) bottleArc.enabled = false;
                return;
            }
            if (bottleArc == null)
            {
                var root = new GameObject("BottleTrajectory");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
                bottleArc = root.AddComponent<LineRenderer>();
                bottleArc.sharedMaterial = GetComponent<NetworkHolyGrenadeHands>().ArcMaterial;
                bottleArc.startWidth = bottleArc.endWidth = .035f;
                bottleArc.startColor = bottleArc.endColor = new Color(1f, .88f, .45f);
                bottleArc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bottleArc.receiveShadows = false;
            }
            bottleArc.enabled = true;
            bottleArc.positionCount = 751;
            bottleArc.SetPosition(0, point);
            int count = 1;
            for (int i = 0; i < 750; i++)
            {
                bool hit = NetworkFogBottle.Advance(transform, null, ref point, ref velocity, .02f, out _, out _);
                bottleArc.SetPosition(count++, point);
                if (hit) break;
            }
            bottleArc.positionCount = count;
        }
    }
}
