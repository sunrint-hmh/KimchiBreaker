using System.Collections.Generic;
using UnityEngine;

/// <summary>Local prototype: toss ingredients inward from gaps between occupied seats.</summary>
[DisallowMultipleComponent]
public sealed class IngredientSpawner : MonoBehaviour
{
    [Header("Drag references here")]
    [SerializeField] private KimchiIngredient ingredientPrefab;
    [SerializeField] private Collider matCollider;
    [Tooltip("Occupied seats only (2 to 4). Use fixed seat markers, not rotating cameras.")]
    [SerializeField] private Transform[] playerSeats = new Transform[2];

    [Header("Timing and limits")]
    [SerializeField, Min(0.1f)] private float spawnInterval = 2f;
    [SerializeField, Min(0f)] private float firstSpawnDelay = 1f;
    [SerializeField, Min(1)] private int maxActiveIngredients = 12;
    [SerializeField, Min(1f)] private float ingredientLifetime = 45f;

    [Header("Throw")]
    [SerializeField, Min(0.2f)] private float heightAboveRim = 0.65f;
    [SerializeField, Min(1.05f)] private float outsideSeatRadiusMultiplier = 1.25f;
    [SerializeField, Range(0f, 0.75f)] private float landingRadiusFraction = 0.55f;
    [SerializeField, Min(0.2f)] private float flightTime = 0.85f;
    [SerializeField, Min(0f)] private float spinSpeed = 2f;

    private struct Spawned
    {
        public KimchiIngredient ingredient;
        public float expiresAt;
    }

    private readonly List<Spawned> active = new List<Spawned>();
    private float nextSpawnTime;
    private readonly List<float> seatAngles = new List<float>(4);

    private void OnEnable() => nextSpawnTime = Time.time + firstSpawnDelay;

    private void Start()
    {
        if (ingredientPrefab == null || matCollider == null || !matCollider.enabled || matCollider.isTrigger)
        {
            Debug.LogError("[Kimjang] Spawner needs an ingredient prefab and an enabled, non-trigger mat collider.", this);
            enabled = false;
            return;
        }
        if (playerSeats == null || playerSeats.Length < 2 || playerSeats.Length > 4)
        {
            Debug.LogError("[Kimjang] Set 2 to 4 occupied player seat markers on the spawner.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < playerSeats.Length; i++)
        {
            if (playerSeats[i] == null)
            {
                Debug.LogError("[Kimjang] Every occupied player seat needs a Transform reference.", this);
                enabled = false;
                return;
            }
        }
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        Bounds mat = matCollider.bounds;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var item = active[i];
            if (item.ingredient == null)
            {
                active.RemoveAt(i);
                continue;
            }
            if (Time.time >= item.expiresAt || item.ingredient.transform.position.y < mat.min.y - 8f)
            {
                // Cleanup is not a completion and never awards kimchi.
                Destroy(item.ingredient.gameObject);
                active.RemoveAt(i);
            }
        }

        if (Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
        if (active.Count < maxActiveIngredients) Toss(mat);
    }

    private void Toss(Bounds mat)
    {
        if (!TryChooseGapOrigin(mat, out Vector3 origin)) return;
        Vector2 landingOffset = Random.insideUnitCircle * landingRadiusFraction;
        Vector3 aboveTarget = new Vector3(mat.center.x + landingOffset.x * mat.extents.x,
            mat.max.y + 1f, mat.center.z + landingOffset.y * mat.extents.z);

        // Query this mat only: other ingredients must not move the intended landing surface up.
        if (!matCollider.Raycast(new Ray(aboveTarget, Vector3.down), out RaycastHit floor, mat.size.y + 2f))
            return;
        Vector3 target = floor.point + Vector3.up * 0.12f;
        Quaternion rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up)
            * ingredientPrefab.transform.rotation;
        KimchiIngredient instance = Instantiate(ingredientPrefab, origin, rotation);
        Rigidbody body = instance.GetComponent<Rigidbody>();
        if (body == null || body.isKinematic || !body.useGravity)
        {
            Debug.LogError("[Kimjang] Ingredient prefab needs a dynamic Rigidbody with Use Gravity enabled.", this);
            Destroy(instance.gameObject);
            enabled = false;
            return;
        }
        float time = Mathf.Max(0.2f, flightTime);
        // Initial ballistic aim; damping, collisions and subsequent clicks remain physical.
        body.linearVelocity = (target - origin) / time - 0.5f * Physics.gravity * time;
        body.angularVelocity = Random.onUnitSphere * spinSpeed;
        active.Add(new Spawned { ingredient = instance, expiresAt = Time.time + ingredientLifetime });
        Debug.Log($"[Kimjang] Ingredient thrown. Active: {active.Count}", this);
    }

    private bool TryChooseGapOrigin(Bounds mat, out Vector3 origin)
    {
        origin = default;
        seatAngles.Clear();
        float radius = Mathf.Max(mat.extents.x, mat.extents.z);
        foreach (Transform seat in playerSeats)
        {
            if (seat == null) return false;
            Vector3 offset = seat.position - mat.center;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.01f)
            {
                Debug.LogError("[Kimjang] Seat markers must be around the mat, not at its center.", this);
                enabled = false;
                return false;
            }
            radius = Mathf.Max(radius, offset.magnitude);
            seatAngles.Add(Mathf.Atan2(offset.z, offset.x));
        }
        seatAngles.Sort();
        for (int i = 0; i < seatAngles.Count; i++)
        {
            float separation = Mathf.Repeat(seatAngles[(i + 1) % seatAngles.Count]
                - seatAngles[i], Mathf.PI * 2f);
            if (separation < 0.01f)
            {
                Debug.LogError("[Kimjang] Each seat needs a different position around the mat.", this);
                enabled = false;
                return false;
            }
        }
        // Equal probability per gap; no gap is tied to a particular player's camera.
        int gap = Random.Range(0, seatAngles.Count);
        float from = seatAngles[gap];
        float to = seatAngles[(gap + 1) % seatAngles.Count];
        if (to <= from) to += Mathf.PI * 2f;
        // Keep launches away from the actual player bodies at the two boundaries.
        float angle = Mathf.Lerp(from, to, Random.Range(0.12f, 0.88f));
        radius *= outsideSeatRadiusMultiplier;
        origin = new Vector3(mat.center.x + Mathf.Cos(angle) * radius,
            mat.max.y + heightAboveRim, mat.center.z + Mathf.Sin(angle) * radius);
        return true;
    }
}
