using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Local prototype resolver; network code can submit other players' final four-jar votes.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DrawTicketAllocator), typeof(DrawSkillPresenter))]
public sealed class DrawRaffleResolver : MonoBehaviour
{
    [SerializeField] private int localPlayerId;
    [SerializeField, Min(0f)] private float revealInterval = .65f;
    private DrawTicketAllocator allocator;
    private DrawSkillPresenter presenter;
    private readonly Dictionary<int, int[]> participants = new Dictionary<int, int[]>();
    private readonly int[][] winners = new int[4][];
    private readonly SkillDefinition[] drawnSkills = new SkillDefinition[4];
    private bool resolved;
    public bool IsComplete { get; private set; }
    public int LocalPlayerId => localPlayerId;
    public event Action RaffleCompleted;

    private void Awake()
    {
        allocator = GetComponent<DrawTicketAllocator>();
        presenter = GetComponent<DrawSkillPresenter>();
    }

    private void OnEnable()
    {
        allocator.AllocationClosed += Resolve;
        if (resolved && !IsComplete) StartCoroutine(Reveal());
        else if (!resolved && allocator.IsReady && allocator.IsAllocationClosed) Resolve();
    }
    private void OnDisable()
    {
        allocator.AllocationClosed -= Resolve;
        StopAllCoroutines();
    }

    // This API does not implement networking or authenticate votes; that remains server work.
    public void SubmitParticipantVotes(int playerId, int[] votes)
    {
        if (resolved) throw new InvalidOperationException("Voting is already resolved.");
        if (playerId == localPlayerId) throw new ArgumentException("Local votes come from DrawTicketAllocator.");
        if (votes == null || votes.Length != 4 || votes.Any(x => x < 0))
            throw new ArgumentException("Provide four nonnegative vote counts.");
        participants[playerId] = (int[])votes.Clone();
    }

    public int[] GetWinners(int jarIndex) => jarIndex >= 0 && jarIndex < 4 && winners[jarIndex] != null
        ? (int[])winners[jarIndex].Clone() : Array.Empty<int>();

    private void Resolve()
    {
        if (resolved) return;
        for (int i = 0; i < 4; i++)
        {
            drawnSkills[i] = presenter.GetSkill(i);
            if (drawnSkills[i] == null)
            {
                if (allocator.StatusLabel != null) allocator.StatusLabel.text = "스킬 데이터를 먼저 연결해주세요";
                Debug.LogError("[Draw] Assign four skill definitions before drawing.", this);
                return;
            }
        }
        resolved = true;
        participants[localPlayerId] = allocator.GetAllocationsSnapshot();
        var random = new System.Random(Guid.NewGuid().GetHashCode());
        for (int jar = 0; jar < 4; jar++)
        {
            var skill = drawnSkills[jar];
            var entries = participants.Select(x => new RaffleEntry(x.Key, x.Value[jar])).ToArray();
            winners[jar] = skill.AvailableInDraw ? WeightedRaffle.Draw(entries, skill.WinnerCount, random) : Array.Empty<int>();
            foreach (int winner in winners[jar]) MatchSkillInventory.Grant(winner, skill);
        }
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        if (allocator.StatusLabel != null) allocator.StatusLabel.text = "추첨 중…";
        int localWins = 0;
        for (int jar = 0; jar < 4; jar++)
        {
            if (revealInterval > 0f) yield return new WaitForSeconds(revealInterval);
            bool localWon = Array.IndexOf(winners[jar], localPlayerId) >= 0;
            if (localWon) localWins++;
            string result;
            if (!drawnSkills[jar].AvailableInDraw) result = "추첨 제외";
            else if (winners[jar].Length == 0) result = "응모 없음";
            else result = "당첨: " + string.Join(", ", winners[jar].Select(x => x == localPlayerId ? "나" : "플레이어 " + (x + 1)));
            presenter.ShowResult(jar, result);
            Debug.Log($"[Draw] {drawnSkills[jar].DisplayName}: {result}", this);
        }
        IsComplete = true;
        if (allocator.StatusLabel != null)
            allocator.StatusLabel.text = $"추첨 완료 · 스킬 {localWins}개 획득 · 남은 김치 {MatchTicketWallet.Balance}개 이월";
        RaffleCompleted?.Invoke();
    }
}
