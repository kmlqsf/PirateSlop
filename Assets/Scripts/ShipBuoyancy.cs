using System;
using UnityEngine;
namespace PirateSlop
{
    public sealed class ShipBuoyancy
    {
        static readonly float[] Contour = { 4.74f, 5.84f, 6.12f, 6.26f, 6.51f, 6.40f, 6.63f, 6.42f, 6.56f, 6.33f, 6.23f, 6.26f, 5.84f, 5.67f, 5.06f, 4.48f, 3.57f, 0f };
        readonly Vector3[] offsets = new Vector3[27];
        readonly float[] weights = new float[27];
        readonly float total, meanZ, momentX, momentZ;
        public ShipBuoyancy(float halfLength, float halfWidth, Vector2 footprint)
        {
            float lengthScale = Mathf.Max(.1f, footprint.y / 46f);
            float widthScale = Mathf.Max(.1f, footprint.x / 13f);
            float stern = Mathf.Min(Mathf.Max(.1f, halfLength), 15.809f * lengthScale);
            float bow = Mathf.Min(Mathf.Max(.1f, halfLength), 18.216f * lengthScale);
            float sumZ = 0, sumZZ = 0;
            for (int station = 0; station < 9; station++)
            {
                float z = Mathf.Lerp(-stern, bow, station / 8f);
                float index = Mathf.Clamp01((z + 15.809f * lengthScale) / (34.025f * lengthScale)) * 17f;
                int first = Mathf.Min((int)index, 16);
                float width = Mathf.Min(Mathf.Max(.1f, halfWidth), Mathf.Lerp(Contour[first], Contour[first + 1], index - first) * widthScale);
                for (int side = 0; side < 3; side++)
                {
                    int probe = station * 3 + side;
                    float weight = width * (station == 0 || station == 8 ? .5f : 1f) * (side == 1 ? 4f : 1f);
                    var offset = new Vector3((side - 1) * width, 0, z);
                    offsets[probe] = offset;
                    weights[probe] = weight;
                    total += weight;
                    sumZ += weight * z;
                    sumZZ += weight * z * z;
                    momentX += weight * offset.x * offset.x;
                }
            }
            meanZ = sumZ / Mathf.Max(.001f, total);
            momentZ = Mathf.Max(.001f, sumZZ - total * meanZ * meanZ);
        }
        public Vector3 Evaluate(Vector3 position, float yaw, Func<Vector3, float> height)
        {
            var rotation = Quaternion.Euler(0, yaw, 0);
            float sumHeight = 0, sumXHeight = 0, sumZHeight = 0;
            for (int i = 0; i < offsets.Length; i++)
            {
                if (weights[i] <= 0) continue;
                float water = height(position + rotation * offsets[i]);
                float weighted = weights[i] * water;
                sumHeight += weighted;
                sumXHeight += offsets[i].x * weighted;
                sumZHeight += offsets[i].z * weighted;
            }
            float slopeX = sumXHeight / Mathf.Max(.001f, momentX);
            float slopeZ = (sumZHeight - meanZ * sumHeight) / momentZ;
            float level = sumHeight / Mathf.Max(.001f, total) - slopeZ * meanZ;
            float pitch = -Mathf.Atan(slopeZ);
            float roll = Mathf.Atan(slopeX * Mathf.Cos(pitch));
            return new Vector3(level, pitch * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
        }
    }
}
