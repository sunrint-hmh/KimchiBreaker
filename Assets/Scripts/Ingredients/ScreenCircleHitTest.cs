using System.Collections.Generic;
using UnityEngine;

/// <summary>Tests a screen-space circle against the projected collider silhouette.</summary>
public static class ScreenCircleHitTest
{
    // Main-thread scratch space, reused across ingredients to avoid per-collider allocations.
    private static readonly Vector3[] Corners = new Vector3[8];
    private static readonly float[] Depths = new float[8];
    private static readonly List<Vector2> Points = new List<Vector2>(20);
    private static readonly List<Vector2> Hull = new List<Vector2>(40);

    public static bool Intersects(Camera camera, Collider collider, Vector2 center, float radius, float hitboxScale = 1f)
    {
        if (radius <= 0f) return false;
        Points.Clear();
        Hull.Clear();
        var box = collider as BoxCollider;
        Bounds bounds = collider.bounds;
        hitboxScale = Mathf.Clamp(hitboxScale, 0.1f, 1f);
        for (int i = 0; i < 8; i++)
        {
            Vector3 signs = new Vector3((i & 1) == 0 ? -1 : 1,
                (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
            // Preserve the actual oriented box instead of inflating it to its world AABB.
            Corners[i] = box != null
                ? box.transform.TransformPoint(box.center + Vector3.Scale(signs, box.size * (0.5f * hitboxScale)))
                : bounds.center + Vector3.Scale(signs, bounds.extents * hitboxScale);
            Depths[i] = Vector3.Dot(Corners[i] - camera.transform.position, camera.transform.forward);
            if (Depths[i] >= camera.nearClipPlane) AddProjected(camera, Corners[i]);
        }
        // Clip box edges to the near plane before projection, including boxes crossing the camera.
        for (int i = 0; i < 8; i++)
        for (int bit = 0; bit < 3; bit++)
        {
            int j = i ^ (1 << bit);
            if (i >= j || (Depths[i] >= camera.nearClipPlane) == (Depths[j] >= camera.nearClipPlane)) continue;
            float t = (camera.nearClipPlane - Depths[i]) / (Depths[j] - Depths[i]);
            AddProjected(camera, Vector3.Lerp(Corners[i], Corners[j], t));
        }
        if (Points.Count < 3) return false;
        Points.Sort(ComparePoints);
        foreach (Vector2 p in Points)
        {
            while (Hull.Count >= 2 && Cross(Hull[Hull.Count - 2], Hull[Hull.Count - 1], p) <= 0f)
                Hull.RemoveAt(Hull.Count - 1);
            Hull.Add(p);
        }
        int lowerCount = Hull.Count;
        for (int i = Points.Count - 2; i >= 0; i--)
        {
            Vector2 p = Points[i];
            while (Hull.Count > lowerCount && Cross(Hull[Hull.Count - 2], Hull[Hull.Count - 1], p) <= 0f)
                Hull.RemoveAt(Hull.Count - 1);
            Hull.Add(p);
        }
        if (Hull.Count > 1) Hull.RemoveAt(Hull.Count - 1);
        if (Hull.Count < 3) return false;
        bool inside = true;
        float radiusSquared = radius * radius;
        for (int i = 0; i < Hull.Count; i++)
        {
            Vector2 a = Hull[i], b = Hull[(i + 1) % Hull.Count];
            if (Cross(a, b, center) < 0f) inside = false;
            Vector2 edge = b - a;
            float t = edge.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(center - a, edge) / edge.sqrMagnitude) : 0f;
            if ((center - (a + edge * t)).sqrMagnitude <= radiusSquared) return true;
        }
        return inside;
    }

    private static void AddProjected(Camera camera, Vector3 point)
    {
        Vector3 screen = camera.WorldToScreenPoint(point);
        Points.Add(new Vector2(screen.x, screen.y));
    }
    private static int ComparePoints(Vector2 a, Vector2 b)
    {
        int x = a.x.CompareTo(b.x);
        return x != 0 ? x : a.y.CompareTo(b.y);
    }
    private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
        (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
}
