using System;
using System.Collections.Generic;
using UnityEngine;
namespace CalgaryCycling
{
    // A sampled cubic Hermite curve gives continuous tangents at authored waypoints.
    // Distances are along the centreline. No network or input dependency.
    public sealed class RoutePath
    {
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> lengths = new List<float>();
        public float Length => lengths.Count == 0 ? 0 : lengths[lengths.Count - 1];
        public IReadOnlyList<Vector3> Samples => points;
        public RoutePath(Vector3[] waypoints) : this(waypoints, true) { }
        public RoutePath(Vector3[] waypoints, bool smooth)
        {
            if (waypoints == null || waypoints.Length < 2)
                throw new ArgumentException("Route needs at least two waypoints.");
            var usable = new List<Vector3>(waypoints.Length);
            for (int i = 0; i < waypoints.Length; i++)
            {
                Vector3 p = waypoints[i];
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))
                    throw new ArgumentException("Route contains a non-finite coordinate.");
                if (usable.Count > 0 && Vector3.Distance(p, usable[usable.Count - 1]) < .5f)
                {
                    if (!smooth) continue;
                    throw new ArgumentException("Consecutive route points must be at least 0.5 m apart.");
                }
                usable.Add(p);
            }
            if (usable.Count < 2)
                throw new ArgumentException("Route needs at least two distinct waypoints.");
            points.Add(usable[0]); lengths.Add(0);
            for (int i = 0; i < usable.Count - 1; i++)
            {
                Vector3 a = usable[i], b = usable[i+1];
                Vector3 m0 = i == 0 ? b-a : (b-usable[i-1]) * .5f;
                Vector3 m1 = i+2 == usable.Count ? b-a : (usable[i+2]-a) * .5f;
                int steps = smooth
                    ? Mathf.Max(16, Mathf.CeilToInt(Vector3.Distance(a,b) * 4))
                    : Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a,b) * 2));
                for (int j = 1; j <= steps; j++)
                {
                    float t = (float)j / steps, t2=t*t, t3=t2*t;
                    Vector3 p = smooth
                        ? (2*t3-3*t2+1)*a + (t3-2*t2+t)*m0 + (-2*t3+3*t2)*b + (t3-t2)*m1
                        : Vector3.Lerp(a, b, t);
                    float d=Vector3.Distance(points[points.Count-1],p);
                    if (d < .0001f) continue;
                    lengths.Add(Length+d); points.Add(p);
                }
            }
            if (Length < 2 || Length > 100000)
                throw new ArgumentException("Route must be between 2 metres and 100 kilometres.");
        }
        public Vector3 Position(float distance)
        {
            distance = Mathf.Clamp(distance, 0, Length);
            int lo=0, hi=lengths.Count-1;
            while (hi-lo>1) { int mid=(lo+hi)/2; if(lengths[mid]<distance)lo=mid;else hi=mid; }
            float t=Mathf.InverseLerp(lengths[lo],lengths[hi],distance);
            return Vector3.Lerp(points[lo],points[hi],t);
        }
        public Vector3 Forward(float distance)
        {
            Vector3 v=Position(distance+.6f)-Position(distance-.6f);v.y=0;
            return v.sqrMagnitude < .00001f ? Vector3.forward : v.normalized;
        }
        public Vector3 Right(float distance) => Vector3.Cross(Vector3.up,Forward(distance)).normalized;
        public Vector3 Position(float distance, float offset) => Position(distance)+Right(distance)*offset;
    }
}
