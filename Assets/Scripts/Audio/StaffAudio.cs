using UnityEngine;
using PirateSlop.Networking;

namespace PirateSlop
{
    public static class StaffAudio
    {
        public static SoundCue Charge(InventoryItem item) => item == InventoryItem.FireStaff ? SoundCue.FireStaffCharge : item == InventoryItem.IceStaff ? SoundCue.IceStaffCharge : SoundCue.LightningStaffCharge;
        public static SoundCue Cast(InventoryItem item) => item == InventoryItem.FireStaff ? SoundCue.FireStaffCast : item == InventoryItem.IceStaff ? SoundCue.IceStaffCast : SoundCue.LightningStaffCast;
        public static SoundCue Flight(InventoryItem item) => item == InventoryItem.FireStaff ? SoundCue.FireStaffFlight : SoundCue.IceStaffFlight;
        public static SoundCue Impact(InventoryItem item) => item == InventoryItem.FireStaff ? SoundCue.FireStaffImpact : item == InventoryItem.IceStaff ? SoundCue.IceStaffImpact : SoundCue.LightningStaffImpact;
        public static void Stop(ref AudioSource source)
        {
            if (source == null) return;
            GameAudio.StopLoop(source);
            Object.Destroy(source.gameObject);
            source = null;
        }
    }
}
