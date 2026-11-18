using TMPro;
using UnityEngine;

/// <summary>Local production score, independent of the spendable ticket wallet.</summary>
[DisallowMultipleComponent]
public sealed class KimchiScoreHud : MonoBehaviour
{
    [SerializeField] private IngredientClicker player;
    [SerializeField] private TMP_Text roundLabel;
    [SerializeField] private TMP_Text totalLabel;
    private float nextSearch, nextRefresh;
    private int previousRound = -1;
    private long previousTotal = -1;
    private bool hasPlayer;

    private void OnEnable()
    {
        if (player != null) player.KimchiCountChanged += OnCountChanged;
        Invalidate(); RefreshNow();
    }
    private void OnDisable()
    {
        if (player != null) player.KimchiCountChanged -= OnCountChanged;
    }
    private void Start() => FindLocalPlayer();

    public void Bind(IngredientClicker source)
    {
        if (player != null) player.KimchiCountChanged -= OnCountChanged;
        player = source;
        if (player != null && isActiveAndEnabled) player.KimchiCountChanged += OnCountChanged;
        Invalidate(); RefreshNow();
    }

    private void FindLocalPlayer()
    {
        if (player != null) return;
        IngredientClicker source = null;
        var camera = Camera.main;
        if (camera != null && camera.gameObject.scene == gameObject.scene) source = camera.GetComponent<IngredientClicker>();
        if (source == null)
            foreach (var candidate in FindObjectsByType<IngredientClicker>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene && candidate.isActiveAndEnabled) { source = candidate; break; }
        Bind(source);
    }

    private void OnCountChanged(int _) => RefreshNow();
    private void LateUpdate()
    {
        if (player == null && Time.unscaledTime >= nextSearch)
        { nextSearch = Time.unscaledTime + 1f; FindLocalPlayer(); }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .1f;
        RefreshNow();
    }
    private void Invalidate() { hasPlayer = false; previousRound = -1; previousTotal = -1; }

    public void RefreshNow()
    {
        if (player == null)
        {
            if (hasPlayer || previousRound == -1)
            {
                if (roundLabel != null) roundLabel.text = "이번 라운드  --";
                if (totalLabel != null) totalLabel.text = "누적 김치  --";
                previousRound = -2; hasPlayer = false;
            }
            return;
        }
        var effects = player.GetComponent<PlayerSkillEffects>();
        var score = LiveKimchiScore.Read(MatchSession.Progress, effects == null ? 0 : effects.PlayerId, player.KimchiCount);
        if (roundLabel != null && (!hasPlayer || previousRound != score.RoundCount))
            roundLabel.text = "이번 라운드  " + score.RoundCount.ToString("N0") + "개";
        if (totalLabel != null && (!hasPlayer || previousTotal != score.TotalCount))
            totalLabel.text = "누적 김치  " + score.TotalCount.ToString("N0") + "개";
        previousRound = score.RoundCount; previousTotal = score.TotalCount; hasPlayer = true;
    }
}
