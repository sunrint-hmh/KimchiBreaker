using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class RoundController : MonoBehaviour
{
    [SerializeField, Min(1f)] private float durationSeconds = 60f;
    [SerializeField, Min(0f)] private float startCountdownSeconds = 3f;
    [SerializeField] private string drawScenePath = "Assets/Scenes/Draw.unity";
    [SerializeField] private string resultScenePath = "Assets/Scenes/Result.unity";
    [SerializeField] private IngredientSpawner[] spawners;
    [SerializeField] private IngredientClicker player;
    [SerializeField] private SeatedCameraLook cameraLook;
    [SerializeField] private TMPro.TMP_Text timerLabel;

    public float RemainingSeconds { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsCountingDown { get; private set; }
    public float StartRemainingSeconds { get; private set; }
    public int CurrentRound => MatchSession.Progress.CurrentRound;
    public int TotalRounds => MatchSession.Progress.TotalRounds;
    private int lastDisplayedSecond = -1;
    private Color normalColor;
    private int lastStartSecond = -1;
    private bool playerWasEnabled;
    private bool[] spawnersWereEnabled;

    private void Start()
    {
        MatchSession.BeginRound();
        RemainingSeconds = Mathf.Max(1f, durationSeconds);
        if (timerLabel != null) normalColor = timerLabel.color;
        playerWasEnabled = player != null && player.enabled;
        spawnersWereEnabled = new bool[spawners == null ? 0 : spawners.Length];
        for (int i = 0; i < spawnersWereEnabled.Length; i++)
        {
            if (spawners[i] == null) continue;
            spawnersWereEnabled[i] = spawners[i].enabled;
            spawners[i].enabled = false;
        }
        if (player != null) player.enabled = false;
        StartRemainingSeconds = Mathf.Max(0f, startCountdownSeconds);
        IsCountingDown = StartRemainingSeconds > 0f;
        if (IsCountingDown) UpdateStartLabel(); else BeginRunning();
    }

    private void Update()
    {
        if (IsCountingDown)
        {
            StartRemainingSeconds = Mathf.Max(0f, StartRemainingSeconds - Time.deltaTime);
            if (StartRemainingSeconds <= 0f) BeginRunning(); else UpdateStartLabel();
            return; // Preparation never consumes the round's sixty seconds.
        }
        if (!IsRunning) return;
        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
        UpdateLabel();
        if (RemainingSeconds <= 0f) EndRound();
    }

    private void UpdateStartLabel()
    {
        int second = Mathf.CeilToInt(StartRemainingSeconds);
        if (timerLabel == null || second == lastStartSecond) return;
        lastStartSecond = second;
        timerLabel.text = $"ROUND {CurrentRound}/{TotalRounds}  START {second}";
        timerLabel.color = new Color(.72f,1f,.25f);
    }

    private void BeginRunning()
    {
        IsCountingDown = false;
        StartRemainingSeconds = 0f;
        for (int i = 0; i < spawnersWereEnabled.Length; i++)
            if (spawners[i] != null) spawners[i].enabled = spawnersWereEnabled[i];
        if (player != null) player.enabled = playerWasEnabled;
        IsRunning = true;
        lastDisplayedSecond = -1;
        UpdateLabel();
        Debug.Log($"[Kimjang] Round {CurrentRound}/{TotalRounds} started.", this);
    }

    private void UpdateLabel()
    {
        int second = Mathf.CeilToInt(RemainingSeconds);
        if (timerLabel == null || second == lastDisplayedSecond) return;
        lastDisplayedSecond = second;
        timerLabel.text = $"ROUND {CurrentRound}/{TotalRounds}  TIME {second / 60:00}:{second % 60:00}";
        timerLabel.color = second <= 10 ? new Color(1f, 0.3f, 0.22f) : normalColor;
    }

    public void EndRound()
    {
        if (!IsRunning) return;
        IsRunning = false;
        RemainingSeconds = 0f;
        UpdateLabel();
        if (spawners != null)
            foreach (var spawner in spawners)
                if (spawner != null) spawner.enabled = false;
        if (player != null) player.enabled = false;
        if (cameraLook != null) cameraLook.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var effects = player == null ? null : player.GetComponent<PlayerSkillEffects>();
        MatchSession.CompleteLocalRound(effects == null ? 0 : effects.PlayerId,
            player != null ? player.KimchiCount : 0);
        foreach (var ingredient in FindObjectsByType<KimchiIngredient>(FindObjectsSortMode.None))
        {
            if (ingredient.gameObject.scene != gameObject.scene) continue;
            ingredient.gameObject.SetActive(false);
            Destroy(ingredient.gameObject);
        }
        Debug.Log($"[Kimjang] Round ended. Kimchi: {RoundSummary.LastRoundKimchiCount}", this);
        string nextScene = MatchSession.Progress.Phase == MatchPhase.Finished ? resultScenePath : drawScenePath;
        if (!Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError($"[Kimjang] Scene is missing from the build scene list: {nextScene}", this);
            return;
        }
        SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
    }
}
