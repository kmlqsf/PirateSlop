using UnityEngine;

namespace PirateSlop
{
    public sealed class BarricadeBreakAnimation : MonoBehaviour
    {
        Transform[] pieces;
        Vector3[] positions, velocities, scales, spins;
        Quaternion[] rotations;
        float started;
        public static void Play(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 direction, int seed)
        {
            if (prefab == null || Application.isBatchMode) return;
            var root = Instantiate(prefab, position, rotation);
            var effect = root.AddComponent<BarricadeBreakAnimation>();
            var meshes = root.GetComponentsInChildren<MeshFilter>();
            int count = meshes.Length;
            effect.pieces = new Transform[count];
            effect.positions = new Vector3[count]; effect.velocities = new Vector3[count];
            effect.scales = new Vector3[count]; effect.spins = new Vector3[count];
            effect.rotations = new Quaternion[count];
            var random = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                var piece = meshes[i].transform;
                effect.pieces[i] = piece;
                effect.positions[i] = piece.position;
                effect.rotations[i] = piece.rotation;
                effect.scales[i] = piece.localScale;
                Vector3 spread = rotation * new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() * .8f + .4f, (float)random.NextDouble() - .5f);
                effect.velocities[i] = direction * (1.5f + (float)random.NextDouble()) + spread * 2f;
                effect.spins[i] = new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f).normalized * (120f + (float)random.NextDouble() * 180f);
            }
            effect.started = Time.time;
            Destroy(root, 1.8f);
        }
        void Update()
        {
            float age = Time.time - started;
            float shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.45f, 1.8f, age));
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i].SetPositionAndRotation(positions[i] + velocities[i] * age + Vector3.down * (3f * age * age), rotations[i] * Quaternion.Euler(spins[i] * age));
                pieces[i].localScale = scales[i] * shrink;
            }
        }
    }
}
