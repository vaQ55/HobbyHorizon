using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VictoryController : MonoBehaviour
{
    [Header("Drag các object victory vào đây (bật khi special gold được pick/destroy)")]
    public GameObject victoryObjectForPicked1;
    public GameObject victoryObjectForPicked2;
    public GameObject victoryObjectForPicked3;

    [Header("Bool riêng cho từng object (Inspector)")]
    [Tooltip("Nếu true -> object for picked1 sẽ được bật (được ưu tiên hơn trạng thái từ event).")]
    public bool victoryFlagForPicked1 = false;
    [Tooltip("Nếu true -> object for picked2 sẽ được bật (được ưu tiên hơn trạng thái từ event).")]
    public bool victoryFlagForPicked2 = false;
    [Tooltip("Nếu true -> object for picked3 sẽ được bật (được ưu tiên hơn trạng thái từ event).")]
    public bool victoryFlagForPicked3 = false;

    [Header("Tuỳ chọn kiểm tra liên tục")]
    public bool continuousCheck = false;
    public bool checkEveryFrame = false;
    public float checkInterval = 0.2f;
    public bool autoDisableWhenConditionClears = false;

    [Header("Tuỳ chọn khởi tạo")]
    public bool disableAtStartIfActive = true;

    // internal state: lưu những index đã nhận event OnSpecialPicked (pick)
    private HashSet<int> activatedIndices = new HashSet<int>();
    private Coroutine checkerCoroutine;

    private void Awake()
    {
        // nothing
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Start()
    {
        EnsureSubscribed();

        if (disableAtStartIfActive)
        {
            if (victoryObjectForPicked1 != null) victoryObjectForPicked1.SetActive(false);
            if (victoryObjectForPicked2 != null) victoryObjectForPicked2.SetActive(false);
            if (victoryObjectForPicked3 != null) victoryObjectForPicked3.SetActive(false);
        }

        if (continuousCheck)
        {
            if (checkEveryFrame)
            {
                // Update will handle
            }
            else
            {
                checkerCoroutine = StartCoroutine(PeriodicCheckCoroutine());
            }
        }

        // Immediately sync once on start
        DoCheckAndSyncObjects();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (checkerCoroutine != null)
        {
            try { StopCoroutine(checkerCoroutine); } catch { }
            checkerCoroutine = null;
        }
    }

    private void Update()
    {
        if (!continuousCheck) return;
        if (!checkEveryFrame) return;
        DoCheckAndSyncObjects();
    }

    private IEnumerator PeriodicCheckCoroutine()
    {
        while (true)
        {
            DoCheckAndSyncObjects();
            yield return new WaitForSeconds(Mathf.Max(0.01f, checkInterval));
        }
    }

    // ---------- Subscription helpers ----------
    private void EnsureSubscribed()
    {
        if (GoldManager.Instance == null)
        {
            var gm = FindObjectOfType<GoldManager>();
            if (gm != null)
            {
                Debug.Log("[VictoryController] Found GoldManager via FindObjectOfType; assigning Instance? (GoldManager manages its own Instance)");
            }
        }

        if (GoldManager.Instance != null)
        {
            // safe unsubscribe then subscribe to avoid duplicates
            GoldManager.Instance.OnSpecialGoldPicked -= OnSpecialPicked;
            GoldManager.Instance.OnSpecialGoldPicked += OnSpecialPicked;
            Debug.Log("[VictoryController] Subscribed to GoldManager.OnSpecialGoldPicked (C# event).");

            if (GoldManager.Instance.OnSpecialGoldPickedEvent != null)
            {
                GoldManager.Instance.OnSpecialGoldPickedEvent.RemoveListener(OnSpecialPicked);
                GoldManager.Instance.OnSpecialGoldPickedEvent.AddListener(OnSpecialPicked);
                Debug.Log("[VictoryController] Subscribed to GoldManager.OnSpecialGoldPickedEvent (UnityEvent).");
            }
            else
            {
                Debug.LogWarning("[VictoryController] GoldManager.OnSpecialGoldPickedEvent is NULL.");
            }
        }
        else
        {
            Debug.LogWarning("[VictoryController] GoldManager.Instance is null when trying to subscribe. Make sure GoldManager exists in the Play scene.");
        }
    }

    private void Unsubscribe()
    {
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.OnSpecialGoldPicked -= OnSpecialPicked;
            if (GoldManager.Instance.OnSpecialGoldPickedEvent != null)
                GoldManager.Instance.OnSpecialGoldPickedEvent.RemoveListener(OnSpecialPicked);
            Debug.Log("[VictoryController] Unsubscribed from GoldManager events.");
        }
    }

    // ---------- Event handler ----------
    private void OnSpecialPicked(int pickIndex)
    {
        Debug.Log($"[VictoryController] Received OnSpecialPicked event: pickIndex={pickIndex}");
        activatedIndices.Add(pickIndex);
        SetVictoryForPickIndex(pickIndex, true);
    }

    // ---------- Check / Sync ----------
    private void DoCheckAndSyncObjects()
    {
        for (int i = 1; i <= 3; i++)
        {
            bool shouldBeActive = ShouldBeActiveForIndex(i);
            bool currentlyActive = IsVictoryObjectActive(i);
            if (shouldBeActive && !currentlyActive)
            {
                Debug.Log($"[VictoryController] Sync: activating object for index {i}");
                SetVictoryForPickIndex(i, true);
            }
            else if (!shouldBeActive && currentlyActive && autoDisableWhenConditionClears)
            {
                Debug.Log($"[VictoryController] Sync: deactivating object for index {i} (condition cleared)");
                SetVictoryForPickIndex(i, false);
                activatedIndices.Remove(i);
            }
        }
    }

    // Now check (in order of precedence):
    // 1) the inspector bool (victoryFlagForPickedX) if set -> that wins
    // 2) internal event-based activatedIndices (OnSpecialPicked)
    // 3) GoldManager destroyed-flags (pickedXDestroyedFlag) if available
    private bool ShouldBeActiveForIndex(int pickIndex)
    {
        // 1) inspector flags
        if (pickIndex == 1 && victoryFlagForPicked1) return true;
        if (pickIndex == 2 && victoryFlagForPicked2) return true;
        if (pickIndex == 3 && victoryFlagForPicked3) return true;

        // 2) event-based activation (picked)
        if (activatedIndices.Contains(pickIndex)) return true;

        // 3) check GoldManager flags if instance exists
        if (GoldManager.Instance != null)
        {
            if (pickIndex == 1 && GoldManager.Instance.picked1DestroyedFlag) return true;
            if (pickIndex == 2 && GoldManager.Instance.picked2DestroyedFlag) return true;
            if (pickIndex == 3 && GoldManager.Instance.picked3DestroyedFlag) return true;
        }

        return false;
    }

    // ---------- Activation API ----------
    public void SetVictoryForPickIndex(int pickIndex, bool active)
    {
        GameObject go = GetVictoryObjectForIndex(pickIndex);
        if (go == null)
        {
            Debug.LogWarning($"[VictoryController] No victory object assigned for index {pickIndex}");
            return;
        }

        if (go.activeSelf == active)
        {
            Debug.Log($"[VictoryController] Object for index {pickIndex} already active={active}");
            return;
        }

        go.SetActive(active);
        Debug.Log($"[VictoryController] SetActive({active}) on victory object for index {pickIndex} -> {go.name}");

        if (active)
        {
            Animator a = go.GetComponent<Animator>();
            if (a != null)
            {
                try { a.SetTrigger("Victory"); } catch { }
            }
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                try { ps.Play(); } catch { }
            }
        }
        else
        {
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                try { ps.Stop(); } catch { }
            }
        }
    }

    public void ResetVictoryForPickIndex(int pickIndex)
    {
        activatedIndices.Remove(pickIndex);
        // also clear inspector flag if desired (developer can choose)
        if (pickIndex == 1) victoryFlagForPicked1 = false;
        if (pickIndex == 2) victoryFlagForPicked2 = false;
        if (pickIndex == 3) victoryFlagForPicked3 = false;

        SetVictoryForPickIndex(pickIndex, false);
    }

    private GameObject GetVictoryObjectForIndex(int pickIndex)
    {
        switch (pickIndex)
        {
            case 1: return victoryObjectForPicked1;
            case 2: return victoryObjectForPicked2;
            case 3: return victoryObjectForPicked3;
            default: return null;
        }
    }

    private bool IsVictoryObjectActive(int pickIndex)
    {
        GameObject go = GetVictoryObjectForIndex(pickIndex);
        return go != null && go.activeInHierarchy;
    }

    // ---------- Testing helpers ----------
    [ContextMenu("Test Activate 1")]
    public void TestActivate1() => TestActivate(1);
    [ContextMenu("Test Activate 2")]
    public void TestActivate2() => TestActivate(2);
    [ContextMenu("Test Activate 3")]
    public void TestActivate3() => TestActivate(3);

    // Call this from Inspector context menu to simulate event.
    public void TestActivate(int pickIndex)
    {
        Debug.Log($"[VictoryController] TestActivate called for index {pickIndex}");
        OnSpecialPicked(pickIndex);
    }

    // Helper: force sync from external, e.g., call when GoldManager flags changed externally
    public void ForceSyncNow()
    {
        DoCheckAndSyncObjects();
    }
}
