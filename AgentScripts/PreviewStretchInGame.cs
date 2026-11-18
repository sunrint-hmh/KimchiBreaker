using System;
using System.Reflection;
using UnityEngine;
public static class PreviewStretchInGame
{
    public static object Main()
    {
        if(!Application.isPlaying)throw new Exception("Temporary Play preview only.");
        var camera=Camera.main;var hands=camera.GetComponentInChildren<LocalRubberGloveHands>();
        hands.enabled=false;
        var meshes=(StretchableGloveMesh[])typeof(LocalRubberGloveHands).GetField("meshes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hands);
        Ray ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
        Vector3 target=Physics.Raycast(ray,out RaycastHit hit,20,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)?hit.point:ray.GetPoint(4);
        meshes[0].Apply(hands.transform.InverseTransformPoint(target)-meshes[0].OriginalContactRig);
        return new{temporaryPreview=true,sceneSaved=false,reach=Vector3.Distance(camera.transform.position,target)};
    }
}
