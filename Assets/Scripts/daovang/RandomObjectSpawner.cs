using UnityEngine;
using System.Collections.Generic;

public class RandomObjectSpawner : MonoBehaviour
{
    [Header("Danh sách prefab có thể spawn")]
    public List<GameObject> objectsToSpawn = new List<GameObject>();

    [Header("Vị trí spawn (nếu bỏ trống sẽ spawn ngay tại vị trí prefab này)")]
    public Transform spawnPoint;

    // Hàm này gọi từ nơi khác (ví dụ GoldManager khi prefab được tạo ra)
    public void SpawnRandom()
    {
        if (objectsToSpawn.Count == 0)
        {
            Debug.LogWarning("[RandomObjectSpawner] Không có object nào trong danh sách!");
            return;
        }

        // Random 1 object bất kỳ
        int index = Random.Range(0, objectsToSpawn.Count);
        GameObject prefab = objectsToSpawn[index];

        Transform point = spawnPoint != null ? spawnPoint : transform;

        Instantiate(prefab, point.position, point.rotation);

        Debug.Log($"[RandomObjectSpawner] Spawned random object: {prefab.name}");
    }
}
