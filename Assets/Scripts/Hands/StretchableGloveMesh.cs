using System;
using UnityEngine;

/// <summary>Deforms a private mesh copy. Pinned sleeve base, stretched cuff, translating fingertips.</summary>
public sealed class StretchableGloveMesh : IDisposable
{
    private readonly MeshFilter filter;
    private readonly Transform rig;
    private readonly Mesh original,copy;
    private readonly Vector3[] vertices,normals,warped;
    private readonly Vector4[] tangents;
    private readonly float[] weights;
    private readonly Vector3 contactVertex;
    private bool deformed;
    public Vector3 Offset {get;private set;}
    public Vector3 OriginalContactRig=>rig.InverseTransformPoint(filter.transform.TransformPoint(contactVertex));
    public Vector3 ContactWorld=>rig.TransformPoint(OriginalContactRig+Offset);

    public StretchableGloveMesh(MeshFilter meshFilter,Transform hand,Transform cameraRig)
    {
        filter=meshFilter;rig=cameraRig;original=filter.sharedMesh;
        if(original==null || !original.isReadable)throw new InvalidOperationException("Glove FBX needs Read/Write enabled.");
        vertices=original.vertices;normals=original.normals;tangents=original.tangents;
        warped=new Vector3[vertices.Length];weights=new float[vertices.Length];
        Matrix4x4 toHand=hand.worldToLocalMatrix*filter.transform.localToWorldMatrix;
        float min=float.PositiveInfinity,max=float.NegativeInfinity;
        for(int i=0;i<vertices.Length;i++)
        {float z=toHand.MultiplyPoint3x4(vertices[i]).z;min=Mathf.Min(min,z);max=Mathf.Max(max,z);}
        // Use the furthest fingertip as the exact contact anchor.
        int contactIndex=0;float furthest=float.NegativeInfinity;
        for(int i=0;i<vertices.Length;i++)
        {
            float z=toHand.MultiplyPoint3x4(vertices[i]).z;
            if(z>furthest){furthest=z;contactIndex=i;}
            float along=Mathf.InverseLerp(min,max,z);
            // Concentrate the forced elongation in the pink cuff; sleeve end stays fixed.
            float weight=Mathf.InverseLerp(.46f,.80f,along);
            weights[i]=weight*weight*(3-2*weight);
        }
        contactVertex=vertices[contactIndex];
        copy=UnityEngine.Object.Instantiate(original);copy.name=original.name+"_LocalStretch";copy.MarkDynamic();
        filter.sharedMesh=copy;
    }

    public void Apply(Vector3 offset)
    {
        Offset=offset;
        if(offset.sqrMagnitude<.00000001f){Reset();return;}
        Vector3 local=filter.transform.InverseTransformVector(rig.TransformVector(offset));
        for(int i=0;i<vertices.Length;i++)warped[i]=vertices[i]+local*weights[i];
        copy.vertices=warped;copy.RecalculateNormals();copy.RecalculateBounds();copy.RecalculateTangents();deformed=true;
    }
    public void Reset()
    {
        Offset=Vector3.zero;if(!deformed)return;
        copy.vertices=vertices;copy.normals=normals;copy.tangents=tangents;copy.bounds=original.bounds;deformed=false;
    }
    public void Dispose()
    {
        if(filter!=null && filter.sharedMesh==copy)filter.sharedMesh=original;
        if(copy!=null)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(copy);else UnityEngine.Object.DestroyImmediate(copy);}
    }
}
