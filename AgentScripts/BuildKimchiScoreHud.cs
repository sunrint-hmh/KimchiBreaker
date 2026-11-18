using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildKimchiScoreHud
{
    public static object Main()
    {
        const string path="Assets/Prefabs/KimchiScoreHUD.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)throw new Exception("Score HUD already exists; inspect before replacing.");
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/NanumGothic/NanumGothic Dynamic SDF.asset");
        if(font==null)throw new Exception("Existing Korean font missing.");
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("KimchiScoreHUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
            root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);root.layer=5;
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=20;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var panelObject=new GameObject("ScorePanel",typeof(RectTransform),typeof(UnityEngine.UI.Image));panelObject.layer=5;
            var panel=(RectTransform)panelObject.transform;panel.SetParent(root.transform,false);
            panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.one;panel.anchoredPosition=new Vector2(-28,-28);panel.sizeDelta=new Vector2(430,150);
            var image=panelObject.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.035f,.045f,.025f,.8f);image.raycastTarget=false;
            TMP_Text Text(string name,string value,float y,float size,Color color)
            {
                var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(panel,false);
                var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
                rect.anchoredPosition=new Vector2(18,y);rect.sizeDelta=new Vector2(394,40);
                var text=go.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.color=color;
                text.enableAutoSizing=true;text.fontSizeMin=22;text.fontSizeMax=size;
                text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;text.text=value;return text;
            }
            Text("Heading","만든 김치",-8,25,new Color(.72f,1,.25f));
            var round=Text("RoundKimchi","이번 라운드  --",-49,32,new Color(.95f,.95f,.86f));
            var total=Text("TotalKimchi","누적 김치  --",-94,32,new Color(.95f,.95f,.86f));
            var hud=root.AddComponent<KimchiScoreHud>();var settings=new SerializedObject(hud);
            settings.FindProperty("roundLabel").objectReferenceValue=round;
            settings.FindProperty("totalLabel").objectReferenceValue=total;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);var asset=new SerializedObject(prefab);
            asset.FindProperty("m_IsActive").boolValue=true;asset.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SavePrefabAsset(prefab);
            return new {prefab=path,autoConnects=true,sceneChanged=false};
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
