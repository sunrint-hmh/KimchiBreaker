using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildResultTowerPrefab
{
    private const string Output = "Assets/Prefabs/MatchResultTowers.prefab";
    public static object Main()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Output) != null)
            throw new Exception("Tower prefab already exists; inspect before overwriting.");
        var oldUI = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MatchResultUI.prefab");
        var jar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SkillJar.prefab");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/NanumGothic/NanumGothic Dynamic SDF.asset");
        if (oldUI == null || jar == null || font == null) throw new Exception("Existing result UI, jar or font missing.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("MatchResultTowers"); root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root,scene);
            Material Material(string path, Color color)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null) return material;
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",.05f);
                AssetDatabase.CreateAsset(material,path); return material;
            }
            var avatarMaterial = Material("Assets/Prefabs/ResultPlaceholderBody.mat",new Color(.52f,.55f,.45f));
            var groundMaterial = Material("Assets/Prefabs/ResultTowerGround.mat",new Color(.14f,.17f,.095f));
            var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ResultCharacterPlaceholder.prefab");
            if (placeholder == null)
            {
                var model = new GameObject("ResultCharacterPlaceholder"); model.transform.SetParent(root.transform,false);
                void Part(string name, PrimitiveType type, Vector3 position, Vector3 scale)
                {
                    var part = GameObject.CreatePrimitive(type); part.name = name;
                    part.transform.SetParent(model.transform,false); part.transform.localPosition = position;
                    part.transform.localScale = scale; part.layer = 30;
                    UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                    part.GetComponent<Renderer>().sharedMaterial = avatarMaterial;
                }
                Part("Body",PrimitiveType.Capsule,new Vector3(0,.72f,0),new Vector3(.4f,.33f,.32f));
                Part("Head",PrimitiveType.Sphere,new Vector3(0,1.2f,0),Vector3.one*.32f);
                Part("LeftLeg",PrimitiveType.Capsule,new Vector3(-.2f,.28f,0),new Vector3(.14f,.23f,.16f));
                Part("RightLeg",PrimitiveType.Capsule,new Vector3(.2f,.28f,0),new Vector3(.14f,.23f,.16f));
                Part("LeftFoot",PrimitiveType.Cube,new Vector3(-.2f,.04f,-.04f),new Vector3(.22f,.08f,.29f));
                Part("RightFoot",PrimitiveType.Cube,new Vector3(.2f,.04f,-.04f),new Vector3(.22f,.08f,.29f));
                Part("LeftArm",PrimitiveType.Capsule,new Vector3(-.31f,.7f,0),new Vector3(.13f,.28f,.13f));
                Part("RightArm",PrimitiveType.Capsule,new Vector3(.31f,.7f,0),new Vector3(.13f,.28f,.13f));
                placeholder = PrefabUtility.SaveAsPrefabAsset(model,"Assets/Prefabs/ResultCharacterPlaceholder.prefab");
                UnityEngine.Object.DestroyImmediate(model);
            }
            var display = root.AddComponent<ResultTowerDisplay>();
            var stage = new GameObject("TowerStage").transform; stage.SetParent(root.transform,false);
            var cameraObject = new GameObject("ResultTowerCamera"); cameraObject.transform.SetParent(root.transform,false);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.orthographicSize = 3.2f; camera.depth = 10;
            camera.transform.localPosition = new Vector3(0,1.8f,-12);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.045f,.025f);
            camera.cullingMask = 1<<30; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            void Light(string name, Vector3 rotation, Color color, float intensity)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform,false);
                var light = go.AddComponent<Light>(); light.type = LightType.Directional;
                light.color = color; light.intensity = intensity; light.cullingMask = 1<<30;
                go.transform.localRotation = Quaternion.Euler(rotation);
            }
            Light("ResultKeyLight",new Vector3(25,-30,0),new Color(1,.9f,.74f),1.8f);
            Light("ResultFillLight",new Vector3(18,145,0),new Color(.6f,.8f,1f),.6f);
            var hud = UnityEngine.Object.Instantiate(oldUI,root.transform,false); hud.name = "ResultHUD";
            var screen = hud.GetComponent<MatchResultScreen>();
            var settings = new SerializedObject(screen);
            settings.FindProperty("rows").arraySize = 0;
            settings.FindProperty("towers").objectReferenceValue = display;
            var title = (TMP_Text)settings.FindProperty("title").objectReferenceValue;
            var summary = (TMP_Text)settings.FindProperty("summary").objectReferenceValue;
            var status = (TMP_Text)settings.FindProperty("status").objectReferenceValue;
            var button = (UnityEngine.UI.Button)settings.FindProperty("playAgainButton").objectReferenceValue;
            void Position(RectTransform rect,Vector2 anchor,Vector2 offset,Vector2 size)
            {
                rect.SetParent(hud.transform,false); rect.anchorMin = rect.anchorMax = anchor;
                rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = offset; rect.sizeDelta = size;
            }
            Position(title.rectTransform,new Vector2(.5f,.94f),Vector2.zero,new Vector2(1200,80));
            Position(summary.rectTransform,new Vector2(.5f,.87f),Vector2.zero,new Vector2(1300,50));
            Position(status.rectTransform,new Vector2(.3f,.045f),Vector2.zero,new Vector2(950,76));
            status.fontSize = 24; status.fontSizeMax = 24;
            Position((RectTransform)button.transform,new Vector2(.83f,.045f),Vector2.zero,new Vector2(300,68));
            ((RectTransform)button.transform.GetChild(0)).sizeDelta = new Vector2(280,60);
            settings.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(hud.transform.Find("ScorePanel").gameObject);
            UnityEngine.Object.DestroyImmediate(hud.transform.Find("Backdrop").gameObject);
            var towerSettings = new SerializedObject(display);
            towerSettings.FindProperty("jarPrefab").objectReferenceValue = jar;
            towerSettings.FindProperty("placeholderCharacterPrefab").objectReferenceValue = placeholder;
            towerSettings.FindProperty("towerCamera").objectReferenceValue = camera;
            towerSettings.FindProperty("stage").objectReferenceValue = stage;
            towerSettings.FindProperty("pedestalMaterial").objectReferenceValue = groundMaterial;
            foreach (string field in new[]{"playerLabels","countLabels"})
            {
                var labels = towerSettings.FindProperty(field); labels.arraySize = 4;
                for (int i=0;i<4;i++)
                {
                    var go = new GameObject(field+"_"+i,typeof(RectTransform)); go.transform.SetParent(hud.transform,false); go.layer=5;
                    var label = go.AddComponent<TextMeshProUGUI>(); label.font=font;
                    label.fontSize=field=="countLabels" ? 38:28;
                    label.color = new Color(.94f,.94f,.86f); label.alignment=TextAlignmentOptions.Center;
                    label.raycastTarget=false; label.rectTransform.sizeDelta=new Vector2(330,50);
                    go.SetActive(false); labels.GetArrayElementAtIndex(i).objectReferenceValue=label;
                }
            }
            towerSettings.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root,Output);
            var asset = new SerializedObject(prefab); asset.FindProperty("m_IsActive").boolValue=true;
            asset.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SavePrefabAsset(prefab);
            return new { prefab=Output, reusedJar=AssetDatabase.GetAssetPath(jar), rounding="floor", sceneChanged=false };
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
