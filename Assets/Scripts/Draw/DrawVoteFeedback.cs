using UnityEngine;

/// <summary>Vote-driven jar punch, counter pop, camera kick, sparks and synthesized UI audio.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(DrawTicketAllocator), typeof(AudioSource))]
public sealed class DrawVoteFeedback : MonoBehaviour
{
    [Header("Camera impact")]
    [SerializeField, Range(0f, .1f)] private float shakeDistance = .035f;
    [SerializeField, Range(0f, 3f)] private float shakeDegrees = .8f;
    [SerializeField, Range(0f, 5f)] private float zoomDegrees = 2.2f;
    [SerializeField, Range(0f, 1f)] private float soundVolume = .2f;
    [SerializeField] private Material particleMaterial;
    public static float SfxVolume { get; set; } = 1f;

    private DrawTicketAllocator allocator;
    private DrawAllocationCountdown countdown;
    private Camera camera;
    private AudioSource audioSource;
    private AudioClip voteSound;
    private ParticleSystem sparks;
    private Material sparkMaterial;
    private Mesh sparkMesh;
    private System.Random random;
    private readonly JarRaffleHover[] jars = new JarRaffleHover[4];
    private readonly Transform[] labels = new Transform[4];
    private readonly Vector3[] labelScales = new Vector3[4];
    private readonly float[] labelPulseAges = new float[4];
    private Vector3 cameraPosition;
    private Quaternion cameraRotation;
    private float cameraFov;
    private float cameraSize;
    private float cameraEnergy;
    private float noiseSeed;
    private bool capturedPose;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGain() => SfxVolume = 1f;

    private void Awake()
    {
        allocator = GetComponent<DrawTicketAllocator>();
        countdown = GetComponent<DrawAllocationCountdown>();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        random = new System.Random(System.Guid.NewGuid().GetHashCode());
        noiseSeed = (float)random.NextDouble() * 100f;
        voteSound = MakeVoteSound();
        MakeSparks();
    }

    private void OnEnable()
    {
        camera = allocator.DrawCamera;
        if (camera != null)
        {
            cameraPosition = camera.transform.localPosition;
            cameraRotation = camera.transform.localRotation;
            cameraFov = camera.fieldOfView;
            cameraSize = camera.orthographicSize;
            capturedPose = true;
        }
        for (int i = 0; i < 4; i++)
        {
            var collider = allocator.GetJarCollider(i);
            jars[i] = collider == null ? null : collider.GetComponentInParent<JarRaffleHover>();
            var label = allocator.GetAllocationLabel(i);
            labels[i] = label == null ? null : label.transform;
            if (labels[i] != null) labelScales[i] = labels[i].localScale;
            labelPulseAges[i] = 10f;
        }
        allocator.AllocationChanged += SyncVotes;
        allocator.TicketDeposited += OnDeposit;
        allocator.AllocationClosed += OnClosed;
    }

    private void Start() => SyncVotes();

    private void SyncVotes()
    {
        for (int i = 0; i < 4; i++)
            if (jars[i] != null) jars[i].SetVoteCount(allocator.GetAllocation(i));
    }

    private void OnDeposit(int index, int count)
    {
        if (index < 0 || index >= 4 || jars[index] == null) return;
        float strength = jars[index].VoteStrength;
        jars[index].PlayVoteImpact();
        labelPulseAges[index] = 0f;
        cameraEnergy = Mathf.Min(1.2f, cameraEnergy + .45f + strength * .55f);
        EmitSparks(jars[index].transform.position + Vector3.up * .55f, strength, 8 + Mathf.RoundToInt(strength * 10f));
        audioSource.volume = soundVolume * Mathf.Clamp01(SfxVolume);
        audioSource.pitch = .94f + strength * .32f + ((float)random.NextDouble() - .5f) * .08f;
        audioSource.panStereo = -.45f + index * .3f;
        audioSource.PlayOneShot(voteSound, .7f + strength * .3f);
    }

    private void OnClosed()
    {
        // Deadline accent only: this does not announce or award a winner.
        cameraEnergy = Mathf.Max(cameraEnergy, .8f);
        for (int i = 0; i < 4; i++)
            if (jars[i] != null)
            {
                jars[i].PlayVoteImpact();
                EmitSparks(jars[i].transform.position + Vector3.up * .55f, .5f, 8);
            }
    }

    private void Update()
    {
        float urgency = countdown == null ? 0f : Mathf.InverseLerp(4f, 0f, countdown.RemainingSeconds);
        for (int i = 0; i < 4; i++) if (jars[i] != null) jars[i].SetUrgency(urgency);
        audioSource.volume = soundVolume * Mathf.Clamp01(SfxVolume);
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        cameraEnergy = Mathf.MoveTowards(cameraEnergy, 0f, dt * 4.5f);
        if (camera != null && capturedPose)
        {
            float time = Time.unscaledTime * 34f;
            float x = Mathf.PerlinNoise(noiseSeed, time) * 2f - 1f;
            float y = Mathf.PerlinNoise(time, noiseSeed + 17f) * 2f - 1f;
            camera.transform.localPosition = cameraPosition + new Vector3(x, y, 0f) * (shakeDistance * cameraEnergy);
            camera.transform.localRotation = cameraRotation * Quaternion.Euler(
                -cameraEnergy + y * shakeDegrees * cameraEnergy,
                x * shakeDegrees * cameraEnergy * .4f, x * shakeDegrees * cameraEnergy);
            if (camera.orthographic) camera.orthographicSize = cameraSize * (1f - cameraEnergy * .015f);
            else camera.fieldOfView = cameraFov - zoomDegrees * cameraEnergy;
        }
        for (int i = 0; i < 4; i++)
        {
            labelPulseAges[i] += dt;
            if (labels[i] == null) continue;
            float age = labelPulseAges[i];
            float pop = Mathf.Sin(Mathf.Min(age / .24f, 1f) * Mathf.PI) * Mathf.Exp(-age * 3f) * .45f;
            labels[i].localScale = labelScales[i] * (1f + pop);
        }
    }

    private void MakeSparks()
    {
        var go = new GameObject("VoteSparks");
        go.transform.SetParent(transform, false);
        sparks = go.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;
        main.gravityModifier = .7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .5f);
        var emission = sparks.emission; emission.enabled = false;
        var shape = sparks.shape; shape.enabled = false;
        var color = sparks.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .4f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var size = sparks.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        sparkMaterial = particleMaterial != null ? new Material(particleMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        sparkMaterial.name = "DrawVoteSpark_Runtime";
        sparkMesh = new Mesh { name = "DrawVoteDiamond_Runtime" };
        sparkMesh.vertices = new[] { Vector3.up * .5f, Vector3.down * .5f, Vector3.left * .5f,
            Vector3.right * .5f, Vector3.forward * .5f, Vector3.back * .5f };
        sparkMesh.triangles = new[] { 0,4,3, 0,3,5, 0,5,2, 0,2,4, 1,3,4, 1,5,3, 1,2,5, 1,4,2 };
        sparkMesh.RecalculateNormals(); sparkMesh.RecalculateBounds();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = sparkMesh;
        renderer.sharedMaterial = sparkMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sparks.Play();
    }

    private void EmitSparks(Vector3 position, float strength, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var particle = new ParticleSystem.EmitParams {
                position = position,
                velocity = new Vector3(((float)random.NextDouble() - .5f) * 1.7f,
                    1.2f + (float)random.NextDouble() * (1f + strength), ((float)random.NextDouble() - .5f) * .8f),
                startColor = random.Next(3) == 0 ? new Color(.45f, .95f, .08f) : new Color(1f, .25f, .055f),
                startSize = (.018f + (float)random.NextDouble() * .015f) * (1f + strength * .6f),
                startLifetime = .3f + (float)random.NextDouble() * .2f,
                rotation3D = new Vector3(random.Next(360), random.Next(360), random.Next(360))
            };
            sparks.Emit(particle, 1);
        }
    }

    private static AudioClip MakeVoteSound()
    {
        const int rate = 44100;
        int length = Mathf.RoundToInt(rate * .12f);
        var data = new float[length];
        float phase = 0;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)rate;
            phase += Mathf.PI * 2f * Mathf.Lerp(520f, 1100f, t / .12f) / rate;
            float fade = Mathf.Min(1f, t / .005f) * Mathf.Min(1f, (.12f - t) / .005f);
            data[i] = (Mathf.Sin(phase) * .3f + Mathf.Sin(t * Mathf.PI * 240f) * .18f)
                * Mathf.Exp(-t * 27f) * fade;
        }
        var clip = AudioClip.Create("DrawVoteBlip_Runtime", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void OnDisable()
    {
        if (allocator != null)
        {
            allocator.AllocationChanged -= SyncVotes;
            allocator.TicketDeposited -= OnDeposit;
            allocator.AllocationClosed -= OnClosed;
        }
        if (camera != null && capturedPose)
        {
            camera.transform.localPosition = cameraPosition;
            camera.transform.localRotation = cameraRotation;
            camera.fieldOfView = cameraFov;
            camera.orthographicSize = cameraSize;
        }
        for (int i = 0; i < 4; i++) if (labels[i] != null) labels[i].localScale = labelScales[i];
    }

    private void OnDestroy()
    {
        if (voteSound != null) Destroy(voteSound);
        if (sparkMaterial != null) Destroy(sparkMaterial);
        if (sparkMesh != null) Destroy(sparkMesh);
    }
}
