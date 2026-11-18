using System;
using UnityEngine;

/// <summary>Local first-person presentation. Backend can observe SwingStarted; no network calls here.</summary>
[DisallowMultipleComponent]
public sealed class LocalRubberGloveHands : MonoBehaviour
{
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField, Min(.01f)] private float thrustSeconds = .055f;
    [SerializeField, Min(.01f)] private float returnSeconds = .16f;
    [SerializeField, Min(0f)] private float contactHoldSeconds = .025f;
    [SerializeField, Range(0f,.01f)] private float idleBreathAmplitude = .002f;
    private IngredientClicker source;
    private Camera view;
    private readonly GloveStretchMotion[] motions = { new GloveStretchMotion(),new GloveStretchMotion() };
    private readonly StretchableGloveMesh[] meshes = new StretchableGloveMesh[2];
    private readonly Vector3[] targetWorld = new Vector3[2];
    private readonly Vector3[] targetLocalPoint = new Vector3[2];
    private readonly Transform[] targetSurface = new Transform[2];
    private readonly Pose[] rests = new Pose[2];
    private readonly bool[] animated = new bool[2];
    private bool captured;
    private int nextHand;
    public event Action<int,Vector3,int> SwingStarted;
    public Transform LeftHand => leftHand;
    public Transform RightHand => rightHand;

    private void OnEnable()
    {
        if (leftHand == null || rightHand == null) return;
        rests[0] = LocalPose(leftHand); rests[1] = LocalPose(rightHand); captured = true;
        for(int i=0;i<2;i++)
        {
            var hand=i==0?leftHand:rightHand;
            if(meshes[i]==null)meshes[i]=new StretchableGloveMesh(hand.GetComponentInChildren<MeshFilter>(),hand,transform);
            meshes[i].Reset();
        }
        nextHand = 0; animated[0] = animated[1] = false;
        view = GetComponentInParent<Camera>();
        source = GetComponentInParent<IngredientClicker>();
        if (source != null && view != null) source.ClickPerformed += OnClick;
        else Debug.LogWarning("[Hands] Place LocalRubberGloveHands directly under the InGame Main Camera.",this);
    }
    private void OnDisable()
    {
        if (source != null) source.ClickPerformed -= OnClick;
        if (!captured) return;
        if (leftHand != null) Apply(leftHand,rests[0]);
        if (rightHand != null) Apply(rightHand,rests[1]);
        for(int i=0;i<2;i++)meshes[i]?.Reset();
    }
    private void OnClick(Vector3 target,int damage) => StartStretch(target,damage,source.LastAimCollider==null?null:source.LastAimCollider.transform);

    // Useful to preview the asset or connect an external animation presenter without dealing damage.
    public int PlaySwing(Vector3 worldTarget,int damage)
        =>StartStretch(worldTarget,damage,null);

    private int StartStretch(Vector3 worldTarget,int damage,Transform surface)
    {
        if (!captured || !isActiveAndEnabled || view == null) return -1;
        int index = nextHand; nextHand = 1-nextHand;
        Transform hand = index == 0 ? leftHand:rightHand;
        Apply(hand,rests[index]);
        targetWorld[index]=worldTarget;targetSurface[index]=surface;
        if(surface!=null)targetLocalPoint[index]=surface.InverseTransformPoint(worldTarget);
        float snapSpeed=1f+Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1,damage))-1f,0f,1f)*.15f;
        motions[index].Begin(meshes[index].Offset,Time.time,thrustSeconds/snapSpeed,contactHoldSeconds,returnSeconds);
        animated[index] = true;
        SwingStarted?.Invoke(index,worldTarget,damage);
        return index;
    }

    private void LateUpdate()
    {
        if (!captured) return;
        for(int i=0;i<2;i++)
        {
            var hand = i == 0 ? leftHand:rightHand;
            if(hand == null) continue;
            if(animated[i])
            {
                Apply(hand,rests[i]);
                Vector3 target=targetSurface[i]==null?targetWorld[i]:targetSurface[i].TransformPoint(targetLocalPoint[i]);
                Vector3 offset=transform.InverseTransformPoint(target)-meshes[i].OriginalContactRig;
                meshes[i].Apply(motions[i].Evaluate(Time.time,offset));
                if(!motions[i].IsActive(Time.time)){animated[i]=false;meshes[i].Reset();targetSurface[i]=null;}
            }
            else
            {
                var pose = rests[i];
                pose.position.y += Mathf.Sin(Time.time*2.2f+i*.7f)*idleBreathAmplitude;
                Apply(hand,pose);
            }
        }
    }
    public Vector3 GetContactWorld(int index)
    {
        if(index<0 || index>1)throw new ArgumentOutOfRangeException(nameof(index));
        return meshes[index]==null?(index==0?leftHand:rightHand).position:meshes[index].ContactWorld;
    }
    private void OnDestroy(){for(int i=0;i<2;i++)meshes[i]?.Dispose();}
    private static Pose LocalPose(Transform item) => new Pose(item.localPosition,item.localRotation);
    private static void Apply(Transform item,Pose pose) => item.SetLocalPositionAndRotation(pose.position,pose.rotation);
}
