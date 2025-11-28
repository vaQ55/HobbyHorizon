using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance;

    [Header("Prefab list cho pickedGold1 (random 1 trong list)")]
    public List<GameObject> PrefabPickedGold1 = new List<GameObject>();

    [Header("Prefab cố định cho pickedGold2 / pickedGold3")]
    public GameObject PrefabPickedGold2;
    public GameObject PrefabPickedGold3;

    [Header("Vị trí spawn 3 prefab khi vàng bị phá hủy")]
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
    [Tooltip("Tăng +1 mỗi lần Start; điều khiển khả năng spawn PrefabPickedGold2")]
    public int luot1 = 0;

    [Tooltip("Tăng +1 mỗi lần Start; điều khiển khả năng spawn PrefabPickedGold3")]
    public int luot2 = 0;

    // map id của gold -> index loại list/prefab:
    // 1 = dùng PrefabPickedGold1 (list), 2 = PrefabPickedGold2 (single), 3 = PrefabPickedGold3 (single)
    private Dictionary<int, int> idToPickIndex = new Dictionary<int, int>();

    private int nextID = 1;

    private const string PREF_KEY_LUOT1 = "GoldManager_luot1";
    private const string PREF_KEY_LUOT2 = "GoldManager_luot2";

    public IReadOnlyList<Gold> GoldList => goldList;
    public Gold PickedGold1 => pickedGold1;
    public Gold PickedGold2 => pickedGold2;
    public Gold PickedGold3 => pickedGold3;

    private void Awake()
    {
        // Singleton guard: nếu đã có Instance khác, destroy bản hiện tại để tránh chạy trùng
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GoldManager] Another instance exists — destroying duplicate.");
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
        //DontDestroyOnLoad(this.gameObject); // tuỳ bạn; nếu không muốn giữ qua scene thì remove

        // load luot1, luot2 từ PlayerPrefs (nếu có)
        luot1 = PlayerPrefs.GetInt(PREF_KEY_LUOT1, 0);
        luot2 = PlayerPrefs.GetInt(PREF_KEY_LUOT2, 0);
        Debug.Log($"[GoldManager][AWAKE] Loaded luot1={luot1}, luot2={luot2}");
    }

    private void Start()
    {
        // mỗi lần Start tăng +1 và lưu lại (ghi log đầy đủ)
        luot1++;
        luot2++;
        PlayerPrefs.SetInt(PREF_KEY_LUOT1, luot1);
        PlayerPrefs.SetInt(PREF_KEY_LUOT2, luot2);
        PlayerPrefs.Save();
        Debug.Log($"[GoldManager][START] Incremented & saved: luot1={luot1}, luot2={luot2}");

        if (autoPickOnStart)
            StartCoroutine(DelayedAutoPick());
    }

    private IEnumerator DelayedAutoPick()
    {
        yield return null;
        if (autoPickDelay > 0f)
            yield return new WaitForSeconds(autoPickDelay);

        // luôn pick slot 1 nếu có ít nhất 1 gold
        if (goldList.Count >= 1)
            RandomPickGold1();

        // Quyết định pick slot2 dựa vào luot1
        if (goldList.Count >= 2)
        {
            bool doPick2 = false;

            // nếu luot1 > 3 && luot1 < 6 => 50% chance (4 hoặc 5)
            if (luot1 > 3 && luot1 < 6)
            {
                if (Random.value < 0.5f) doPick2 = true;
            }
            // nếu luot1 > 6 => chắc chắn (>=7)
            else if (luot1 > 6)
            {
                doPick2 = true;
            }

            if (doPick2)
            {
                Debug.Log($"[GoldManager] Decision: will RandomPickGold2 (luot1={luot1})");
                RandomPickGold2();
            }
            else
            {
                Debug.Log($"[GoldManager] Decision: skip RandomPickGold2 (luot1={luot1})");
            }
        }

        // Quyết định pick slot3 dựa vào luot2 (tương tự)
        if (goldList.Count >= 3)
        {
            bool doPick3 = false;

            // nếu luot2 > 8 && luot2 < 10 => 50% chance (9)
            if (luot2 > 8 && luot2 < 10)
            {
                if (Random.value < 0.5f) doPick3 = true;
            }
            // nếu luot2 > 10 => chắc chắn (>=11)
            else if (luot2 > 10)
            {
                doPick3 = true;
            }

            if (doPick3)
            {
                Debug.Log($"[GoldManager] Decision: will RandomPickGold3 (luot2={luot2})");
                RandomPickGold3();
            }
            else
            {
                Debug.Log($"[GoldManager] Decision: skip RandomPickGold3 (luot2={luot2})");
            }
        }
    }

    // Register — tự gán ID
    public void Register(Gold g)
    {
        if (g == null) return;

        if (!goldList.Contains(g))
        {
            g.id = nextID++;
            goldList.Add(g);
            Debug.Log($"[GoldManager] Registered Gold id={g.id} name={g.name}");
        }
    }

    // --------------------------
    // Các hàm Random tách lẻ
    // --------------------------

    // Helper: lấy Gold random, trừ các id đã exclude; trả null nếu không lấy được
    private Gold GetRandomGoldExcluding(HashSet<int> excludeIds)
    {
        if (goldList.Count == 0) return null;

        // xây list chỉ gồm phần tử hợp lệ
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

    // Random cho pickedGold1 (sử dụng list PrefabPickedGold1)
    public void RandomPickGold1()
    {
        if (pickedGold1 != null && idToPickIndex.ContainsKey(pickedGold1.id))
            idToPickIndex.Remove(pickedGold1.id);

        var exclude = new HashSet<int>();
        if (pickedGold2 != null) exclude.Add(pickedGold2.id);
        if (pickedGold3 != null) exclude.Add(pickedGold3.id);

        Gold chosen = GetRandomGoldExcluding(exclude);
        if (chosen == null)
        {
            Debug.LogWarning("[GoldManager] RandomPickGold1: Không tìm được gold phù hợp!");
            pickedGold1 = null;
            return;
        }

        pickedGold1 = chosen;
        idToPickIndex[pickedGold1.id] = 1;
        Debug.Log($"[GoldManager] PickedGold1 = Gold ID {pickedGold1.id}");
    }

    // Random cho pickedGold2 (single prefab) - tránh trùng với pickedGold1
    public void RandomPickGold2()
    {
        if (pickedGold2 != null && idToPickIndex.ContainsKey(pickedGold2.id))
            idToPickIndex.Remove(pickedGold2.id);

        var exclude = new HashSet<int>();
        if (pickedGold1 != null) exclude.Add(pickedGold1.id);
        if (pickedGold3 != null) exclude.Add(pickedGold3.id);

        Gold chosen = GetRandomGoldExcluding(exclude);

        if (chosen == null)
        {
            if (goldList.Count > 0)
                chosen = goldList[Random.Range(0, goldList.Count)];
            else
                chosen = null;
        }

        if (chosen == null)
        {
            Debug.LogWarning("[GoldManager] RandomPickGold2: Không tìm được gold để pick!");
            pickedGold2 = null;
            return;
        }

        pickedGold2 = chosen;
        idToPickIndex[pickedGold2.id] = 2;
        Debug.Log($"[GoldManager] PickedGold2 = Gold ID {pickedGold2.id}");
    }

    // Random cho pickedGold3 (single prefab) - tránh trùng với pickedGold1 và pickedGold2
    public void RandomPickGold3()
    {
        if (pickedGold3 != null && idToPickIndex.ContainsKey(pickedGold3.id))
            idToPickIndex.Remove(pickedGold3.id);

        var exclude = new HashSet<int>();
        if (pickedGold1 != null) exclude.Add(pickedGold1.id);
        if (pickedGold2 != null) exclude.Add(pickedGold2.id);

        Gold chosen = GetRandomGoldExcluding(exclude);

        if (chosen == null)
        {
            if (goldList.Count > 0)
                chosen = goldList[Random.Range(0, goldList.Count)];
            else
                chosen = null;
        }

        if (chosen == null)
        {
            Debug.LogWarning("[GoldManager] RandomPickGold3: Không tìm được gold để pick!");
            pickedGold3 = null;
            return;
        }

        pickedGold3 = chosen;
        idToPickIndex[pickedGold3.id] = 3;
        Debug.Log($"[GoldManager] PickedGold3 = Gold ID {pickedGold3.id}");
    }

    // Giữ hàm RandomTwo/RandomThree nếu cần gọi ngoại tuyến (tuỳ chọn)
    public void RandomTwoGold()
    {
        idToPickIndex.Clear();
        pickedGold1 = null;
        pickedGold2 = null;
        pickedGold3 = null;

        if (goldList.Count < 2)
        {
            Debug.LogWarning("[GoldManager] Không đủ vàng để random 2!");
            return;
        }

        int i1 = Random.Range(0, goldList.Count);
        int i2;
        do { i2 = Random.Range(0, goldList.Count); } while (i2 == i1);

        pickedGold1 = goldList[i1];
        pickedGold2 = goldList[i2];

        idToPickIndex[pickedGold1.id] = 1;
        idToPickIndex[pickedGold2.id] = 2;

        Debug.Log($"[GoldManager] Picked Gold {pickedGold1.id} → PrefabPickedGold1 (list)");
        Debug.Log($"[GoldManager] Picked Gold {pickedGold2.id} → PrefabPickedGold2 (single)");
    }

    public void RandomThreeGold()
    {
        idToPickIndex.Clear();
        pickedGold1 = null;
        pickedGold2 = null;
        pickedGold3 = null;

        if (goldList.Count < 3)
        {
            Debug.LogWarning("[GoldManager] Không đủ vàng để random 3!");
            return;
        }

        int i1 = Random.Range(0, goldList.Count);
        int i2;
        int i3;

        do { i2 = Random.Range(0, goldList.Count); } while (i2 == i1);
        do { i3 = Random.Range(0, goldList.Count); } while (i3 == i1 || i3 == i2);

        pickedGold1 = goldList[i1];
        pickedGold2 = goldList[i2];
        pickedGold3 = goldList[i3];

        idToPickIndex[pickedGold1.id] = 1;
        idToPickIndex[pickedGold2.id] = 2;
        idToPickIndex[pickedGold3.id] = 3;

        Debug.Log($"[GoldManager] Picked Gold {pickedGold1.id} → PrefabPickedGold1 (list)");
        Debug.Log($"[GoldManager] Picked Gold {pickedGold2.id} → PrefabPickedGold2 (single)");
        Debug.Log($"[GoldManager] Picked Gold {pickedGold3.id} → PrefabPickedGold3 (single)");
    }

    // Khi 1 vàng bị destroy
    public void OnGoldDestroyed(int id, Vector3 posUnused)
    {
        Gold gold = goldList.Find(g => g != null && g.id == id);
        if (gold != null)
            goldList.Remove(gold);

        if (!idToPickIndex.TryGetValue(id, out int pickIndex))
        {
            Debug.Log($"[GoldManager] OnGoldDestroyed: id={id} not in idToPickIndex, ignoring.");
            return;
        }

        idToPickIndex.Remove(id);

        GameObject prefabToSpawn = null;
        Transform spawnPoint = null;

        if (pickIndex == 1)
        {
            if (PrefabPickedGold1 == null || PrefabPickedGold1.Count == 0)
            {
                Debug.LogWarning("[GoldManager] PrefabPickedGold1 list rỗng hoặc null!");
            }
            else
            {
                prefabToSpawn = PrefabPickedGold1[Random.Range(0, PrefabPickedGold1.Count)];
            }
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
            Debug.LogWarning($"[GoldManager] No prefabToSpawn for pickIndex={pickIndex} (id={id}). No reset.");
            return;
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : posUnused;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject spawned = null;
        try
        {
            spawned = Instantiate(prefabToSpawn, spawnPos, spawnRot);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GoldManager] Exception when Instantiate prefab: {ex}");
            spawned = null;
        }

        if (spawned != null)
        {
            Debug.Log($"[GoldManager] Spawned prefab for pickIndex={pickIndex}: {prefabToSpawn.name}");

            // RESET luot chỉ khi prefab spawn thành công và đúng prefab configured
            if (pickIndex == 2 && prefabToSpawn == PrefabPickedGold2)
            {
                luot1 = 0;
                PlayerPrefs.SetInt(PREF_KEY_LUOT1, luot1);
                PlayerPrefs.Save();
                Debug.Log($"[GoldManager] Reset luot1 -> 0 (spawned PrefabPickedGold2).");
            }
            else if (pickIndex == 3 && prefabToSpawn == PrefabPickedGold3)
            {
                luot2 = 0;
                PlayerPrefs.SetInt(PREF_KEY_LUOT2, luot2);
                PlayerPrefs.Save();
                Debug.Log($"[GoldManager] Reset luot2 -> 0 (spawned PrefabPickedGold3).");
            }
            else
            {
                Debug.Log($"[GoldManager] Spawned prefab does not trigger luot reset (pickIndex={pickIndex}, prefab={prefabToSpawn.name}).");
            }
        }
        else
        {
            Debug.LogWarning($"[GoldManager] Instantiate returned null for pickIndex={pickIndex}. Not resetting luot.");
        }

        if (OngGiaScript.instance != null)
            OngGiaScript.instance.Happy();
    }

#if UNITY_EDITOR
    public void OnEditorClearPicked()
    {
        pickedGold1 = null;
        pickedGold2 = null;
        pickedGold3 = null;
        idToPickIndex.Clear();

        // reset luot (nếu muốn) - log rõ ràng
        luot1 = 0;
        luot2 = 0;
        PlayerPrefs.SetInt(PREF_KEY_LUOT1, luot1);
        PlayerPrefs.SetInt(PREF_KEY_LUOT2, luot2);
        PlayerPrefs.Save();
        Debug.Log("[GoldManager] OnEditorClearPicked: reset luot1/luot2 to 0");
    }
#endif

    // --- Debug helpers ---
    // Gọi thủ công để xem trạng thái luot hiện thời
    [ContextMenu("Debug_PrintLuot")]
    private void Debug_PrintLuot()
    {
        Debug.Log($"[GoldManager][Debug] luot1={luot1}, luot2={luot2}");
    }
}
