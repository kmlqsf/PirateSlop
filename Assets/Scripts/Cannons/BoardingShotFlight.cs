using PirateSlop.Networking;
using UnityEngine;

namespace PirateSlop
{
    public sealed class BoardingShotFlight : MonoBehaviour
    {
        readonly Transform[] hooks = new Transform[2];
        readonly bool[] stopped = new bool[2];
        readonly Vector3[] points = new Vector3[2];
        readonly LineRenderer[] tow = new LineRenderer[2];
        NetworkCannon network;
        Transform muzzle;
        Transform source;
        Vector3 center, velocity, right;
        LineRenderer tie;
        int cannon, shot;
        bool authoritative;
        float age;

        public void Initialize(SimpleCannon gun, Vector3 position, Vector3 speed, bool authority, int id)
        {
            network = gun.Network; source = gun.GetComponentInParent<ShipController>().transform;
            muzzle = gun.Muzzle;
            cannon = gun.Index; shot = id; authoritative = authority;
            center = position; velocity = speed;
            right = Vector3.Cross(Vector3.up, speed.normalized).normalized;
            if (right.sqrMagnitude < .01f) right = gun.Muzzle.right;
            for (int i = 0; i < 2; i++)
            {
                var model = Instantiate(Resources.Load<GameObject>("BoardingHookVisual"), transform);
                foreach (var shape in model.GetComponentsInChildren<Collider>()) shape.enabled = false;
                hooks[i] = model.transform; points[i] = position + right * (i == 0 ? -.12f : .12f);
                hooks[i].SetPositionAndRotation(points[i], Quaternion.LookRotation(speed));
                var rope = new GameObject("PayingOutRope_" + i); rope.transform.SetParent(transform, false);
                tow[i] = rope.AddComponent<LineRenderer>();
                tow[i].sharedMaterial = Resources.Load<Material>("HookRope");
                tow[i].widthMultiplier = .045f; tow[i].positionCount = 24;
                tow[i].generateLightingData = true; tow[i].numCapVertices = 3;
                for (int p = 0; p < tow[i].positionCount; p++) tow[i].SetPosition(p, muzzle.position);
                rope.AddComponent<RopeTubeVisual>().Line = tow[i];
            }
            tie = gameObject.AddComponent<LineRenderer>();
            tie.sharedMaterial = Resources.Load<Material>("HookRope");
            tie.widthMultiplier = .035f; tie.positionCount = 12;
            tie.generateLightingData = true; tie.numCapVertices = 3;
            for (int p = 0; p < tie.positionCount; p++) tie.SetPosition(p, position);
            gameObject.AddComponent<RopeTubeVisual>().Line = tie;
            Destroy(gameObject, 20f);
        }

        void FixedUpdate()
        {
            if (muzzle == null || !muzzle.gameObject.activeInHierarchy || source == null || !source.gameObject.activeInHierarchy)
            { Destroy(gameObject); return; }
            age += Time.fixedDeltaTime;
            Vector3 nextVelocity = CannonShotDamage.StepVelocity(velocity, Time.fixedDeltaTime);
            center += (velocity + nextVelocity) * (.5f * Time.fixedDeltaTime);
            velocity = nextVelocity;
            float spread = Mathf.Lerp(.12f, .65f, Mathf.SmoothStep(0f, 1f, age / .85f));
            for (int i = 0; i < 2; i++)
            {
                if (stopped[i]) continue;
                Vector3 next = center + right * (i == 0 ? -spread : spread);
                Vector3 delta = next - points[i];
                if (MortarTrajectory.Trace(points[i], delta, .075f, source, out var point, out var normal, out var collider))
                {
                    if (authoritative && network != null && collider != null)
                        network.AttachBoarding(cannon, collider.GetComponentInParent<NetworkShip>(), point, normal, shot, i, collider);
                    CombatVfx.Impact(point, normal, true);
                    stopped[i] = true; hooks[i].gameObject.SetActive(false);
                    tow[i].enabled = false;
                    points[i] = point;
                }
                else
                {
                    points[i] = next;
                    hooks[i].SetPositionAndRotation(next, Quaternion.LookRotation(delta.sqrMagnitude > .001f ? delta : velocity));
                    if (muzzle != null)
                    {
                        Vector3 start = muzzle.position;
                        Vector3 end = next - hooks[i].forward * .64f;
                        for (int p = 0; p < tow[i].positionCount; p++)
                        {
                            float t = p / (tow[i].positionCount - 1f);
                            tow[i].SetPosition(p, Vector3.Lerp(start, end, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * .15f));
                        }
                    }
                }
            }
            tie.enabled = !stopped[0] && !stopped[1];
            if (tie.enabled)
                for (int p = 0; p < tie.positionCount; p++)
                {
                    float t = p / (tie.positionCount - 1f);
                    Vector3 a = points[0] - hooks[0].forward * .64f, b = points[1] - hooks[1].forward * .64f;
                    tie.SetPosition(p, Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * .10f));
                }
            if (stopped[0] && stopped[1]) Destroy(gameObject);
        }
    }
}
