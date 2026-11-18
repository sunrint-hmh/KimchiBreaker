using UnityEngine;

/// <summary>Fast mesh extension, short contact hold, then full retraction.</summary>
public sealed class GloveStretchMotion
{
    private Vector3 initialOffset;
    private float startedAt,thrust,hold,recovery;
    private bool started;
    public void Begin(Vector3 currentOffset,float now,float thrustSeconds,float holdSeconds,float recoverySeconds)
    {
        initialOffset=currentOffset;startedAt=now;started=true;
        thrust=Mathf.Max(.01f,thrustSeconds);hold=Mathf.Max(0,holdSeconds);recovery=Mathf.Max(.01f,recoverySeconds);
    }
    public bool IsActive(float now)=>started && now-startedAt<thrust+hold+recovery;
    public Vector3 Evaluate(float now,Vector3 contactOffset)
    {
        if(!started)return Vector3.zero;
        float elapsed=Mathf.Max(0,now-startedAt);
        if(elapsed<thrust)
        {
            float t=elapsed/thrust;float ease=1-Mathf.Pow(1-t,3);
            return Vector3.Lerp(initialOffset,contactOffset,ease);
        }
        if(elapsed<thrust+hold)return contactOffset;
        float r=Mathf.Clamp01((elapsed-thrust-hold)/recovery);float smooth=r*r*(3-2*r);
        return contactOffset*(1-smooth);
    }
}
