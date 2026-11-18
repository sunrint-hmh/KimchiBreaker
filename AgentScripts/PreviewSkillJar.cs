using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class PreviewSkillJar
{
    public static string Main()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SkillJar.prefab");
        if (prefab == null) throw new Exception("Jar prefab missing.");
        var preview = new PreviewRenderUtility();
        try
        {
            var jar = UnityEngine.Object.Instantiate(prefab);
            jar.transform.position = Vector3.zero;
            preview.AddSingleGO(jar);
            foreach (var t in jar.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var red = new Material(lit) { color = new Color(.60f, .055f, .018f) };
            var floorMaterial = new Material(lit) { color = new Color(.10f, .12f, .095f) };
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    piece.transform.position = new Vector3(Mathf.Cos(i*2.4f)*.12f, .12f + (i/3)*.13f, Mathf.Sin(i*2.4f)*.12f);
                    piece.transform.localScale = new Vector3(.13f, .19f, .10f);
                    piece.transform.rotation = Quaternion.Euler(i*9, i*63, i*14);
                    piece.GetComponent<Renderer>().sharedMaterial = red;
                    preview.AddSingleGO(piece);
                }
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.localScale = new Vector3(200, .02f, 200);
                floor.transform.position = new Vector3(0, -.015f, 0);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                preview.AddSingleGO(floor);
                preview.camera.transform.position = new Vector3(1.35f, 1.45f, -2.35f);
                preview.camera.transform.LookAt(new Vector3(0, .41f, 0));
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = .66f;
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = 100;
                preview.camera.cullingMask = ~0;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.16f, .19f, .14f);
                preview.lights[0].intensity = 1.7f;
                preview.lights[0].color = new Color(1, .91f, .76f);
                preview.lights[0].transform.rotation = Quaternion.Euler(42, -35, 0);
                preview.lights[1].intensity = 1.1f;
                preview.lights[1].color = new Color(.7f, .86f, 1);
                preview.lights[1].transform.rotation = Quaternion.Euler(30, 145, 0);
                preview.ambientColor = new Color(.22f, .24f, .18f);
                preview.BeginPreview(new Rect(0, 0, 900, 900), GUIStyle.none);
                preview.Render(true);
                Texture texture = preview.EndPreview();
                var prior = RenderTexture.active;
                var temporary = RenderTexture.GetTemporary(900, 900, 0, RenderTextureFormat.ARGB32);
                Texture2D output = null;
                try
                {
                    Graphics.Blit(texture, temporary);
                    RenderTexture.active = temporary;
                    output = new Texture2D(900, 900, TextureFormat.RGBA32, false);
                    output.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                    output.Apply();
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Art/SkillJar/UnityPreview.png"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, output.EncodeToPNG());
                    return "URP transparent jar preview saved: " + path;
                }
                finally
                {
                    RenderTexture.active = prior;
                    RenderTexture.ReleaseTemporary(temporary);
                    if (output != null) UnityEngine.Object.DestroyImmediate(output);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(red);
                UnityEngine.Object.DestroyImmediate(floorMaterial);
            }
        }
        finally { preview.Cleanup(); }
    }
}
