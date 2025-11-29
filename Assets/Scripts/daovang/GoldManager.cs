using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Flags được bật khi các pickedGold bị destroy")]
    public bool picked1DestroyedFlag = false;
    public bool picked2DestroyedFlag = false;
    public bool picked3DestroyedFlag = false;

    [Tooltip("Invoked with parameter = pickIndex (1,2,3) when special gold is picked.")]
    public UnityEventInt OnSpecialGoldPickedEvent = new UnityEventInt();

    public event Action<int> OnSpecialGoldPicked;

    // ===== FIX: TRACK SPAWNED PREFABS =====
    [Header("Debug: Spawned Prefabs (Auto-managed)")]
    [SerializeField] private List<GameObject> spawnedPrefabs = new List<GameObject>();

    private Dictionary<int, int> idToPickIndex = new Dictionary<int, int>();
    private int nextID = 1;
    private const string PREF_KEY_LUOT1 = "GoldManager_luot1";
    private const string PREF_KEY_LUOT2 = "GoldManager_luot2";

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
        Debug.Log($"[GoldManager] Awake in scene '{SceneManager.GetActiveScene().name}'");

        if (SceneManager.GetActiveScene().name != playSceneName)
        {
            Debug.LogWarning($"[GoldManager] Not in Play scene, destroying.");
            DestroyImmediate(this.gameObject);
            return;
        }

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GoldManager] Duplicate instance, destroying.");
            DestroyImmediate(this.gameObject);
            return;
        }

        Instance = this;
        isRunningInPlay = true;

        luot1 = PlayerPrefs.GetInt(PREF_KEY_LUOT1, 0);
        luot2 = PlayerPrefs.GetInt(PREF_KEY_LUOT2, 0);
        Debug.Log($"[GoldManager] Loaded luot1={luot1}, luot2={luot2}");
    }

    private void Start()
    {
        if (!isRunningInPlay) return;

        luot1++;
        luot2++;
        SafeSaveLuot1();
        SafeSaveLuot2();
        Debug.Log($"[GoldManager] Incremented: luot1={luot1}, luot2={luot2}");

        if (autoPickOnStart)
            autoPickCoroutine = StartCoroutine(DelayedAutoPick());
    }

    private void SaveLuot1()
    {
        PlayerPrefs.SetInt(PREF_KEY_LUOT1, luot1);
        PlayerPrefs.Save();
        Debug.Log($"[GoldManager] Saved luot1={luot1}");
    }

    private void SaveLuot2()
    {
        PlayerPrefs.SetInt(PREF_KEY_LUOT2, luot2);
        PlayerPrefs.Save();
        Debug.Log($"[GoldManager] Saved luot2={luot2}");
    }

    private void SafeSaveLuot1()
    {
        if (!isRunningInPlay) return;
        SaveLuot1();
    }

    private void SafeSaveLuot2()
    {
        if (!isRunningInPlay) return;
        SaveLuot2();
    }

    private void OnActiveSceneChanged(Scene prev, Scene next)
    {
        Debug.Log($"[GoldManager] Scene changed: '{prev.name}' -> '{next.name}'");
        if (next.name != playSceneName && isRunningInPlay)
        {
            CleanupAndDestroy();
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        Debug.Log($"[GoldManager] Scene unloaded: '{scene.name}'");
        if (scene.name == playSceneName && isRunningInPlay)
        {
            CleanupAndDestroy();
        }
    }

    // ===== FIX: CLEANUP SPAWNED PREFABS =====
    private void CleanupAndDestroy()
    {
        Debug.Log("[GoldManager] CleanupAndDestroy called.");

        if (autoPickCoroutine != null)
        {
            StopCoroutine(autoPickCoroutine);
            autoPickCoroutine = null;
        }
        StopAllCoroutines();

        // DESTROY ALL SPAWNED PREFABS
        CleanupSpawnedPrefabs();

        pickedGold1 = null;
        pickedGold2 = null;
        pickedGold3 = null;
        idToPickIndex.Clear();
        goldList.Clear();

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

    // ===== NEW: CLEANUP SPAWNED PREFABS =====
    private void CleanupSpawnedPrefabs()
    {
        Debug.Log($"[GoldManager] Cleaning up {spawnedPrefabs.Count} spawned prefabs.");
        
        foreach (var obj in spawnedPrefabs)
        {
            if (obj != null)
            {
                Debug.Log($"[GoldManager] Destroying spawned prefab: {obj.name}");
                Destroy(obj);
            }
        }
        spawnedPrefabs.Clear();
    }

    // ===== NEW: PERIODIC CLEANUP OF NULL REFERENCES =====
    private void LateUpdate()
    {
        if (!isRunningInPlay) return;

        // Clean null golds every 60 frames
        if (Time.frameCount % 60 == 0)
        {
            int before = goldList.Count;
            goldList.RemoveAll(g => g == null);
            if (goldList.Count != before)
            {
                Debug.Log($"[GoldManager] Cleaned {before - goldList.Count} null golds from list.");
            }

            // Clean null spawned prefabs
            spawnedPrefabs.RemoveAll(p => p == null);
        }
    }

    private IEnumerator DelayedAutoPick()
    {
        if (!isRunningInPlay) yield break;

        yield return null;
        if (autoPickDelay > 0f)
            yield return new WaitForSeconds(autoPickDelay);

        Debug.Log($"[GoldManager] DelayedAutoPick: {goldList.Count} golds available");

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
        CleanupSpawnedPrefabs(); // FIX: Cleanup on destroy
        Debug.Log("[GoldManager] OnDestroy executed.");
    }

    public void Register(Gold g)
    {
        if (!isRunningInPlay) return;
        if (g == null) return;

        if (!goldList.Contains(g))
        {
            g.id = nextID++;
            goldList.Add(g);
            Debug.Log($"[GoldManager] Registered Gold id={g.id} name={g.name}");
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
            Debug.LogWarning("[GoldManager] RandomPickGold1: No suitable gold found!");
            pickedGold1 = null;
            return;
        }

        pickedGold1 = chosen;
        idToPickIndex[pickedGold1.id] = 1;
        Debug.Log($"[GoldManager] PickedGold1 = Gold ID {pickedGold1.id}");
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

        if (chosen == null && goldList.Count > 0)
            chosen = goldList[Random.Range(0, goldList.Count)];

        if (chosen == null)
        {
            Debug.LogWarning("[GoldManager] RandomPickGold2: No gold to pick!");
            pickedGold2 = null;
            return;
        }

        pickedGold2 = chosen;
        idToPickIndex[pickedGold2.id] = 2;
        Debug.Log($"[GoldManager] PickedGold2 = Gold ID {pickedGold2.id}");
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

        if (chosen == null && goldList.Count > 0)
            chosen = goldList[Random.Range(0, goldList.Count)];

        if (chosen == null)
        {
            Debug.LogWarning("[GoldManager] RandomPickGold3: No gold to pick!");
            pickedGold3 = null;
            return;
        }

        pickedGold3 = chosen;
        idToPickIndex[pickedGold3.id] = 3;
        Debug.Log($"[GoldManager] PickedGold3 = Gold ID {pickedGold3.id}");
    }

    public void OnGoldPicked(Gold gold)
    {
        if (!isRunningInPlay || gold == null) return;

        Debug.Log($"[GoldManager] OnGoldPicked: Gold id={gold.id}");

        int pickIndex = GetPickIndexForGold(gold.id);
        
        if (pickIndex > 0)
        {
            if (GamePlayScript.instance != null)
            {
                GamePlayScript.instance.pickGold = true;
                Debug.Log($"[GoldManager] Set pickGold = true (pickIndex={pickIndex})");
            }

            InvokeSpecialGoldEvents(pickIndex);
        }
    }

    public void OnGoldPicked(int id)
    {
        if (!isRunningInPlay) return;

        Debug.Log($"[GoldManager] OnGoldPicked(int): id={id}");

        int pickIndex = GetPickIndexForGold(id);

        if (pickIndex > 0)
        {
            if (GamePlayScript.instance != null)
            {
                GamePlayScript.instance.pickGold = true;
                Debug.Log($"[GoldManager] Set pickGold = true (pickIndex={pickIndex})");
            }

            InvokeSpecialGoldEvents(pickIndex);
        }
    }

    // ===== NEW: HELPER TO GET PICK INDEX =====
    private int GetPickIndexForGold(int goldId)
    {
        if (idToPickIndex.TryGetValue(goldId, out int idx)) return idx;
        if (pickedGold1 != null && pickedGold1.id == goldId) return 1;
        if (pickedGold2 != null && pickedGold2.id == goldId) return 2;
        if (pickedGold3 != null && pickedGold3.id == goldId) return 3;
        return -1;
    }

    // ===== NEW: SAFE EVENT INVOCATION =====
    private void InvokeSpecialGoldEvents(int pickIndex)
    {
        try
        {
            OnSpecialGoldPicked?.Invoke(pickIndex);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GoldManager] Exception in OnSpecialGoldPicked: {ex}");
        }

        try
        {
            OnSpecialGoldPickedEvent?.Invoke(pickIndex);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GoldManager] Exception in OnSpecialGoldPickedEvent: {ex}");
        }

        Debug.Log($"[GoldManager] OnSpecialGoldPicked invoked for pickIndex={pickIndex}");
    }

    // ===== FIXED: TRACK SPAWNED PREFABS =====
    public void OnGoldDestroyed(int id, Vector3 posUnused)
    {
        if (!isRunningInPlay) return;

        Debug.Log($"[GoldManager] OnGoldDestroyed: id={id}");

        Gold gold = goldList.Find(g => g != null && g.id == id);
        if (gold != null)
        {
            goldList.Remove(gold);
            Debug.Log($"[GoldManager] Removed gold id={id} from goldList");
        }

        int pickIndex = GetPickIndexForGold(id);

        if (pickIndex <= 0)
        {
            Debug.Log($"[GoldManager] id={id} is not a special gold, no spawn.");
            return;
        }

        idToPickIndex.Remove(id);

        // Set destroyed flags
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
            Debug.LogWarning($"[GoldManager] No prefab to spawn for pickIndex={pickIndex}");
            return;
        }

        // Validate spawn point scene
        if (spawnPoint != null)
        {
            Scene spawnScene = spawnPoint.gameObject.scene;
            Scene active = SceneManager.GetActiveScene();
            if (spawnScene != active)
            {
                Debug.LogWarning($"[GoldManager] Skipping spawn - spawnPoint in wrong scene '{spawnScene.name}'");
                return;
            }
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : posUnused;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject spawned = null;
        try
        {
            spawned = Instantiate(prefabToSpawn, spawnPos, spawnRot);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GoldManager] Exception when spawning prefab: {ex}");
            return;
        }

        if (spawned != null)
        {
            // ===== FIX: TRACK SPAWNED PREFAB =====
            spawnedPrefabs.Add(spawned);
            Debug.Log($"[GoldManager] ✓ Spawned prefab '{spawned.name}' at {spawnPos} (tracked)");

            Animator anim = spawned.GetComponent<Animator>();
            if (anim != null)
            {
                try { anim.SetTrigger("Happy"); } catch { }
            }

            // Reset luot logic
            if (pickIndex == 2 && prefabToSpawn == PrefabPickedGold2)
            {
                luot1 = 0;
                SafeSaveLuot1();
                Debug.Log("[GoldManager] Reset luot1 to 0");
            }
            else if (pickIndex == 3 && prefabToSpawn == PrefabPickedGold3)
            {
                luot2 = 0;
                SafeSaveLuot2();
                Debug.Log("[GoldManager] Reset luot2 to 0");
            }
        }

        if (OngGiaScript.instance != null)
        {
            OngGiaScript.instance.Happy();
            Debug.Log("[GoldManager] Called OngGiaScript.instance.Happy()");
        }
    }

#if UNITY_EDITOR
    public void OnEditorClearPicked()
    {
        pickedGold1 = null;
        pickedGold2 = null;
        pickedGold3 = null;
        idToPickIndex.Clear();
        luot1 = 0;
        luot2 = 0;
        picked1DestroyedFlag = false;
        picked2DestroyedFlag = false;
        picked3DestroyedFlag = false;
        CleanupSpawnedPrefabs();
        SaveLuot1();
        SaveLuot2();
        Debug.Log("[GoldManager] Editor: Reset all data and cleaned spawned prefabs");
    }
#endif

    [ContextMenu("Debug_PrintLuot")]
    private void Debug_PrintLuot()
    {
        Debug.Log($"[GoldManager] luot1={luot1}, luot2={luot2} | flags: p1={picked1DestroyedFlag} p2={picked2DestroyedFlag} p3={picked3DestroyedFlag} | spawned: {spawnedPrefabs.Count}");
    }

    public void ResetPickedDestroyedFlags()
    {
        picked1DestroyedFlag = false;
        picked2DestroyedFlag = false;
        picked3DestroyedFlag = false;
        Debug.Log("[GoldManager] ResetPickedDestroyedFlags called.");
    }
}

[Serializable]
public class UnityEventInt : UnityEvent<int> { }