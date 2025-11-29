using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VictoryController : MonoBehaviour
{
    [Header("Drag các object victory vào đây (bật khi special gold được pick/destroy)")]
    public GameObject victoryObjectForPicked1;
    public GameObject victoryObjectForPicked2;
    public GameObject victoryObjectForPicked3;

    [Header("Object sẽ bị tắt tương ứng khi victory object bật lên")]
    public GameObject disableObject1;
    public GameObject disableObject2;
    public GameObject disableObject3;

    [Header("Bool riêng cho từng object (Inspector)")]
    public bool victoryFlagForPicked1 = false;
    public bool victoryFlagForPicked2 = false;
    public bool victoryFlagForPicked3 = false;

    [Header("Tuỳ chọn kiểm tra liên tục")]
    public bool continuousCheck = false;
    public bool checkEveryFrame = false;
    public float checkInterval = 0.2f;
    public bool autoDisableWhenConditionClears = false;

    [Header("Tuỳ chọn khởi tạo")]
    public bool disableAtStartIfActive = true;

    private HashSet<int> activatedIndices = new HashSet<int>();
    private Coroutine checkerCoroutine;

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

        if (continuousCheck && !checkEveryFrame)
            checkerCoroutine = StartCoroutine(PeriodicCheckCoroutine());

        DoCheckAndSyncObjects();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (checkerCoroutine != null)
        {
            StopCoroutine(checkerCoroutine);
            checkerCoroutine = null;
        }
    }

    private void Update()
    {
        if (continuousCheck && checkEveryFrame)
            DoCheckAndSyncObjects();
    }

    private IEnumerator PeriodicCheckCoroutine()
    {
        while (true)
        {
            DoCheckAndSyncObjects();
            yield return new WaitForSeconds(checkInterval);
        }
    }

    // ---------- Subscription ----------
    private void EnsureSubscribed()
    {
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.OnSpecialGoldPicked -= OnSpecialPicked;
            GoldManager.Instance.OnSpecialGoldPicked += OnSpecialPicked;

            if (GoldManager.Instance.OnSpecialGoldPickedEvent != null)
            {
                GoldManager.Instance.OnSpecialGoldPickedEvent.RemoveListener(OnSpecialPicked);
                GoldManager.Instance.OnSpecialGoldPickedEvent.AddListener(OnSpecialPicked);
            }
        }
        else
        {
            Debug.LogWarning("[VictoryController] GoldManager.Instance is null!");
        }
    }

    private void Unsubscribe()
    {
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.OnSpecialGoldPicked -= OnSpecialPicked;

            if (GoldManager.Instance.OnSpecialGoldPickedEvent != null)
                GoldManager.Instance.OnSpecialGoldPickedEvent.RemoveListener(OnSpecialPicked);
        }
    }

    // ---------- Event ----------
    private void OnSpecialPicked(int pickIndex)
    {
        activatedIndices.Add(pickIndex);
        SetVictoryForPickIndex(pickIndex, true);
    }

    // ---------- Sync ----------
    private void DoCheckAndSyncObjects()
    {
        for (int i = 1; i <= 3; i++)
        {
            bool shouldBeActive = ShouldBeActiveForIndex(i);
            bool currentlyActive = IsVictoryObjectActive(i);

            if (shouldBeActive && !currentlyActive)
            {
                SetVictoryForPickIndex(i, true);
            }
            else if (!shouldBeActive && currentlyActive && autoDisableWhenConditionClears)
            {
                SetVictoryForPickIndex(i, false);
                activatedIndices.Remove(i);
            }
        }
    }

    private bool ShouldBeActiveForIndex(int pickIndex)
    {
        if (pickIndex == 1 && victoryFlagForPicked1) return true;
        if (pickIndex == 2 && victoryFlagForPicked2) return true;
        if (pickIndex == 3 && victoryFlagForPicked3) return true;

        if (activatedIndices.Contains(pickIndex)) return true;

        if (GoldManager.Instance != null)
        {
            if (pickIndex == 1 && GoldManager.Instance.picked1DestroyedFlag) return true;
            if (pickIndex == 2 && GoldManager.Instance.picked2DestroyedFlag) return true;
            if (pickIndex == 3 && GoldManager.Instance.picked3DestroyedFlag) return true;
        }

        return false;
    }

    // ---------- Activation ----------
    public void SetVictoryForPickIndex(int pickIndex, bool active)
    {
        GameObject go = GetVictoryObjectForIndex(pickIndex);
        if (go == null) return;

        if (go.activeSelf == active) return;

        go.SetActive(active);

        // 🔥 TẮT OBJECT TƯƠNG ỨNG
        if (active)
        {
            GameObject disableTarget = GetDisableObjectForIndex(pickIndex);
            if (disableTarget != null)
            {
                disableTarget.SetActive(false);
                Debug.Log($"[VictoryController] Disable object {disableTarget.name} because victory {pickIndex} activated.");
            }

            Animator a = go.GetComponent<Animator>();
            if (a != null) a.SetTrigger("Victory");

            ParticleSystem ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Play();
        }
        else
        {
            ParticleSystem ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Stop();
        }
    }

    public void ResetVictoryForPickIndex(int pickIndex)
    {
        activatedIndices.Remove(pickIndex);

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

    private GameObject GetDisableObjectForIndex(int pickIndex)
    {
        switch (pickIndex)
        {
            case 1: return disableObject1;
            case 2: return disableObject2;
            case 3: return disableObject3;
        }
        return null;
    }

    private bool IsVictoryObjectActive(int pickIndex)
    {
        GameObject go = GetVictoryObjectForIndex(pickIndex);
        return go != null && go.activeSelf;
    }

    // ---------- Testing ----------
    [ContextMenu("Test Activate 1")]
    public void TestActivate1() => OnSpecialPicked(1);

    [ContextMenu("Test Activate 2")]
    public void TestActivate2() => OnSpecialPicked(2);

    [ContextMenu("Test Activate 3")]
    public void TestActivate3() => OnSpecialPicked(3);
}
