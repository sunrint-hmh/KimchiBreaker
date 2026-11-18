using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class UpdateNaturalGlovePose
{
    public static object Main()
    {
        const string path="Assets/Prefabs/LocalRubberGloveHands.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            int triangles=0;
            foreach(string side in new[]{"Left","Right"})
            {
                var pivot=root.transform.Find(side+"Hand");
                if(pivot==null)throw new Exception("Existing hand pivot missing.");
                float sign=side=="Left"?-1:1;
                pivot.localPosition=new Vector3(sign*.43f,-.22f,.68f);
                pivot.localRotation=Quaternion.Euler(-72,-sign*8,-sign*10);
                string sourceSide=side;
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3d_models/RubberGloves/RubberGlove_"+sourceSide+".fbx");
                var sourceMesh=source.GetComponentInChildren<MeshFilter>();
                var filter=pivot.GetComponentInChildren<MeshFilter>();
                filter.sharedMesh=sourceMesh.sharedMesh;
                filter.transform.localRotation=source.transform.localRotation;
                var materials=sourceMesh.GetComponent<MeshRenderer>().sharedMaterials.Select(m=>
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/3d_models/RubberGloves/"+m.name+".mat")).ToArray();
                if(materials.Any(m=>m==null))throw new Exception("Existing glove material mapping failed.");
                var renderer=filter.GetComponent<MeshRenderer>();renderer.sharedMaterials=materials;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                triangles+=filter.sharedMesh.triangles.Length/3;
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
            return new{prefab=path,triangles,pose="upright relaxed open hands",sceneSaved=false};
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
