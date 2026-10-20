#if UNITY_EDITOR || PIRATESLOP_UPPER_DESTRUCTION_V2
using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop.Ships
{
    public sealed partial class ShipV3Features
    {
        bool upperDiceDetached;
        float upperDiceScatteredAt;
        Vector3[][] upperDiceHome;
        Quaternion[][] upperDiceHomeRotation;
        readonly RaycastHit[] upperDiceGround = new RaycastHit[8];

        public void UpdateUpperDice(bool detached)
        {
            if (restingDice == null) return;
            if (upperDiceHome == null)
            {
                upperDiceHome = new Vector3[DiceSlots.Length][];
                upperDiceHomeRotation = new Quaternion[DiceSlots.Length][];
                for (int slot = 0; slot < DiceSlots.Length; slot++)
                {
                    upperDiceHome[slot] = (Vector3[])restingDice[slot].Clone();
                    upperDiceHomeRotation[slot] = (Quaternion[])restingDiceRotation[slot].Clone();
                }
            }
            if (upperDiceDetached == detached) return;
            upperDiceDetached = detached;
            upperDiceScatteredAt = Time.time;
            if (!IsServerInitialized) return;
            for (int slot = 0; slot < DiceSlots.Length; slot++)
            {
                ReleaseSlot(slot);
                previewCup[slot] = false;
                diceMode[slot] = detached ? (byte)7 : (byte)0;
                dicePhases[slot] = diceMode[slot];
                for (int die = 0; die < DiceSlots[slot].Dice.Length; die++)
                {
                    var body = DiceSlots[slot].Dice[die];
                    if (body == null) continue;
                    body.isKinematic = !detached;
                    if (detached)
                    {
                        float angle = (slot * 5 + die) * 2.399963f;
                        var direction = transform.right * Mathf.Cos(angle) + transform.forward * Mathf.Sin(angle);
                        body.linearVelocity = ship.Motor.CannonPointVelocity(body.position) + direction * (1.25f + die * .13f) + transform.up * 1.1f;
                        body.angularVelocity = direction * (4f + die);
                        body.WakeUp();
                    }
                    else
                    {
                        if (DiceSlots[slot].DiceVisuals != null && die < DiceSlots[slot].DiceVisuals.Length && DiceSlots[slot].DiceVisuals[die] != null) DiceSlots[slot].DiceVisuals[die].gameObject.SetActive(true);
                        restingDice[slot][die] = upperDiceHome[slot][die];
                        restingDiceRotation[slot][die] = upperDiceHomeRotation[slot][die];
                        body.position = transform.TransformPoint(restingDice[slot][die]);
                        body.rotation = transform.rotation * restingDiceRotation[slot][die];
                    }
                }
            }
            publishedFrame = null;
        }

        void SimulateUpperDice(int slot)
        {
            bool settled = true;
            for (int die = 0; die < DiceSlots[slot].Dice.Length; die++)
            {
                var body = DiceSlots[slot].Dice[die];
                if (body == null) continue;
                if (!body.isKinematic)
                {
                    float age = Time.time - upperDiceScatteredAt;
                    bool grounded = false;
                    if (age > .4f && (body.IsSleeping() || (body.linearVelocity - ship.Motor.CannonPointVelocity(body.position)).sqrMagnitude < .04f))
                    {
                        int count = Physics.RaycastNonAlloc(body.position + transform.up * .02f, -transform.up, upperDiceGround, .1f, ~0, QueryTriggerInteraction.Ignore);
                        for (int i = 0; i < count; i++)
                            if (upperDiceGround[i].collider.GetComponentInParent<NetworkShip>() == ship) { grounded = true; break; }
                    }
                    if (!grounded && age < 6f) { settled = false; continue; }
                    body.linearVelocity = body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                    restingDice[slot][die] = grounded ? transform.InverseTransformPoint(body.position) : new Vector3(0, -100f, 0);
                    restingDiceRotation[slot][die] = Quaternion.Inverse(transform.rotation) * body.rotation;
                }
                body.MovePosition(transform.TransformPoint(restingDice[slot][die]));
                body.MoveRotation(transform.rotation * restingDiceRotation[slot][die]);
            }
            if (settled && diceMode[slot] != 8) { diceMode[slot] = 8; dicePhases[slot] = 8; }
        }

        void RenderUpperDice(int slot, float blend)
        {
            var data = DiceSlots[slot];
            for (int die = 0; die < data.Dice.Length; die++)
            {
                if (data.DiceVisuals == null || die >= data.DiceVisuals.Length || data.DiceVisuals[die] == null) continue;
                var pose = DiceBodyPose(diceBodyIndices[slot][die], data.Dice[die]);
                data.DiceVisuals[die].gameObject.SetActive(pose.Position.y > -50f);
                renderedDicePosition[slot][die] = Vector3.Lerp(renderedDicePosition[slot][die], pose.Position, blend);
                renderedDiceRotation[slot][die] = Quaternion.Slerp(renderedDiceRotation[slot][die], pose.Rotation, blend);
                data.DiceVisuals[die].SetPositionAndRotation(transform.TransformPoint(renderedDicePosition[slot][die]), transform.rotation * renderedDiceRotation[slot][die]);
            }
        }
    }
}
#endif
