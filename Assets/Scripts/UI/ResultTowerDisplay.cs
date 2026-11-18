using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>One jar per two completed kimchi. Display-only geometry: no physics or scoring.</summary>
[DisallowMultipleComponent]
public sealed class ResultTowerDisplay : MonoBehaviour
{
    [SerializeField] private GameObject jarPrefab;
    [Tooltip("Optional final character prefabs, indexed by player ID. Empty entries use the placeholder.")]
    [SerializeField] private GameObject[] playerCharacterPrefabs = new GameObject[4];
    [SerializeField] private GameObject placeholderCharacterPrefab;
    [SerializeField] private Camera towerCamera;
    [SerializeField] private Transform stage;
    [SerializeField] private Material pedestalMaterial;
    [SerializeField] private TMP_Text[] playerLabels = new TMP_Text[4];
    [SerializeField] private TMP_Text[] countLabels = new TMP_Text[4];
    [SerializeField, Range(0,31)] private int displayLayer = 30;
    private Transform generated;
    private readonly List<Transform> columns = new List<Transform>();
    private float tallest;
    private float lastAspect = -1;

    public long[] DisplayedJarCounts { get; private set; } = Array.Empty<long>();

    public void Show(MatchResultStandings.Row[] standings, int localPlayerId)
    {
        Clear();
        if (playerLabels != null) foreach (var label in playerLabels) if (label != null) label.gameObject.SetActive(false);
        if (countLabels != null) foreach (var label in countLabels) if (label != null) label.gameObject.SetActive(false);
        if (standings == null || standings.Length == 0) return;
        if (jarPrefab == null || towerCamera == null || stage == null)
        {
            Debug.LogError("[Result] Assign the jar, stage and tower camera.", this);
            return;
        }
        var scores = standings.OrderBy(x => x.PlayerId).ToArray(); // Keep player seats stable, independent of rank.
        if (scores.Length > playerLabels.Length || scores.Length > countLabels.Length)
        {
            Debug.LogError("[Result] Provide a label pair for each participant.", this);
            return;
        }
        var holder = new GameObject("GeneratedResultTowers");
        holder.transform.SetParent(stage, false); holder.SetActive(false);
        generated = holder.transform;
        DisplayedJarCounts = new long[scores.Length];
        Bounds jarBounds = VisualBounds(jarPrefab);
        float jarHeight = Mathf.Max(.01f, jarBounds.size.y);
        tallest = 0;
        for (int i = 0; i < scores.Length; i++)
        {
            var score = scores[i];
            long count = KimchiTowerRules.JarCount(score.KimchiCount);
            DisplayedJarCounts[i] = count;
            var column = new GameObject("PlayerTower_" + score.PlayerId).transform;
            column.SetParent(generated, false); columns.Add(column);
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Ground"; pedestal.transform.SetParent(column, false);
            pedestal.transform.localPosition = new Vector3(0,-.06f,0);
            pedestal.transform.localScale = new Vector3(1.25f,.06f,1.25f);
            if (pedestalMaterial != null) pedestal.GetComponent<Renderer>().sharedMaterial = pedestalMaterial;
            for (long j = 0; j < count; j++)
            {
                var jar = Instantiate(jarPrefab, column, false);
                jar.name = "Jar_" + (j + 1);
                jar.transform.localPosition += new Vector3(-jarBounds.center.x,
                    (float)j * jarHeight - jarBounds.min.y, -jarBounds.center.z);
                DisableGameplay(jar);
            }
            var characterPrefab = score.PlayerId >= 0 && score.PlayerId < playerCharacterPrefabs.Length
                && playerCharacterPrefabs[score.PlayerId] != null ? playerCharacterPrefabs[score.PlayerId] : placeholderCharacterPrefab;
            float towerHeight = (float)count * jarHeight;
            float characterHeight = 0;
            if (characterPrefab != null)
            {
                Bounds characterBounds = VisualBounds(characterPrefab);
                var character = Instantiate(characterPrefab, column, false);
                character.name = "PlayerCharacter";
                character.transform.localPosition += new Vector3(-characterBounds.center.x,
                    towerHeight - characterBounds.min.y, -characterBounds.center.z);
                DisableGameplay(character);
                characterHeight = characterBounds.size.y;
            }
            tallest = Mathf.Max(tallest, towerHeight + characterHeight);
            if (playerLabels[i] != null)
            {
                playerLabels[i].gameObject.SetActive(true);
                string name = score.PlayerId == localPlayerId ? "나" : "플레이어 " + ((long)score.PlayerId + 1);
                playerLabels[i].text = score.Rank + "위 · " + name;
                playerLabels[i].color = score.IsWinner ? new Color(.7f,1f,.24f) : new Color(.94f,.94f,.86f);
            }
            if (countLabels[i] != null)
            {
                countLabels[i].gameObject.SetActive(true);
                countLabels[i].text = "김치 " + score.KimchiCount.ToString("N0") + "개";
            }
        }
        foreach (var t in generated.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = displayLayer;
        DisableGameplay(holder);
        holder.SetActive(true);
        Layout();
    }

    private static void DisableGameplay(GameObject visual)
    {
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
        foreach (var script in visual.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var listener in visual.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        foreach (var audio in visual.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
    }

    // Asset-root rotation/scale are included (FBX jar imports use a rotated root).
    public static Bounds VisualBounds(GameObject prefab)
    {
        bool found = false;
        Bounds bounds = default;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds meshBounds = filter.sharedMesh.bounds;
            foreach (var point in Corners(meshBounds))
            {
                Vector3 position = filter.transform.localToWorldMatrix.MultiplyPoint3x4(point);
                if (!found) { bounds = new Bounds(position, Vector3.zero); found = true; }
                else bounds.Encapsulate(position);
            }
        }
        foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        foreach (var point in Corners(renderer.localBounds))
        {
            Vector3 position = renderer.transform.localToWorldMatrix.MultiplyPoint3x4(point);
            if (!found) { bounds = new Bounds(position,Vector3.zero); found = true; } else bounds.Encapsulate(position);
        }
        if (!found) throw new ArgumentException("No visual meshes in " + prefab.name);
        return bounds;
    }

    private static IEnumerable<Vector3> Corners(Bounds bounds)
    {
        for (int i=0;i<8;i++) yield return bounds.center + Vector3.Scale(bounds.extents,
            new Vector3((i&1)==0 ? -1:1, (i&2)==0 ? -1:1, (i&4)==0 ? -1:1));
    }

    private void LateUpdate()
    {
        if (towerCamera != null && !Mathf.Approximately(lastAspect,towerCamera.aspect)) Layout();
    }

    public void Layout()
    {
        if (towerCamera == null || columns.Count == 0) return;
        float aspect = Mathf.Max(.1f,towerCamera.aspect);
        // Ground at 22% of screen height; character tops stay below the title at 80%.
        float size = Mathf.Max(3.2f, tallest / 1.16f, 3f / aspect);
        towerCamera.orthographic = true; towerCamera.orthographicSize = size;
        towerCamera.transform.localPosition = new Vector3(0,size*.56f,-Mathf.Max(12f,size*2f));
        towerCamera.transform.localRotation = Quaternion.identity;
        towerCamera.farClipPlane = Mathf.Max(100f,size*6f);
        for (int i=0;i<columns.Count;i++)
        {
            float x = columns.Count == 1 ? .5f : Mathf.Lerp(.17f,.83f,(float)i/(columns.Count-1));
            columns[i].localPosition = new Vector3((x-.5f)*2f*size*aspect,0,0);
            if (playerLabels[i] != null) AnchorLabel(playerLabels[i], x, .16f);
            if (countLabels[i] != null) AnchorLabel(countLabels[i], x, .11f);
        }
        lastAspect = aspect;
    }

    private static void AnchorLabel(TMP_Text label,float x,float y)
    {
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(x,y);
        label.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void Clear()
    {
        columns.Clear(); DisplayedJarCounts = Array.Empty<long>();
        if (generated == null) return;
        generated.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(generated.gameObject); else DestroyImmediate(generated.gameObject);
        generated = null;
    }
    private void OnDestroy() => Clear();
}
