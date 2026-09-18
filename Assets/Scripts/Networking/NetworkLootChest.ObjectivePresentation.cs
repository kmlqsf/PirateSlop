using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class NetworkLootChest
    {
        LineRenderer captureArc;
        Material arcMaterial;
        readonly LineRenderer[] tethers = new LineRenderer[3];
        readonly GameObject[] knots = new GameObject[3];
        float shownProgress, floatVelocity;
        SeaLootState lastPhase;
        bool phaseKnown;

        void CreateObjectiveDetails()
        {
            if (Kind == SeaLootKind.Capture)
            {
                var arc = new GameObject("CaptureProgress");
                arc.transform.SetParent(eventVisual.transform, false);
                captureArc = arc.AddComponent<LineRenderer>();
                arcMaterial = new Material(markerMaterial) { renderQueue = markerMaterial.renderQueue + 1 };
                arcMaterial.SetColor("_BaseColor", Color.white);
                captureArc.sharedMaterial = arcMaterial;
                captureArc.useWorldSpace = false;
                captureArc.loop = false;
                captureArc.widthMultiplier = ring.widthMultiplier;
                captureArc.numCapVertices = 4;
                shownProgress = progress.Value;
            }
            if (Kind != SeaLootKind.Sunken) return;
            for (int i = 0; i < 3; i++)
            {
                knots[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                knots[i].name = "ReleaseTether" + (i + 1);
                knots[i].transform.SetParent(transform, true);
                knots[i].transform.position = TetherPoint(i);
                knots[i].transform.localScale = Vector3.one * .45f;
                knots[i].GetComponent<Renderer>().sharedMaterial = markerMaterial;
                var line = new GameObject("Tether" + (i + 1));
                line.transform.SetParent(eventVisual.transform, false);
                tethers[i] = line.AddComponent<LineRenderer>();
                tethers[i].sharedMaterial = markerMaterial;
                tethers[i].widthMultiplier = .075f;
                tethers[i].positionCount = 3;
                tethers[i].useWorldSpace = true;
            }
        }

        void UpdateObjectivePresentation()
        {
            if (phaseKnown && lastPhase == SeaLootState.Rising && phase.Value == SeaLootState.Ready && IsClientInitialized)
            {
                GameAudio.Play(SoundCue.WaterSplash, transform.position);
                CombatVfx.Splash(transform.position);
            }
            if (phaseKnown && lastPhase == SeaLootState.Locked && phase.Value == SeaLootState.Rising && IsClientInitialized)
            {
                transform.position = anchor.Value;
                GameAudio.Play(SoundCue.UnderwaterBubbles, transform.position);
            }
            phaseKnown = true;
            lastPhase = phase.Value;
            if (captureArc != null)
            {
                shownProgress = contested.Value ? progress.Value : Mathf.MoveTowards(shownProgress, progress.Value, Time.deltaTime / Mathf.Max(1, Catalog.CaptureSeconds) * 2f);
                captureArc.enabled = phase.Value == SeaLootState.Locked && shownProgress > .0001f;
                captureArc.startColor = captureArc.endColor = contested.Value ? new Color(1f, .78f, .12f) : capturing.Value ? new Color(.15f, 1f, .48f) : new Color(1f, .2f, .14f);
                int segments = Mathf.Max(1, Mathf.CeilToInt(shownProgress * 128f));
                captureArc.positionCount = segments + 1;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = Mathf.Min(i / 128f, shownProgress) * Mathf.PI * 2f;
                    captureArc.SetPosition(i, SurfaceVertex(new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * Catalog.CaptureRadius));
                }
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(i, SurfaceVertex(new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * Catalog.CaptureRadius));
                }
            }
            for (int i = 0; i < 3; i++)
            {
                if (tethers[i] == null) continue;
                bool locked = phase.Value == SeaLootState.Locked && (releasedTethers.Value & (1 << i)) == 0;
                tethers[i].enabled = locked;
                knots[i].SetActive(locked);
                if (!locked) continue;
                var end = TetherPoint(i);
                knots[i].transform.position = end;
                tethers[i].SetPosition(0, transform.position + Vector3.up * .3f);
                tethers[i].SetPosition(1, Vector3.Lerp(transform.position, end, .5f) + Vector3.down * .18f);
                tethers[i].SetPosition(2, end);
                Color color = activeTether.Value == i ? new Color(.2f, 1f, .5f) : new Color(1f, .78f, .3f);
                tethers[i].startColor = tethers[i].endColor = color;
            }
        }

        Vector3 SurfaceVertex(Vector3 local)
        {
            var world = eventPoint.Value + local;
            local.y = OceanSurface.Instance != null ? OceanSurface.Instance.Height(world) - eventPoint.Value.y + .18f : .18f;
            return local;
        }

        void FloatChest(Vector3 point)
        {
            var ocean = OceanSurface.Instance;
            float water = ocean != null ? ocean.Height(point) : eventPoint.Value.y;
            float t = ocean != null ? ocean.WaveTime : Time.time;
            float target = water - .12f + Mathf.Sin(t * 1.7f + eventPoint.Value.x) * .035f;
            point.y = Mathf.SmoothDamp(transform.position.y, target, ref floatVelocity, .32f, 10f, Time.deltaTime);
            Vector3 normal = Vector3.up;
            if (ocean != null)
            {
                float x = ocean.Height(point - Vector3.right * .6f) - ocean.Height(point + Vector3.right * .6f);
                float z = ocean.Height(point - Vector3.forward * .6f) - ocean.Height(point + Vector3.forward * .6f);
                normal = new Vector3(x, 1.2f, z).normalized;
            }
            var rotation = Quaternion.FromToRotation(Vector3.up, normal) * facing.Value * Quaternion.Euler(Mathf.Sin(t * 1.2f) * 2f, 0, Mathf.Cos(t * 1.5f) * 2f);
            transform.SetPositionAndRotation(point, Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-4f * Time.deltaTime)));
        }
    }
}
