using System;
using UnityEditor;
using UnityEngine;

public static class VerifyHandAnatomyFacing
{
    public static object Main()
    {
        const string path="Assets/Prefabs/LocalRubberGloveHands.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        int checks=0;
        void Check(bool condition,string detail){if(!condition)throw new Exception(detail);checks++;}
        try
        {
            foreach(string side in new[]{"Left","Right"})
            {
                var hand=root.transform.Find(side+"Hand");var f=hand.GetComponentInChildren<MeshFilter>();
                var mesh=f.sharedMesh;var v=mesh.vertices;var uv=mesh.uv;var n=mesh.normals;var indices=mesh.GetTriangles(0);
                float dorsal=0,palm=0,dorsalArea=0,palmArea=0,inside=float.NegativeInfinity,outside=float.NegativeInfinity;
                float inward=side=="Left"?1:-1;
                for(int i=0;i<indices.Length;i+=3)
                {
                    int a=indices[i],b=indices[i+1],c=indices[i+2];Vector2 mid=(uv[a]+uv[b]+uv[c])/3;
                    if(mid.y>.5f)continue;
                    Vector3 normal=f.transform.TransformDirection((n[a]+n[b]+n[c])/3).normalized;
                    float area=Vector3.Cross(v[b]-v[a],v[c]-v[a]).magnitude;
                    float facing=Vector3.Dot(normal,-root.transform.forward);
                    if(mid.x<.5f){dorsal+=facing*area;dorsalArea+=area;}else{palm+=facing*area;palmArea+=area;}
                }
                Check(dorsal/dorsalArea>.4f,side+" dorsal surface must face the viewer");
                Check(palm/palmArea<-.4f,side+" palmar surface must face away from the viewer");
                foreach(var vertex in v)
                {
                    Vector3 p=hand.InverseTransformPoint(f.transform.TransformPoint(vertex));
                    if(p.z<.035f || p.z>.15f)continue;
                    inside=Mathf.Max(inside,p.x*inward);outside=Mathf.Max(outside,-p.x*inward);
                }
                Check(inside>outside+.025f,side+" thumb must lie on the inward side");
                Check(Vector3.Dot(hand.forward,root.transform.up)>.85f,side+" fingertips point upward");
                Check(mesh.isReadable,side+" retains stretch support");
            }
            return new{checksPassed=checks,palmDorsalAndThumbOrientationVerified=true,sceneSaved=false};
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
