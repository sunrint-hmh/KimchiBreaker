using UnityEngine;

/// <summary>Displays the local player's completed kimchi count in a TMP label.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMPro.TMP_Text))]
public sealed class KimchiCounterLabel : MonoBehaviour
{
    [SerializeField] private IngredientClicker player;
    private TMPro.TMP_Text label;

    private void Awake() => label = GetComponent<TMPro.TMP_Text>();

    private void OnEnable()
    {
        if (player == null) return;
        player.KimchiCountChanged += Refresh;
        Refresh(player.KimchiCount);
    }

    private void Start()
    {
        if (player != null) return;
        Debug.LogWarning("[Kimjang] Assign Main Camera's IngredientClicker to the counter's Player field.", this);
        label.text = "KIMCHI : --";
    }

    private void OnDisable()
    {
        if (player != null) player.KimchiCountChanged -= Refresh;
    }

    private void Refresh(int count) => label.SetText("KIMCHI : {0}", count);
}
