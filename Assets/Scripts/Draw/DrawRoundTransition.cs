using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Waits for all four results to be revealed, then returns to InGame.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DrawRaffleResolver))]
public sealed class DrawRoundTransition : MonoBehaviour
{
    [SerializeField] private string inGameScenePath = "Assets/Scenes/InGame.unity";
    [SerializeField, Min(0f)] private float resultHoldSeconds = 2f;
    private DrawRaffleResolver resolver;
    private bool transitioning;

    private void Awake() => resolver = GetComponent<DrawRaffleResolver>();
    private void OnEnable()
    {
        resolver.RaffleCompleted += OnRaffleCompleted;
        if (resolver.IsComplete) OnRaffleCompleted();
    }
    private void OnDisable()
    {
        resolver.RaffleCompleted -= OnRaffleCompleted;
        StopAllCoroutines();
        transitioning = false;
    }

    private void OnRaffleCompleted()
    {
        if (transitioning) return;
        // Opening Draw directly is an isolated preview, not a completed match round.
        if (MatchSession.Progress.Phase != MatchPhase.Drawing)
        {
            Debug.Log("[Draw] Standalone draw preview complete; no active round to advance.", this);
            return;
        }
        transitioning = true;
        StartCoroutine(LoadNextRound());
    }

    private IEnumerator LoadNextRound()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, resultHoldSeconds));
        if (!Application.CanStreamedLevelBeLoaded(inGameScenePath))
        {
            transitioning = false;
            Debug.LogError("[Draw] Add InGame to the build scene list: " + inGameScenePath, this);
            yield break;
        }
        if (!MatchSession.Progress.CompleteDraw()) { transitioning = false; yield break; }
        Debug.Log($"[Draw] Starting round {MatchSession.Progress.CurrentRound + 1}/6; skills and tickets retained.", this);
        SceneManager.LoadSceneAsync(inGameScenePath, LoadSceneMode.Single);
    }
}
