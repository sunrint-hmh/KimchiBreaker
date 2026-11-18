using UnityEditor;
using UnityEngine;
public static class InspectGloveAxes
{
    public static object Main()
    {
        var p=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3d_models/RubberGloves/RubberGlove_Right.fbx");
        var mesh=p.GetComponentInChildren<MeshFilter>();
        return new {rotation=p.transform.localEulerAngles,bounds=ResultTowerDisplay.VisualBounds(p).size,
            meshBounds=mesh.sharedMesh.bounds.size,childRotation=mesh.transform.localEulerAngles};
    }
}
