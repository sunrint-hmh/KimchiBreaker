using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Final cumulative scores; ticket spending never changes these values.</summary>
[DisallowMultipleComponent]
public sealed class MatchResultScreen : MonoBehaviour
{
    [Serializable] public sealed class ResultRow
    {
        public GameObject root;
        public TMP_Text rank;
        public TMP_Text playerName;
        public TMP_Text kimchiCount;
        public UnityEngine.UI.Image background;
    }

    [SerializeField] private int localPlayerId;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text summary;
    [SerializeField] private TMP_Text status;
    [SerializeField] private ResultRow[] rows = new ResultRow[4];
    [SerializeField] private ResultTowerDisplay towers;
    [SerializeField] private UnityEngine.UI.Button playAgainButton;
    [SerializeField] private TMP_Text playAgainLabel;
    [SerializeField] private string inGameScenePath = "Assets/Scenes/InGame.unity";
    private bool loading;

    private void OnEnable()
    {
        if (playAgainButton != null) playAgainButton.onClick.AddListener(PlayAgain);
        Refresh();
    }
    private void OnDisable()
    {
        if (playAgainButton != null) playAgainButton.onClick.RemoveListener(PlayAgain);
    }
    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Refresh() => RefreshFrom(MatchSession.Progress);

    public void RefreshFrom(MatchProgress match)
    {
        var standings = MatchResultStandings.Build(match);
        if (towers != null) towers.Show(standings, localPlayerId);
        var winners = standings.Where(x => x.IsWinner).ToArray();
        bool localWon = winners.Any(x => x.PlayerId == localPlayerId);
        if (title != null) title.text = standings.Length == 0 ? "게임 결과" :
            winners.Length > 1 ? "공동우승" : localWon ? "우승!" : "게임 종료";
        if (summary != null) summary.text = standings.Length == 0
            ? "완료된 게임 결과가 없습니다."
            : $"{match.TotalRounds}라운드 완료 · 내가 만든 김치 {match.GetTotal(localPlayerId):N0}개";
        if (status != null) status.text = standings.Length == 0
            ? "인게임에서 시작해 모든 라운드를 완료하면 결과가 표시됩니다."
            : "우승: " + string.Join(", ", winners.Select(x => PlayerName(x.PlayerId))) +
                "\n추첨에 사용한 김치도 누적 점수에 포함됩니다.";
        if (rows != null)
        for (int i = 0; i < rows.Length; i++)
        {
            ResultRow row = rows[i];
            if (row == null || row.root == null) continue;
            bool visible = i < standings.Length;
            row.root.SetActive(visible);
            if (!visible) continue;
            var score = standings[i];
            if (row.rank != null) row.rank.text = score.Rank + "위";
            if (row.playerName != null) row.playerName.text = PlayerName(score.PlayerId);
            if (row.kimchiCount != null) row.kimchiCount.text = score.KimchiCount.ToString("N0") + "개";
            if (row.background != null) row.background.color = score.IsWinner
                ? new Color(.19f, .27f, .12f, 1f) : new Color(.105f, .115f, .105f, 1f);
        }
        if (playAgainLabel != null) playAgainLabel.text = standings.Length == 0 ? "게임 시작" : "다시 하기";
        if (playAgainButton != null) playAgainButton.interactable = !loading;
    }

    private string PlayerName(int playerId) => playerId == localPlayerId ? "나" : "플레이어 " + ((long)playerId + 1);

    public void PlayAgain()
    {
        if (loading) return;
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(inGameScenePath);
        if (buildIndex < 0 || !Application.CanStreamedLevelBeLoaded(buildIndex))
        {
            if (status != null) status.text = "Build Profiles의 Scene List에 InGame을 추가해주세요.";
            Debug.LogError("[Result] InGame scene unavailable: " + inGameScenePath, this);
            return;
        }
        loading = true;
        if (playAgainButton != null) playAgainButton.interactable = false;
        MatchSession.StartNewMatch();
        SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
    }
}
