using UnityEngine;

namespace LastBastion
{
    /// <summary>The enemy road: a polyline enemies follow by distance travelled.</summary>
    public class PathRoute
    {
        public readonly Vector2[] Points;
        readonly float[] cumulative;
        public float Length { get; private set; }

        public PathRoute(Vector2[] points)
        {
            Points = points;
            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            Length = cumulative[points.Length - 1];
        }

        int Segment(float d)
        {
            for (int i = 1; i < Points.Length; i++)
                if (d <= cumulative[i]) return i - 1;
            return Points.Length - 2;
        }

        public Vector2 PositionAt(float distance)
        {
            float d = Mathf.Clamp(distance, 0f, Length);
            int s = Segment(d);
            float segLen = cumulative[s + 1] - cumulative[s];
            float t = segLen > 0f ? (d - cumulative[s]) / segLen : 0f;
            return Vector2.Lerp(Points[s], Points[s + 1], t);
        }

        public Vector2 DirectionAt(float distance)
        {
            int s = Segment(Mathf.Clamp(distance, 0f, Length));
            return (Points[s + 1] - Points[s]).normalized;
        }

        public float DistanceTo(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 1; i < Points.Length; i++)
                best = Mathf.Min(best, DistanceToSegment(p, Points[i - 1], Points[i]));
            return best;
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
