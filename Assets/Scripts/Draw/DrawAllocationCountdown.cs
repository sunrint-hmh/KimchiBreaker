using UnityEngine;

/// <summary>Ten-second allocation window, then a single close event for the raffle sequence.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(DrawTicketAllocator))]
public sealed class DrawAllocationCountdown : MonoBehaviour
{
    [SerializeField, Min(1f)] private float durationSeconds = 10f;
    [SerializeField] private TMPro.TMP_Text timerLabel;

    private DrawTicketAllocator allocator;
    private Color normalColor;
    private int displayedSecond = -1;
    public float RemainingSeconds { get; private set; }
    public bool IsRunning { get; private set; }

    private void Awake() => allocator = GetComponent<DrawTicketAllocator>();

    private void Start()
    {
        RemainingSeconds = Mathf.Max(1f, durationSeconds);
        IsRunning = true;
        if (timerLabel != null) normalColor = timerLabel.color;
        RefreshLabel();
    }

    private void Update()
    {
        if (!IsRunning || !allocator.IsReady) return;
        if (allocator.IsAllocationClosed)
        {
            RemainingSeconds = 0f;
            IsRunning = false;
            RefreshLabel();
            return;
        }
        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
        RefreshLabel();
        if (RemainingSeconds > 0f) return;
        // Runs before allocator.Update, so a click on the expiry frame cannot add tickets.
        IsRunning = false;
        allocator.CloseAllocation();
    }

    private void RefreshLabel()
    {
        int second = Mathf.CeilToInt(RemainingSeconds);
        if (timerLabel == null || second == displayedSecond) return;
        displayedSecond = second;
        timerLabel.text = second == 0 ? "배분 종료" : $"배분  {second}초";
        timerLabel.color = second <= 3 ? new Color(1f, .3f, .22f) : normalColor;
    }
}
