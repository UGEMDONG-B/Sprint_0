using UnityEngine;

namespace Sprint0.Prototype
{
    /// <summary>Position-based FABRIK solver used by the procedural tentacle.</summary>
    public static class TentacleChainSolver
    {
        public static void Solve(
            Vector3[] points,
            float[] segmentLengths,
            Vector3 anchor,
            Vector3 target,
            int iterations,
            float tolerance)
        {
            if (points == null || segmentLengths == null || points.Length < 2
                || segmentLengths.Length != points.Length - 1)
            {
                return;
            }

            var totalLength = 0f;
            for (var i = 0; i < segmentLengths.Length; i++)
            {
                totalLength += segmentLengths[i];
            }

            if ((target - anchor).sqrMagnitude >= totalLength * totalLength)
            {
                var direction = (target - anchor).normalized;
                points[0] = anchor;
                for (var i = 1; i < points.Length; i++)
                {
                    points[i] = points[i - 1] + direction * segmentLengths[i - 1];
                }

                return;
            }

            var toleranceSquared = tolerance * tolerance;
            for (var iteration = 0; iteration < Mathf.Max(1, iterations); iteration++)
            {
                points[^1] = target;
                for (var i = points.Length - 2; i >= 0; i--)
                {
                    points[i] = KeepDistance(points[i + 1], points[i], segmentLengths[i]);
                }

                points[0] = anchor;
                for (var i = 1; i < points.Length; i++)
                {
                    points[i] = KeepDistance(points[i - 1], points[i], segmentLengths[i - 1]);
                }

                if ((points[^1] - target).sqrMagnitude <= toleranceSquared)
                {
                    break;
                }
            }
        }

        static Vector3 KeepDistance(Vector3 origin, Vector3 point, float distance)
        {
            var offset = point - origin;
            if (offset.sqrMagnitude < 0.0000001f)
            {
                offset = Vector3.forward;
            }

            return origin + offset.normalized * distance;
        }
    }
}
