using UnityEngine;

/// <summary>Small repeating lift and rocking motion around the jar's authored pose.</summary>
[DisallowMultipleComponent]
public sealed class JarRaffleHover : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float cycleSeconds = 1f;
    [SerializeField, Min(0f)] private float liftHeight = .09f;
    [SerializeField, Range(0f, 10f)] private float rockDegrees = 2f;
    [SerializeField, Min(0f)] private float sidewaysDistance = .01f;
    [SerializeField, Range(0f, 1f)] private float phaseOffset;
    [SerializeField] private bool randomizeMotion = true;
    [SerializeField, Min(.1f)] private float votesForStrongEffect = 10f;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 restScale;
    private float phase;
    private float speedMultiplier = 1f;
    private Vector2 driftDirection;
    private float rockDirection = 1f;
    private float pulseAge = 10f;
    private float pulseStrength;
    private float urgency;
    private bool capturedPose;
    public int VoteCount { get; private set; }
    public float VoteStrength => 1f - Mathf.Exp(-2f * VoteCount / Mathf.Max(.1f, votesForStrongEffect));

    private void OnEnable()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        restScale = transform.localScale;
        capturedPose = true;
        var random = new System.Random(System.Guid.NewGuid().GetHashCode());
        float offset = randomizeMotion ? (float)random.NextDouble() : 0f;
        phase = (phaseOffset + offset) * Mathf.PI * 2f;
        speedMultiplier = randomizeMotion ? Mathf.Lerp(.82f, 1.18f, (float)random.NextDouble()) : 1f;
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        driftDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        rockDirection = random.Next(2) == 0 ? -1f : 1f;
        pulseAge = 10f;
    }

    public void SetVoteCount(int count) => VoteCount = Mathf.Max(0, count);
    public void SetUrgency(float value) => urgency = Mathf.Clamp01(value);

    public void PlayVoteImpact()
    {
        pulseAge = 0f;
        pulseStrength = Mathf.Min(1.5f, .65f + VoteStrength * .85f);
    }

    private void LateUpdate()
    {
        float power = VoteStrength;
        phase += Time.deltaTime / Mathf.Max(.1f, cycleSeconds) * Mathf.PI * 2f
            * speedMultiplier * (1f + power * .5f + urgency * .3f);
        pulseAge += Time.deltaTime;
        float wave = Mathf.Sin(phase);
        float envelope = pulseStrength * Mathf.Exp(-pulseAge * 12f);
        float kick = pulseStrength * Mathf.Sin(Mathf.Clamp01(pulseAge / .28f) * Mathf.PI) * .10f;
        float lift = (.5f - .5f * Mathf.Cos(phase)) * liftHeight * (1f + power * 1.1f + urgency * .3f);
        float drift = sidewaysDistance * (1f + power * 1.5f);
        transform.localPosition = restPosition + new Vector3(wave * driftDirection.x * drift,
            lift + kick, Mathf.Sin(phase * .8f) * driftDirection.y * drift);
        float rocking = rockDegrees * (1f + power * 2.2f + urgency * .6f);
        transform.localRotation = restRotation * Quaternion.Euler(wave * rocking * rockDirection,
            Mathf.Sin(phase * .6f) * power * 3f,
            Mathf.Sin(phase * 2f) * rocking * .65f + envelope * Mathf.Sin(pulseAge * 42f) * 4f);
        float squash = envelope * Mathf.Sin(pulseAge * 32f) * .09f;
        transform.localScale = Vector3.Scale(restScale, new Vector3(1f - squash * .5f, 1f + squash, 1f - squash * .5f));
    }

    private void OnDisable()
    {
        if (!capturedPose) return;
        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
        transform.localScale = restScale;
    }
}
