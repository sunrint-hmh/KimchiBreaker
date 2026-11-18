using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class ImportRubberGloves
{
    private const string Folder = "Assets/3d_models/RubberGloves/";
    private const string Output = "Assets/Prefabs/LocalRubberGloveHands.prefab";
    public static object Main()
    {
        // This builder owns only the glove asset and its prefab; scene instances are never edited.
        Texture2D Texture(string file,bool srgb,bool normal=false)
        {
            var importer = AssetImporter.GetAtPath(Folder+file) as TextureImporter;
            if(importer==null)throw new Exception("Missing glove texture "+file);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=srgb;importer.filterMode=FilterMode.Point;
            importer.maxTextureSize=256;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=0;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+file);
        }
        var albedo=Texture("Glove_WornPink_Albedo.png",true);
        var normal=Texture("Glove_WornPink_Normal.png",false,true);
        var smoothness=Texture("Glove_WornPink_MetallicSmoothness.png",false);
        Texture("Glove_WornPink_Roughness.png",false);
        var sleeveAlbedo=Texture("Sleeve_WornGrey_Albedo.png",true);
        var sleeveMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Sleeve_WornGrey.mat");
        if(sleeveMaterial==null){sleeveMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(sleeveMaterial,Folder+"Sleeve_WornGrey.mat");}
        sleeveMaterial.SetTexture("_BaseMap",sleeveAlbedo);sleeveMaterial.SetColor("_BaseColor",Color.white);
        sleeveMaterial.SetFloat("_Metallic",0);sleeveMaterial.SetFloat("_Smoothness",.14f);AssetDatabase.SaveAssetIfDirty(sleeveMaterial);
        string matPath=Folder+"Glove_WornPink.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,matPath);}
        if(material.shader.name!="KimchiBreaker/PSXRubber")
        {
        material.SetTexture("_BaseMap",albedo);material.SetColor("_BaseColor",Color.white);
        material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.45f);material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap",smoothness);material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",1);
        material.SetFloat("_Surface",0);material.SetFloat("_ZWrite",1);material.SetFloat("_Cull",2);
        material.SetOverrideTag("RenderType","Opaque");material.renderQueue=(int)RenderQueue.Geometry;
        }
        AssetDatabase.SaveAssetIfDirty(material);
        foreach(string side in new[]{"Left","Right"})
        {
            var importer=AssetImporter.GetAtPath(Folder+"RubberGlove_"+side+".fbx") as ModelImporter;
            if(importer==null)throw new Exception("Missing glove model "+side);
            importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=true;
            importer.isReadable=true;
            importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
            importer.addCollider=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("LocalRubberGloveHands");root.SetActive(false);root.layer=2;
            SceneManager.MoveGameObjectToScene(root,scene);
            var hands=root.AddComponent<LocalRubberGloveHands>();var settings=new SerializedObject(hands);
            int triangles=0;
            foreach(string side in new[]{"Left","Right"})
            {
                var pivot=new GameObject(side+"Hand").transform;pivot.SetParent(root.transform,false);
                float sign=side=="Left"?-1f:1f;
                pivot.localPosition=new Vector3(sign*.43f,-.22f,.68f);
                pivot.localRotation=Quaternion.Euler(-72,-sign*8,-sign*10);
                // Keep anatomical handedness; FBX conversion already orients the dorsal surface correctly.
                string sourceSide=side;
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"RubberGlove_"+sourceSide+".fbx");
                var visual=UnityEngine.Object.Instantiate(source,pivot,false);visual.name="RubberGloveMesh";
                visual.transform.localRotation=source.transform.localRotation;
                foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=filter.GetComponent<MeshRenderer>();
                    var slots=renderer.sharedMaterials;
                    for(int i=0;i<slots.Length;i++)
                    {
                        string name=slots[i]==null?"":slots[i].name;
                        if(name=="Glove_WornPink")slots[i]=material;
                        else if(name=="Sleeve_WornGrey")slots[i]=sleeveMaterial;
                        else throw new Exception("Unknown glove material: "+name);
                    }
                    renderer.sharedMaterials=slots;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    triangles+=filter.sharedMesh.triangles.Length/3;
                }
                foreach(var t in pivot.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
                settings.FindProperty(side=="Left"?"leftHand":"rightHand").objectReferenceValue=pivot;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Output);
            var asset=new SerializedObject(prefab);asset.FindProperty("m_IsActive").boolValue=true;
            asset.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SavePrefabAsset(prefab);
            return new{prefab=Output,triangles=triangles,textures="256px Point, opaque rubber, metallic zero",sceneChanged=false};
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
