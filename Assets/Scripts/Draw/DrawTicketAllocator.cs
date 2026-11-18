using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Local draw-screen allocation. Winner selection and skill effects are separate.</summary>
[DisallowMultipleComponent]
public sealed class DrawTicketAllocator : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Camera drawCamera;
    [SerializeField] private Collider[] jarColliders = new Collider[4];
    [SerializeField] private TMPro.TMP_Text remainingLabel;
    [SerializeField] private TMPro.TMP_Text statusLabel;
    [SerializeField] private TMPro.TMP_Text[] allocationLabels = new TMPro.TMP_Text[4];

    [Header("Editor testing: only when no round has been completed")]
    [SerializeField] private bool useEditorTestTickets = true;
    [SerializeField, Min(0)] private int editorTestTickets = 10;

    private readonly int[] allocations = new int[4];
    private bool initialized;
    private bool testMode;
    private DrawSkillPresenter skillPresenter;
    public int RemainingTickets { get; private set; }
    public bool IsReady => initialized && isActiveAndEnabled;
    public bool IsAllocationClosed { get; private set; }
    public Camera DrawCamera => drawCamera != null ? drawCamera : Camera.main;
    public TMPro.TMP_Text StatusLabel => statusLabel;
    public event Action AllocationChanged;
    public event Action AllocationClosed;
    public event Action<int, int> TicketDeposited;

    private void Start()
    {
        if (drawCamera == null) drawCamera = Camera.main;
        if (drawCamera == null || jarColliders == null || jarColliders.Length != 4)
        {
            Debug.LogError("[Draw] Assign the Draw Camera and exactly four Jar Colliders.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < 4; i++)
        {
            if (jarColliders[i] == null || !jarColliders[i].enabled)
            {
                Debug.LogError($"[Draw] Assign an enabled collider to Jar Colliders element {i}.", this);
                enabled = false;
                return;
            }
            for (int j = 0; j < i; j++)
                if (jarColliders[i] == jarColliders[j])
                {
                    Debug.LogError("[Draw] Assign each jar only once.", this);
                    enabled = false;
                    return;
                }
        }

        skillPresenter = GetComponent<DrawSkillPresenter>();
        RemainingTickets = RoundSummary.HasResult
            ? MatchTicketWallet.OpenRound(RoundSummary.ResultVersion, RoundSummary.LastRoundKimchiCount)
            : MatchTicketWallet.Balance;
#if UNITY_EDITOR
        if (!RoundSummary.HasResult && useEditorTestTickets)
        {
            RemainingTickets = MatchTicketWallet.OpenEditorTest(editorTestTickets);
            testMode = true;
        }
#endif
        initialized = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Refresh();
        Debug.Log($"[Draw] {(testMode ? "EDITOR TEST" : "Round result")}: {RemainingTickets} tickets. Click a jar to deposit; deposits are final.", this);
    }

    private void Update()
    {
        if (!IsReady || IsAllocationClosed || !Application.isFocused || Time.timeScale == 0f) return;
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        TryClickJarAtScreenPoint(mouse.position.ReadValue());
    }

    public bool TryClickJarAtScreenPoint(Vector2 screenPoint)
    {
        if (!IsReady || IsAllocationClosed) return false;
        Ray ray = drawCamera.ScreenPointToRay(screenPoint);
        // Match picking to the displayed position of hovering mesh colliders.
        Physics.SyncTransforms();
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide)) return false;
        for (int i = 0; i < jarColliders.Length; i++)
        {
            if (hit.collider != jarColliders[i]) continue;
            return TryDeposit(i);
        }
        return false;
    }

    public int GetAllocation(int jarIndex) => jarIndex >= 0 && jarIndex < 4 ? allocations[jarIndex] : 0;
    public Collider GetJarCollider(int index) => jarColliders != null && index >= 0 && index < jarColliders.Length ? jarColliders[index] : null;
    public TMPro.TMP_Text GetAllocationLabel(int index) => allocationLabels != null && index >= 0 && index < allocationLabels.Length ? allocationLabels[index] : null;

    public int[] GetAllocationsSnapshot() => (int[])allocations.Clone();

    public void CloseAllocation()
    {
        if (!IsReady || IsAllocationClosed) return;
        IsAllocationClosed = true;
        Refresh();
        Debug.Log("[Draw] Allocation time ended. Deposits are now locked.", this);
        AllocationClosed?.Invoke();
    }

    public bool TryDeposit(int jarIndex)
    {
        if (!IsReady || IsAllocationClosed || jarIndex < 0 || jarIndex >= 4 || RemainingTickets <= 0) return false;
        if (skillPresenter != null)
        {
            var skill = skillPresenter.GetSkill(jarIndex);
            if (skill == null || !skill.AvailableInDraw)
            {
                if (statusLabel != null) statusLabel.text = "이 항아리는 이번 추첨에서 제외되었어요";
                return false;
            }
        }
        if (!MatchTicketWallet.TrySpendOne()) return false;
        RemainingTickets = MatchTicketWallet.Balance;
        allocations[jarIndex]++;
        Changed(jarIndex);
        TicketDeposited?.Invoke(jarIndex, allocations[jarIndex]);
        return true;
    }

    private void Changed(int jarIndex)
    {
        Refresh();
        AllocationChanged?.Invoke();
        Debug.Log($"[Draw] Jar {jarIndex + 1}: {allocations[jarIndex]}, remaining: {RemainingTickets}{(testMode ? " [TEST]" : "")}", this);
    }

    private void Refresh()
    {
        if (remainingLabel != null)
            remainingLabel.text = $"{(testMode ? "테스트 · " : "")}남은 김치 : {RemainingTickets}";
        if (statusLabel != null)
            statusLabel.text = IsAllocationClosed ? "배분 종료 · 추첨 준비 중"
                : RemainingTickets == 0 ? "남은 김치가 없어요 · 배분 종료를 기다려주세요"
                : "항아리를 클릭해 김치 넣기 · 넣은 김치는 회수할 수 없어요";
        if (allocationLabels == null) return;
        for (int i = 0; i < Mathf.Min(4, allocationLabels.Length); i++)
            if (allocationLabels[i] != null) allocationLabels[i].text = $"투표 : {allocations[i]}";
    }
}
