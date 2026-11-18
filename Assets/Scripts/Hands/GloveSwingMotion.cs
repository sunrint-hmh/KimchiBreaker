using UnityEngine;

/// <summary>Deterministic local hand pose, independent of physics, damage and networking.</summary>
public sealed class GloveSwingMotion
{
    private Pose start, rest, peak;
    private float startedAt, thrust, recovery;
    private bool started;
    public void Begin(Pose current, Pose idle, Vector3 cameraLocalTarget, int damage,
        float now, float thrustSeconds, float recoverySeconds)
    {
        start = current; rest = idle; startedAt = now; started = true;
        thrust = Mathf.Max(.01f,thrustSeconds); recovery = Mathf.Max(.01f,recoverySeconds);
        Vector3 direction = cameraLocalTarget.sqrMagnitude < .001f || cameraLocalTarget.z <= 0f
            ? Vector3.forward : cameraLocalTarget.normalized;
        float strength = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1,damage)) - 1f,0f,2f);
        Vector3 destination = direction * (.83f + strength*.025f) + new Vector3(Mathf.Sign(idle.position.x)*.015f,-.09f,0);
        Vector3 travel = Vector3.ClampMagnitude(destination-idle.position,.38f+strength*.025f);
        peak = new Pose(idle.position+travel,
            Quaternion.LookRotation(direction,Vector3.up) * Quaternion.Euler(-14f-strength*2f,0,-Mathf.Sign(idle.position.x)*7f));
    }
    public bool IsActive(float now) => started && now-startedAt < thrust+recovery;
    public Pose Evaluate(float now)
    {
        if (!started) return rest;
        float elapsed = Mathf.Max(0f,now-startedAt);
        if (elapsed < thrust)
        {
            float t = elapsed/thrust; float eased = 1f-Mathf.Pow(1f-t,3f);
            return new Pose(Vector3.Lerp(start.position,peak.position,eased),Quaternion.Slerp(start.rotation,peak.rotation,eased));
        }
        float recoveryT = Mathf.Clamp01((elapsed-thrust)/recovery);
        float smooth = recoveryT*recoveryT*(3f-2f*recoveryT);
        return new Pose(Vector3.Lerp(peak.position,rest.position,smooth),Quaternion.Slerp(peak.rotation,rest.rotation,smooth));
    }
}
