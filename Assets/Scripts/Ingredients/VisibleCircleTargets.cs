using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Collect the nearest visible ingredient at sample points inside the aim circle.</summary>
public static class VisibleCircleTargets
{
    /// <summary>Find an exposed point for an extra strike; walls and other ingredients block it.</summary>
    public static bool TryFindVisiblePoint(Camera camera, KimchiIngredient candidate, float reach,
        LayerMask hitLayers, float hitboxScale, out Vector3 point)
    {
        point = default;
        PhysicsScene physics = camera.gameObject.scene.GetPhysicsScene();
        foreach (var collider in candidate.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger ||
                (hitLayers.value & (1 << collider.gameObject.layer)) == 0) continue;
            Bounds bounds = collider.bounds;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            bool crossesNearPlane = false;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 screen = camera.WorldToScreenPoint(corner);
                if (screen.z < camera.nearClipPlane) { crossesNearPlane = true; continue; }
                min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            if (float.IsInfinity(min.x)) continue;
            Rect viewport = camera.pixelRect;
            if (crossesNearPlane) { min = viewport.min; max = viewport.max; }
            min = Vector2.Max(min, viewport.min); max = Vector2.Min(max, viewport.max);
            if (min.x > max.x || min.y > max.y) continue;
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
            {
                Vector2 sample = new Vector2(Mathf.Lerp(min.x, max.x, (x + .5f) / 7f),
                    Mathf.Lerp(min.y, max.y, (y + .5f) / 7f));
                Ray ray = camera.ScreenPointToRay(sample);
                if (!physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, reach,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<KimchiIngredient>() != candidate) continue;
                if (!ScreenCircleHitTest.Intersects(camera, hit.collider, sample, .01f, hitboxScale)) continue;
                point = hit.point;
                return true;
            }
        }
        return false;
    }

    public static void Collect(Camera camera, Vector2 center, float radius, float reach,
        LayerMask hitLayers, float hitboxScale, Dictionary<KimchiIngredient, Vector3> targets)
    {
        targets.Clear();
        if (radius <= 0f) return;
        PhysicsScene physics = camera.gameObject.scene.GetPhysicsScene();
        // One-pixel spacing at normal aim sizes, bounded sampling cost for large skill circles.
        float step = Mathf.Max(1f, radius / 48f);
        int extent = Mathf.CeilToInt(radius / step);
        for (int y = -extent; y <= extent; y++)
        for (int x = -extent; x <= extent; x++)
        {
            Vector2 offset = new Vector2(x, y) * step;
            if (offset.sqrMagnitude > radius * radius) continue;
            Vector2 sample = center + offset;
            if (!camera.pixelRect.Contains(sample)) continue;
            // Determine occlusion before filtering damage targets, so walls also block clicks.
            Ray ray = camera.ScreenPointToRay(sample);
            if (!physics.Raycast(ray.origin, ray.direction, out RaycastHit hit,
                reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            if ((hitLayers.value & (1 << hit.collider.gameObject.layer)) == 0) continue;
            var ingredient = hit.collider.GetComponentInParent<KimchiIngredient>();
            if (ingredient == null || !ingredient.isActiveAndEnabled || targets.ContainsKey(ingredient)) continue;
            if (!ScreenCircleHitTest.Intersects(camera, hit.collider, sample, 0.01f, hitboxScale)) continue;
            targets.Add(ingredient, hit.point);
        }
    }
}
