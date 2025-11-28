using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance;

    [Header("Prefabs khi 2 cục vàng được chọn")]
    public GameObject prefabForFirstGold;
    public GameObject prefabForSecondGold;

    [Header("Vị trí spawn 2 prefab khi vàng bị phá hủy")]
    public Transform spawnPointForFirstGold;
    public Transform spawnPointForSecondGold;

    [Header("Danh sách vàng đang tồn tại (Readonly)")]
    [SerializeField] private List<Gold> goldList = new List<Gold>();

    [Header("Hai cục vàng được random trúng (Readonly)")]
    [SerializeField] private Gold pickedGold1;
    [SerializeField] private Gold pickedGold2;

    [Header("Auto pick when scene starts")]
    public bool autoPickOnStart = true;
    public float autoPickDelay = 0.2f;

    private Dictionary<int, GameObject> idToPrefab = new Dictionary<int, GameObject>();
    private int nextID = 1;

    public IReadOnlyList<Gold> GoldList => goldList;
    public Gold PickedGold1 => pickedGold1;
    public Gold PickedGold2 => pickedGold2;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (autoPickOnStart)
            StartCoroutine(DelayedAutoPick());
    }

    private IEnumerator DelayedAutoPick()
    {
        yield return null;
        if (autoPickDelay > 0f)
            yield return new WaitForSeconds(autoPickDelay);

        if (goldList.Count >= 2)
            RandomTwoGold();
        else
            Debug.LogWarning($"[GoldManager] AutoPick: chỉ có {goldList.Count} gold. Không thể pick 2.");
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

    // Random chọn 2 vàng
    public void RandomTwoGold()
    {
        if (goldList.Count < 2)
        {
            Debug.LogWarning("[GoldManager] Không đủ vàng để random!");
            return;
        }

        idToPrefab.Clear();

        int index1 = Random.Range(0, goldList.Count);
        int index2;
        do
        {
            index2 = Random.Range(0, goldList.Count);
        } while (index2 == index1);

        pickedGold1 = goldList[index1];
        pickedGold2 = goldList[index2];

        idToPrefab[pickedGold1.id] = prefabForFirstGold;
        idToPrefab[pickedGold2.id] = prefabForSecondGold;

        Debug.Log($"[GoldManager] Picked Gold {pickedGold1.id} → Prefab1");
        Debug.Log($"[GoldManager] Picked Gold {pickedGold2.id} → Prefab2");
    }

    // Khi 1 vàng bị destroy
    public void OnGoldDestroyed(int id, Vector3 posUnused)
    {
        Gold gold = goldList.Find(g => g != null && g.id == id);
        if (gold != null)
            goldList.Remove(gold);

        if (idToPrefab.TryGetValue(id, out GameObject prefab))
        {
            Transform spawnPoint = null;

            if (pickedGold1 != null && pickedGold1.id == id)
                spawnPoint = spawnPointForFirstGold;

            if (pickedGold2 != null && pickedGold2.id == id)
                spawnPoint = spawnPointForSecondGold;

            if (spawnPoint != null && prefab != null)
            {
                // Instantiate prefab at configured spawn point
                GameObject spawned = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

                // --- GỌI ANIMATION "Happy" TRÊN ÔNG GIÀ ---
                // Kiểm tra và gọi instance nếu tồn tại
                if (OngGiaScript.instance != null)
                {
                    OngGiaScript.instance.Happy();
                }
                else
                {
                    Debug.LogWarning("[GoldManager] OngGiaScript.instance is null - cannot call Happy()");
                }

                // Nếu bạn muốn gọi Happy trên component nằm trong prefab vừa spawn
                // (ví dụ prefab có script riêng), bạn có thể làm:
                // var ongGiaComp = spawned.GetComponent<OngGiaScript>();
                // if (ongGiaComp != null) ongGiaComp.Happy();
            }
            else
            {
                Debug.LogWarning($"[GoldManager] Spawn point for gold id={id} is missing or prefab null!");
            }

            idToPrefab.Remove(id);
        }

        if (pickedGold1 != null && pickedGold1.id == id)
            pickedGold1 = null;

        if (pickedGold2 != null && pickedGold2.id == id)
            pickedGold2 = null;
    }

#if UNITY_EDITOR
    public void OnEditorClearPicked()
    {
        pickedGold1 = null;
        pickedGold2 = null;
        idToPrefab.Clear();
    }
#endif
}
