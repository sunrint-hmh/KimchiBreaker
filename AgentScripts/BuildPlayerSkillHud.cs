using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildPlayerSkillHud
{
    public static object Main()
    {
        const string path="Assets/Prefabs/PlayerSkillHUD.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null) throw new Exception("HUD already exists; inspect before replacing.");
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/NanumGothic/NanumGothic Dynamic SDF.asset");
        if(font==null)throw new Exception("Existing Korean font missing.");
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("PlayerSkillHUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
            root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);root.layer=5;
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=20;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            RectTransform Rect(string name,Transform parent,Vector2 size)
            {
                var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
                var rect=(RectTransform)go.transform;rect.sizeDelta=size;return rect;
            }
            var panel=Rect("SkillPanel",root.transform,new Vector2(470,0));
            panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.zero;panel.anchoredPosition=new Vector2(28,28);
            var image=panel.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.035f,.045f,.025f,.8f);image.raycastTarget=false;
            var layout=panel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding=new RectOffset(18,18,14,14);layout.spacing=10;
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            var fitter=panel.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text Text(string name,Transform parent,string value,float size,float height)
            {
                var rect=Rect(name,parent,new Vector2(430,height));
                var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;
                text.text=value;text.raycastTarget=false;text.alignment=TextAlignmentOptions.MidlineLeft;
                text.color=new Color(.9f,.9f,.8f);
                var element=rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();element.preferredHeight=height;element.minHeight=height;
                return text;
            }
            var title=Text("Heading",panel,"보유 스킬",28,38);title.color=new Color(.72f,1f,.25f);
            var empty=Text("EmptySkills",panel,"아직 획득한 스킬이 없어요",24,38);
            var hud=root.AddComponent<PlayerSkillHud>();var settings=new SerializedObject(hud);
            settings.FindProperty("emptyLabel").objectReferenceValue=empty;
            var rows=settings.FindProperty("rows");rows.arraySize=4;
            for(int i=0;i<4;i++)
            {
                var row=Rect("SkillRow_"+(SkillKind)i,panel,new Vector2(430,65));
                var rowLayout=row.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                rowLayout.childControlHeight=true;rowLayout.childControlWidth=true;rowLayout.childForceExpandHeight=false;
                var name=Text("Name",row,"",25,32);var detail=Text("Detail",row,"",22,31);
                var item=rows.GetArrayElementAtIndex(i);item.FindPropertyRelative("kind").enumValueIndex=i;
                item.FindPropertyRelative("root").objectReferenceValue=row.gameObject;
                item.FindPropertyRelative("nameLabel").objectReferenceValue=name;
                item.FindPropertyRelative("detailLabel").objectReferenceValue=detail;
                row.gameObject.SetActive(false);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
            var asset=new SerializedObject(prefab);asset.FindProperty("m_IsActive").boolValue=true;
            asset.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SavePrefabAsset(prefab);
            return new {prefab=path,autoConnectsToCamera=true,sceneChanged=false};
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
