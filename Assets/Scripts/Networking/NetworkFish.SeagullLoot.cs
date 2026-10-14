using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkFish
    {
        bool seagullLoot, seagullFloating;
        Bounds seagullLootBounds;

        public void BeginSeagullLoot(Vector3 incoming)
        {
            if (!IsServerInitialized || !IsSpawned) return;
            seagullLoot = true;
            seagullFloating = false;
            var ball = GetComponent<Cannonball>();
            if (ball != null)
            {
                ball.RefreshVisual();
                ball.AttachToPlatform(null);
                ball.Body.linearVelocity = Vector3.zero;
                ball.Body.angularVelocity = Vector3.zero;
                ball.Body.isKinematic = true;
                ball.Body.useGravity = false;
            }
            seagullLootBounds = LootPlacement.VisualBounds(this);
            BeginKick(incoming);
        }

        float SeagullLootBottom(Quaternion facing)
        {
            var bounds = seagullLootBounds;
            float bottom = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = Vector3.Scale(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)), transform.lossyScale);
                bottom = Mathf.Min(bottom, (facing * corner).y);
            }
            return bottom;
        }

        void FloatSeagullLoot(Vector3 point)
        {
            kicked = airborne = false;
            seagullFloating = true;
            resolvedPlatform = null;
            point.y = OceanSurface.Instance.Height(point) - SeagullLootBottom(transform.rotation) - .06f;
            Place(null, point, transform.rotation);
            var body = GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; body.useGravity = false; body.position = point; }
        }

        bool TickSeagullLootFloat()
        {
            if (!seagullLoot) return false;
            if (!seagullFloating && !kicked)
            {
                var ball = GetComponent<Cannonball>();
                if (ball != null && !ball.Held && !ball.Loaded && ball.PlatformBody == null && OceanSurface.Instance != null
                    && transform.position.y + SeagullLootBottom(transform.rotation) <= OceanSurface.Instance.Height(transform.position))
                    FloatSeagullLoot(transform.position);
            }
            if (!seagullFloating) return false;
            var loose = GetComponent<NetworkLooseCannonball>();
            if (!Available || MonkeyCarried || loose != null && loose.IsHeld)
            {
                seagullFloating = false;
                return false;
            }
            if (OceanSurface.Instance == null) return false;
            FloatSeagullLoot(transform.position);
            return true;
        }

        void FinishSeagullLootLanding(NetworkShip ship)
        {
            if (!seagullLoot) return;
            var ball = GetComponent<Cannonball>();
            if (ball == null) return;
            ball.Body.position = transform.position;
            ball.Body.rotation = transform.rotation;
            if (ship != null) ball.RollOnPlatform(ship.Body);
            else ball.Release(false);
        }
    }
}
