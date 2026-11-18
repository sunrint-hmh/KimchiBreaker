using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera), typeof(SeatedCameraLook))]
public sealed class IngredientClicker : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float reach = 20f;
    [SerializeField, Min(1)] private int damagePerClick = 1;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private int kimchiCount;
    [SerializeField] private RectTransform aimCircle;
    [Tooltip("Click hitbox size relative to the physics collider; does not change physical collisions.")]
    [SerializeField, Range(0.1f, 1f)] private float hitboxScale = 0.75f;
    // CrosshairRing's transparent center has radius 25 in a 64-pixel image.
    [SerializeField, Range(0.01f, 0.5f)] private float innerRadiusFraction = 25f / 64f;

    private SeatedCameraLook cameraLook;
    private Camera viewCamera;
    private readonly Vector3[] circleCorners = new Vector3[4];
    private readonly System.Collections.Generic.Dictionary<KimchiIngredient, Vector3> visibleTargets =
        new System.Collections.Generic.Dictionary<KimchiIngredient, Vector3>();
    public int KimchiCount => kimchiCount;
    public RectTransform AimCircle => aimCircle;
    public Collider LastAimCollider {get;private set;}
    private readonly System.Random extraTargetRandom = new System.Random();
    public event System.Action<int> KimchiCountChanged;
    // Presentation only: one notification per click, including misses; never per AOE target.
    public event System.Action<Vector3, int> ClickPerformed;

    private void Awake()
    {
        cameraLook = GetComponent<SeatedCameraLook>();
        viewCamera = GetComponent<Camera>();
        kimchiCount = 0;
    }

    private void LateUpdate()
    {
        if (!Application.isFocused || Time.timeScale == 0f || !cameraLook.CanInteract)
            return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return;
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        TryAttackAtAim();
    }

    public void TryAttackAtAim()
    {
        if (!isActiveAndEnabled || Time.timeScale == 0f || aimCircle == null) return;
        Canvas canvas = aimCircle.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        aimCircle.GetWorldCorners(circleCorners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, circleCorners[0]);
        Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, circleCorners[1]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, circleCorners[2]);
        Vector2 center = (bottomLeft + topRight) * 0.5f;
        float radius = Mathf.Min(Vector2.Distance(bottomLeft, topLeft),
            Vector2.Distance(topLeft, topRight)) * innerRadiusFraction;

        // Collect visibility before damage: breaking the front ingredient must not hit the rear
        // ingredient on the same click. Multiple visible ingredients still each receive one hit.
        var effects = GetComponent<PlayerSkillEffects>();
        SkillCombatState skills = effects != null && effects.isActiveAndEnabled ? effects.State : null;
        int damage = skills == null ? damagePerClick : skills.BeginClick(damagePerClick, Time.time, out _);
        Physics.SyncTransforms();
        VisibleCircleTargets.Collect(viewCamera, center, radius, reach, hitLayers, hitboxScale, visibleTargets);
        KimchiIngredient extra = null;
        Vector3 extraPoint = default;
        // Select before direct damage, so destroying an occluder cannot expose a bonus target.
        if (skills != null && skills.AncestorReadyOnNextHit && visibleTargets.Count > 0)
        {
            int candidates = 0;
            foreach (var candidate in FindObjectsByType<KimchiIngredient>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != gameObject.scene || !candidate.isActiveAndEnabled ||
                    visibleTargets.ContainsKey(candidate)) continue;
                if (!VisibleCircleTargets.TryFindVisiblePoint(viewCamera, candidate, reach, hitLayers,
                    hitboxScale, out Vector3 point)) continue;
                if (extraTargetRandom.Next(++candidates) == 0) { extra = candidate; extraPoint = point; }
            }
        }
        Ray aimRay = viewCamera.ScreenPointToRay(center);
        LastAimCollider=null;
        Vector3 visualTarget = aimRay.GetPoint(Mathf.Min(2f, reach));
        if (gameObject.scene.GetPhysicsScene().Raycast(aimRay.origin, aimRay.direction, out RaycastHit aimHit,
            reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {visualTarget = aimHit.point;LastAimCollider=aimHit.collider;}
        ClickPerformed?.Invoke(visualTarget, damage);
        bool successful = false;
        foreach (var target in visibleTargets)
        {
            KimchiIngredient ingredient = target.Key;
            if (ingredient == null || !ingredient.isActiveAndEnabled) continue;
            successful = true;
            ApplyDamageAndAward(ingredient, damage, target.Value);
        }
        if (successful && skills != null && skills.RecordSuccessfulClick() && extra != null && extra.isActiveAndEnabled)
        {
            ApplyDamageAndAward(extra, skills.GetAncestorDamage(damagePerClick), extraPoint);
            skills.ConsumeAncestorStrike();
            Debug.Log("[Skills] 선조의 손맛: 추가 타격", this);
        }
        if (visibleTargets.Count > 0) Debug.Log($"[Kimjang] Circle click hit {visibleTargets.Count} visible ingredient(s).", this);
    }

    private void ApplyDamageAndAward(KimchiIngredient ingredient, int damage, Vector3 point)
    {
        if (!ingredient.ApplyHit(damage, transform.position, point)) return;
        kimchiCount++;
        KimchiCountChanged?.Invoke(kimchiCount);
        Debug.Log($"[Kimjang] Kimchi completed! Total: {kimchiCount}", this);
    }
}
