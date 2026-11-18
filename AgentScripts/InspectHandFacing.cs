using System;
using UnityEngine;
using UnityEditor;
public static class InspectHandFacing
{
    public static object Main()
    {
        var h=UnityEngine.Object.FindAnyObjectByType<LocalRubberGloveHands>();
        var cam=h.GetComponentInParent<Camera>();
        object Inspect(Transform hand)
        {
            var f=hand.GetComponentInChildren<MeshFilter>();var mesh=f.sharedMesh;
            var v=mesh.vertices;var uv=mesh.uv;var n=mesh.normals;var triangles=mesh.GetTriangles(0);
            float dorsal=0,palm=0,da=0,pa=0;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                Vector2 mid=(uv[a]+uv[b]+uv[c])/3;
                if(mid.y>.5f)continue;
                Vector3 normal=f.transform.TransformDirection((n[a]+n[b]+n[c])/3).normalized;
                float area=Vector3.Cross(v[b]-v[a],v[c]-v[a]).magnitude;
                float dot=Vector3.Dot(normal,-cam.transform.forward);
                if(mid.x<.5f){dorsal+=dot*area;da+=area;}else{palm+=dot*area;pa+=area;}
            }
            return new {dorsalFacingCamera=dorsal/da,palmFacingCamera=palm/pa};
        }
        return new {left=Inspect(h.LeftHand),right=Inspect(h.RightHand)};
    }
}
