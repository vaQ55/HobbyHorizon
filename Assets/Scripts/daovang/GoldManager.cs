using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;

// Giải quyết xung đột Random (UnityEngine.Random vs System.Random)
using Random = UnityEngine.Random;

public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance;

    [Header("Tên scene Play (GoldManager chỉ chạy khi active scene khớp)")]
    public string playSceneName = "Play";

    [Header("Prefab list cho pickedGold1 (random 1 trong list)")]
    public List<GameObject> PrefabPickedGold1 = new List<GameObject>();

    [Header("Prefab cố định cho pickedGold2 / pickedGold3")]
    public GameObject PrefabPickedGold2;
    public GameObject PrefabPickedGold3;

    [Header("Vị trí spawn 3 prefab khi vàng bị phá hủy hoặc nhặt")]
    public Transform spawnPointForFirstGold;
    public Transform spawnPointForSecondGold;
    public Transform spawnPointForThirdGold;

    [Header("Danh sách vàng đang tồn tại (Readonly)")]
    [SerializeField] private List<Gold> goldList = new List<Gold>();

    [Header("Ba cục vàng được random trúng (Readonly)")]
    [SerializeField] private Gold pickedGold1;
    [SerializeField] private Gold pickedGold2;
    [SerializeField] private Gold pickedGold3;

    [Header("Auto pick when scene starts")]
    public bool autoPickOnStart = true;
    public float autoPickDelay = 0.2f;

    [Header("Số lượt (luu lại giữa các phiên)")]
    public int luot1 = 0;
    public int luot2 = 0;

    // NEW: flags that indicate "destroyed" events for picked golds.
    [Header("Flags được bật khi các pickedGold bị destroy (VictoryController có thể đọc)")]
    [Tooltip("True khi pickedGold1 bị destroy (hoặc bị nhặt) — VictoryController có thể bật object tương ứng).")]
    public bool picked1DestroyedFlag = false;
    [Tooltip("True khi pickedGold2 bị destroy (hoặc bị nhặt)")]
    public bool picked2DestroyedFlag = false;
    [Tooltip("True khi pickedGold3 bị destroy (hoặc bị nhặt)")]
    public bool picked3DestroyedFlag = false;

    // NOTE: Không giữ các victoryObject ở đây nữa.
    // VictoryController sẽ quản lý việc bật/tắt các object.

    // NEW: Event thông báo pick special gold (parameter = pickIndex: 1/2/3)
    [Tooltip("Invoked with parameter = pickIndex (1,2,3) when special gold is picked.")]
    public UnityEventInt OnSpecialGoldPickedEvent = new UnityEventInt();

    // C# event để code subscribe nếu cần
    public event Action<int> OnSpecialGoldPicked;

    private Dictionary<int, int> idToPickIndex = new Dictionary<int, int>();
    private int nextID = 1;
    private const string PREF_KEY_LUOT1 = "GoldManager_luot1";
    private const string PREF_KEY_LUOT2 = "GoldManager_luot2";

    // state: chỉ true khi manager đang active trong Play scene
    private bool isRunningInPlay = false;
    private Coroutine autoPickCoroutine = null;

    public IReadOnlyList<Gold> GoldList => goldList;
    public Gold PickedGold1 => pickedGold1;
    public Gold PickedGold2 => pickedGold2;
    public Gold PickedGold3 => pickedGold3;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void Awake()
    {
        UnityEngine.Debug.Log($"[GoldManager] Awake on GameObject '{gameObject.name}' in scene '{gameObject.scene.name}'. PlaySceneName='{playSceneName}'");

        if (SceneManager.GetActiveScene().name != playSceneName)
        {
            UnityEngine.Debug.LogError($"[GoldManager] Created outside Play scene! Stacktrace:\n{new System.Diagnostics.StackTrace(true)}");
            DestroyImmediate(this.gameObject);
            return;
        }

        if (Instance != null && Instance != this)
        {
            UnityEngine.Debug.LogWarning("[GoldManager] Another instance exists — destroying duplicate.");
            DestroyImmediate(this.gameObject);
            return;
        }

        Instance = this;
        isRunningInPlay = true;

        luot1 = PlayerPrefs.GetInt(PREF_KEY_LUOT1, 0);
        luot2 = PlayerPrefs.GetInt(PREF_KEY_LUOT2, 0);
        UnityEngine.Debug.Log($"[GoldManager][AWAKE] Loaded luot1={luot1}, luot2={luot2}");
    }

    private void Start()
    {
        if (!isRunningInPlay) return;

        luot1++;
        luot2++;
        SafeSaveLuot1();
        SafeSaveLuot2();
        UnityEngine.Debug.Log($"[GoldManager][START] Incremented & saved: luot1={luot1}, luot2={luot2}");

        if (autoPickOnStart)
            autoPickCoroutine = StartCoroutine(DelayedAutoPick());
    }

    private void SaveLuot1()
    {
        UnityEngine.Debug.LogError($"[GoldManager] Saving luot1={luot1}. Stacktrace:\n{new System.Diagnostics.StackTrace(true)}");
        PlayerPrefs.SetInt(PREF_KEY_LUOT1, luot1);
        PlayerPrefs.Save();
    }

    private void SaveLuot2()
    {
        UnityEngine.Debug.LogError($"[GoldManager] Saving luot2={luot2}. Stacktrace:\n{new System.Diagnostics.StackTrace(true)}");
        PlayerPrefs.SetInt(PREF_KEY_LUOT2, luot2);
        PlayerPrefs.Save();
    }

    private void SafeSaveLuot1()
    {
        if (!isRunningInPlay)
        {
            UnityEngine.Debug.LogWarning("[GoldManager] Ignored SaveLuot1 because not in Play scene.");
            return;
        }
        SaveLuot1();
    }

    private void SafeSaveLuot2()
    {
        if (!isRunningInPlay)
        {
            UnityEngine.Debug.LogWarning("[GoldManager] Ignored SaveLuot2 because not in Play scene.");
            return;
        }
        SaveLuot2();
    }

    private void OnActiveSceneChanged(Scene prev, Scene next)
    {
        UnityEngine.Debug.Log($"[GoldManager] OnActiveSceneChanged: from '{prev.name}' -> '{next.name}'");
        if (next.name != playSceneName && isRunningInPlay)
        {
            UnityEngine.Debug.Log("[GoldManager] Leaving Play scene -> cleaning up and destroying GoldManager.");
            CleanupAndDestroy();
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        UnityEngine.Debug.Log($"[GoldManager] OnSceneUnloaded: '{scene.name}'");
        if (scene.name == playSceneName && isRunningInPlay)
        {
            UnityEngine.Debug.Log("[GoldManager] Play scene unloaded -> cleaning up and destroying GoldManager.");
            CleanupAndDestroy();
        }
    }

    private void CleanupAndDestroy()
    {
        if (autoPickCoroutine != null)
        {
            try { StopCoroutine(autoPickCoroutine); } catch { }
            autoPickCoroutine = null;
        }
        StopAllCoroutines();

        pickedGold1 = null;
        pickedGold2 = null;
        pickedGold3 = null;
        idToPickIndex.Clear();
        goldList.Clear();

        // reset flags
        picked1DestroyedFlag = false;
        picked2DestroyedFlag = false;
        picked3DestroyedFlag = false;

        isRunningInPlay = false;

        if (Instance == this) Instance = null;

#if UNITY_EDITOR
        DestroyImmediate(this.gameObject);
#else
        Destroy(this.gameObject);
#endif
    }

    private IEnumerator DelayedAutoPick()
    {
        if (!isRunningInPlay) yield break;

        yield return null;
        if (autoPickDelay > 0f)
            yield return new WaitForSeconds(autoPickDelay);

        UnityEngine.Debug.Log($"[GoldManager] DelayedAutoPick running. gold count={goldList.Count}");

        if (goldList.Count >= 1) RandomPickGold1();

        if (goldList.Count >= 2)
        {
            bool doPick2 = false;
            if (luot1 > 3 && luot1 < 6)
            {
                if (Random.value < 0.5f) doPick2 = true;
            }
            else if (luot1 > 6) doPick2 = true;

            if (doPick2) RandomPickGold2();
        }

        if (goldList.Count >= 3)
        {
            bool doPick3 = false;
            if (luot2 > 8 && luot2 < 10)
            {
                if (Random.value < 0.5f) doPick3 = true;
            }
            else if (luot2 > 10) doPick3 = true;

            if (doPick3) RandomPickGold3();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        isRunningInPlay = false;
        StopAllCoroutines();
        UnityEngine.Debug.Log("[GoldManager] OnDestroy executed.");
    }

    public void Register(Gold g)
    {
        if (!isRunningInPlay) return;
        if (g == null) return;

        if (!goldList.Contains(g))
        {
            g.id = nextID++;
            goldList.Add(g);
            UnityEngine.Debug.Log($"[GoldManager] Registered Gold id={g.id} name={g.name}");
        }
    }

    private Gold GetRandomGoldExcluding(HashSet<int> excludeIds)
    {
        if (!isRunningInPlay) return null;
        if (goldList.Count == 0) return null;
        List<Gold> candidates = new List<Gold>();
        foreach (var g in goldList)
        {
            if (g == null) continue;
            if (excludeIds != null && excludeIds.Contains(g.id)) continue;
            candidates.Add(g);
        }
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public void RandomPickGold1()
    {
        if (!isRunningInPlay) return;

        if (pickedGold1 != null && idToPickIndex.ContainsKey(pickedGold1.id))
            idToPickIndex.Remove(pickedGold1.id);

        var exclude = new HashSet<int>();
        if (pickedGold2 != null) exclude.Add(pickedGold2.id);
        if (pickedGold3 != null) exclude.Add(pickedGold3.id);

        Gold chosen = GetRandomGoldExcluding(exclude);
        if (chosen == null)
        {
            UnityEngine.Debug.LogWarning("[GoldManager] RandomPickGold1: Không tìm được gold phù hợp!");
            pickedGold1 = null;
            return;
        }

        pickedGold1 = chosen;
        idToPickIndex[pickedGold1.id] = 1;
        UnityEngine.Debug.Log($"[GoldManager] PickedGold1 = Gold ID {pickedGold1.id} (instanceID={pickedGold1.GetInstanceID()})");
    }

    public void RandomPickGold2()
    {
        if (!isRunningInPlay) return;

        if (pickedGold2 != null && idToPickIndex.ContainsKey(pickedGold2.id))
            idToPickIndex.Remove(pickedGold2.id);

        var exclude = new HashSet<int>();
        if (pickedGold1 != null) exclude.Add(pickedGold1.id);
        if (pickedGold3 != null) exclude.Add(pickedGold3.id);

        Gold chosen = GetRandomGoldExcluding(exclude);

        if (chosen == null)
        {
            if (goldList.Count > 0) chosen = goldList[Random.Range(0, goldList.Count)];
            else chosen = null;
        }

        if (chosen == null)
        {
            UnityEngine.Debug.LogWarning("[GoldManager] RandomPickGold2: Không tìm được gold để pick!");
            pickedGold2 = null;
            return;
        }

        pickedGold2 = chosen;
        idToPickIndex[pickedGold2.id] = 2;
        UnityEngine.Debug.Log($"[GoldManager] PickedGold2 = Gold ID {pickedGold2.id} (instanceID={pickedGold2.GetInstanceID()})");
    }

    public void RandomPickGold3()
    {
        if (!isRunningInPlay) return;

        if (pickedGold3 != null && idToPickIndex.ContainsKey(pickedGold3.id))
            idToPickIndex.Remove(pickedGold3.id);

        var exclude = new HashSet<int>();
        if (pickedGold1 != null) exclude.Add(pickedGold1.id);
        if (pickedGold2 != null) exclude.Add(pickedGold2.id);

        Gold chosen = GetRandomGoldExcluding(exclude);

        if (chosen == null)
        {
            if (goldList.Count > 0) chosen = goldList[Random.Range(0, goldList.Count)];
            else chosen = null;
        }

        if (chosen == null)
        {
            UnityEngine.Debug.LogWarning("[GoldManager] RandomPickGold3: Không tìm được gold để pick!");
            pickedGold3 = null;
            return;
        }

        pickedGold3 = chosen;
        idToPickIndex[pickedGold3.id] = 3;
        UnityEngine.Debug.Log($"[GoldManager] PickedGold3 = Gold ID {pickedGold3.id} (instanceID={pickedGold3.GetInstanceID()})");
    }

    public void OnGoldPicked(Gold gold)
    {
        if (!isRunningInPlay)
        {
            UnityEngine.Debug.LogWarning("[GoldManager] OnGoldPicked called but manager not running in Play. Ignoring.");
            return;
        }

        if (gold == null)
        {
            UnityEngine.Debug.LogWarning("[GoldManager] OnGoldPicked(Gold) called with null.");
            return;
        }

        UnityEngine.Debug.Log($"[GoldManager] OnGoldPicked(Gold) called. instanceID={gold.GetInstanceID()} id={gold.id}");

        if (pickedGold1 == gold || pickedGold2 == gold || pickedGold3 == gold)
        {
            int pickIndex = -1;
            if (pickedGold1 == gold) pickIndex = 1;
            else if (pickedGold2 == gold) pickIndex = 2;
            else if (pickedGold3 == gold) pickIndex = 3;

            if (GamePlayScript.instance != null)
            {
                GamePlayScript.instance.pickGold = true;
                UnityEngine.Debug.Log($"[GoldManager] OnGoldPicked(Gold) -> matched pickedGold reference. pickGold = true (id={gold.id})");
            }

            // FIRE EVENTS (UnityEvent + C# event)
            if (pickIndex != -1)
            {
                try
                {
                    OnSpecialGoldPicked?.Invoke(pickIndex);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[GoldManager] Exception when invoking OnSpecialGoldPicked: {ex}");
                }

                try
                {
                    OnSpecialGoldPickedEvent?.Invoke(pickIndex);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[GoldManager] Exception when invoking OnSpecialGoldPickedEvent: {ex}");
                }

                UnityEngine.Debug.Log($"[GoldManager] OnSpecialGoldPicked invoked for pickIndex={pickIndex}");
            }

            return;
        }

        if (gold.id >= 0)
        {
            OnGoldPicked(gold.id);
        }
        else
        {
            UnityEngine.Debug.Log($"[GoldManager] OnGoldPicked(Gold): no match by reference and id invalid ({gold.id}).");
        }
    }

    public void OnGoldPicked(int id)
    {
        if (!isRunningInPlay)
        {
            UnityEngine.Debug.LogWarning($"[GoldManager] OnGoldPicked(int) called id={id} but manager not running in Play. Ignoring.");
            return;
        }

        UnityEngine.Debug.Log($"[GoldManager] OnGoldPicked(int) called id={id}. idToPickIndex hasKey={idToPickIndex.ContainsKey(id)}");

        if (idToPickIndex.TryGetValue(id, out int pickIndex))
        {
            if (pickIndex == 1 || pickIndex == 2 || pickIndex == 3)
            {
                if (GamePlayScript.instance != null)
                {
                    GamePlayScript.instance.pickGold = true;
                    UnityEngine.Debug.Log($"[GoldManager] OnGoldPicked: set pickGold = true (via idToPickIndex) id={id} pickIndex={pickIndex}");
                }

                // FIRE EVENTS
                try
                {
                    OnSpecialGoldPicked?.Invoke(pickIndex);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[GoldManager] Exception when invoking OnSpecialGoldPicked: {ex}");
                }

                try
                {
                    OnSpecialGoldPickedEvent?.Invoke(pickIndex);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[GoldManager] Exception when invoking OnSpecialGoldPickedEvent: {ex}");
                }

                UnityEngine.Debug.Log($"[GoldManager] OnSpecialGoldPicked invoked for pickIndex={pickIndex} (via idToPickIndex)");
                return;
            }
        }

        if (pickedGold1 != null && pickedGold1.id == id)
        {
            if (GamePlayScript.instance != null) GamePlayScript.instance.pickGold = true;
            UnityEngine.Debug.Log($"[GoldManager] OnGoldPicked: matched pickedGold1 id={id} -> pickGold = true");

            try { OnSpecialGoldPicked?.Invoke(1); } catch (Exception ex) { UnityEngine.Debug.LogError(ex.ToString()); }
            try { OnSpecialGoldPickedEvent?.Invoke(1); } catch (Exception ex) { UnityEngine.Debug.LogError(ex.ToString()); }

            return;
        }
        if (pickedGold2 != null && pickedGold2.id == id)
        {
            if (GamePlayScript.instance != null) GamePlayScript.instance.pickGold = true;
            UnityEngine.Debug.Log($"[GoldManager] OnGoldPicked: matched pickedGold2 id={id} -> pickGold = true");

            try { OnSpecialGoldPicked?.Invoke(2); } catch (Exception ex) { UnityEngine.Debug.LogError(ex.ToString()); }
            try { OnSpecialGoldPickedEvent?.Invoke(2); } catch (Exception ex) { UnityEngine.Debug.LogError(ex.ToString()); }

            return;
        }
        if (pickedGold3 != null && pickedGold3.id == id)
        {
            if (GamePlayScript.instance != null) GamePlayScript.instance.pickGold = true;
            UnityEngine.Debug.Log($"[GoldManager] OnGoldPicked: matched pickedGold3 id={id} -> pickGold = true");

            try { OnSpecialGoldPicked?.Invoke(3); } catch (Exception ex) { UnityEngine.Debug.LogError(ex.ToString()); }
            try { OnSpecialGoldPickedEvent?.Invoke(3); } catch (Exception ex) { UnityEngine.Debug.LogError(ex.ToString()); }

            return;
        }

        UnityEngine.Debug.Log($"[GoldManager] OnGoldPicked: id={id} is NOT one of picked special golds (no flag).");
    }

    // When a gold is destroyed, if it was one of the picked ones we:
    // - remove it from lists/maps
    // - set the corresponding pickedXDestroyedFlag = true so VictoryController can respond
    // - spawn replacement prefab if configured (existing logic)
    public void OnGoldDestroyed(int id, Vector3 posUnused)
    {
        if (!isRunningInPlay)
        {
            UnityEngine.Debug.LogWarning($"[GoldManager] OnGoldDestroyed called id={id} but manager not running in Play. Ignoring. ActiveScene='{SceneManager.GetActiveScene().name}'");
            return;
        }

        UnityEngine.Debug.Log($"[GoldManager] OnGoldDestroyed START id={id} | idToPickIndex keys: {string.Join(",", idToPickIndex.Keys)} | goldList.count={goldList.Count}");

        Gold gold = goldList.Find(g => g != null && g.id == id);
        if (gold != null)
        {
            goldList.Remove(gold);
            UnityEngine.Debug.Log($"[GoldManager] Removed gold from goldList id={id} name={gold.name} instanceID={gold.GetInstanceID()}");
        }
        else
        {
            UnityEngine.Debug.Log($"[GoldManager] goldList did not contain id={id}");
        }

        int pickIndex = -1;
        bool hadMap = idToPickIndex.TryGetValue(id, out pickIndex);

        if (!hadMap)
        {
            UnityEngine.Debug.Log($"[GoldManager] id={id} not found in idToPickIndex. Checking pickedGold references...");

            if (pickedGold1 != null) UnityEngine.Debug.Log($"pickedGold1 id={pickedGold1.id} instanceID={pickedGold1.GetInstanceID()}");
            if (pickedGold2 != null) UnityEngine.Debug.Log($"pickedGold2 id={pickedGold2.id} instanceID={pickedGold2.GetInstanceID()}");
            if (pickedGold3 != null) UnityEngine.Debug.Log($"pickedGold3 id={pickedGold3.id} instanceID={pickedGold3.GetInstanceID()}");

            if (pickedGold1 != null && pickedGold1.id == id) { pickIndex = 1; UnityEngine.Debug.Log("[GoldManager] matched pickedGold1"); }
            else if (pickedGold2 != null && pickedGold2.id == id) { pickIndex = 2; UnityEngine.Debug.Log("[GoldManager] matched pickedGold2"); }
            else if (pickedGold3 != null && pickedGold3.id == id) { pickIndex = 3; UnityEngine.Debug.Log("[GoldManager] matched pickedGold3"); }
            else
            {
                UnityEngine.Debug.Log($"[GoldManager] OnGoldDestroyed: id={id} not mapped and not matched pickedGolds -> treat as normal destroy (no special spawn).");
                return;
            }
        }
        else
        {
            idToPickIndex.Remove(id);
            UnityEngine.Debug.Log($"[GoldManager] id={id} found in idToPickIndex with pickIndex={pickIndex}. Removed mapping.");
        }

        // Set the destroyed-flag so other systems (VictoryController) can react.
        if (pickIndex == 1) picked1DestroyedFlag = true;
        else if (pickIndex == 2) picked2DestroyedFlag = true;
        else if (pickIndex == 3) picked3DestroyedFlag = true;

        GameObject prefabToSpawn = null;
        Transform spawnPoint = null;

        if (pickIndex == 1)
        {
            if (PrefabPickedGold1 != null && PrefabPickedGold1.Count > 0)
                prefabToSpawn = PrefabPickedGold1[Random.Range(0, PrefabPickedGold1.Count)];
            spawnPoint = spawnPointForFirstGold;
            if (pickedGold1 != null && pickedGold1.id == id) pickedGold1 = null;
        }
        else if (pickIndex == 2)
        {
            prefabToSpawn = PrefabPickedGold2;
            spawnPoint = spawnPointForSecondGold;
            if (pickedGold2 != null && pickedGold2.id == id) pickedGold2 = null;
        }
        else if (pickIndex == 3)
        {
            prefabToSpawn = PrefabPickedGold3;
            spawnPoint = spawnPointForThirdGold;
            if (pickedGold3 != null && pickedGold3.id == id) pickedGold3 = null;
        }

        if (prefabToSpawn == null)
        {
            UnityEngine.Debug.LogWarning($"[GoldManager] prefabToSpawn is null for id={id}, pickIndex={pickIndex}. No spawn will occur.");
            return;
        }

        if (spawnPoint != null)
        {
            Scene spawnScene = spawnPoint.gameObject.scene;
            Scene active = SceneManager.GetActiveScene();
            if (spawnScene != active)
            {
                UnityEngine.Debug.Log($"[GoldManager] Skipping spawn because spawnPoint is in scene '{spawnScene.name}' but active scene is '{active.name}'.");
                return;
            }
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : posUnused;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject spawned = null;
        try { spawned = Instantiate(prefabToSpawn, spawnPos, spawnRot); }
        catch (System.Exception ex) { UnityEngine.Debug.LogError($"[GoldManager] Exception when Instantiate prefab: {ex}"); spawned = null; }

        if (spawned != null)
        {
            UnityEngine.Debug.Log($"[GoldManager] Spawned prefab for pickIndex={pickIndex}: {prefabToSpawn.name} at pos {spawnPos}. Stacktrace:\n{new System.Diagnostics.StackTrace(true)}");
            Animator anim = spawned.GetComponent<Animator>();
            if (anim != null)
            {
                try { anim.SetTrigger("Happy"); }
                catch { }
            }

            // Reset luot logic nếu spawn trúng PrefabPickedGold2/3
            if (pickIndex == 2 && prefabToSpawn == PrefabPickedGold2)
            {
                luot1 = 0;
                SafeSaveLuot1();
                UnityEngine.Debug.Log($"[GoldManager] Reset luot1 -> 0 (spawned PrefabPickedGold2).");
            }
            else if (pickIndex == 3 && prefabToSpawn == PrefabPickedGold3)
            {
                luot2 = 0;
                SafeSaveLuot2();
                UnityEngine.Debug.Log($"[GoldManager] Reset luot2 -> 0 (spawned PrefabPickedGold3).");
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[GoldManager] Instantiate returned null for pickIndex={pickIndex}. Not resetting luot.");
        }

        if (OngGiaScript.instance != null)
        {
            OngGiaScript.instance.Happy();
            UnityEngine.Debug.Log("[GoldManager] Called OngGiaScript.instance.Happy()");
        }
    }

#if UNITY_EDITOR
    public void OnEditorClearPicked()
    {
        pickedGold1 = null;
        pickedGold2 = null;
        pickedGold3 = null;
        idToPickIndex.Clear();
        luot1 = 0; luot2 = 0;
        picked1DestroyedFlag = false;
        picked2DestroyedFlag = false;
        picked3DestroyedFlag = false;
        SaveLuot1();
        SaveLuot2();
        UnityEngine.Debug.Log("[GoldManager] OnEditorClearPicked: reset luot1/luot2 to 0 and cleared flags");
    }
#endif

    [ContextMenu("Debug_PrintLuot")]
    private void Debug_PrintLuot()
    {
        UnityEngine.Debug.Log($"[GoldManager][Debug] luot1={luot1}, luot2={luot2} | flags: p1={picked1DestroyedFlag} p2={picked2DestroyedFlag} p3={picked3DestroyedFlag}");
    }

    // Public helper: reset destroyed-flags (call from UI or other scripts if needed)
    public void ResetPickedDestroyedFlags()
    {
        picked1DestroyedFlag = false;
        picked2DestroyedFlag = false;
        picked3DestroyedFlag = false;
        UnityEngine.Debug.Log("[GoldManager] ResetPickedDestroyedFlags called.");
    }
}

// Helper class so Unity inspector có thể hiển thị UnityEvent<int>
[Serializable]
public class UnityEventInt : UnityEvent<int> { }
