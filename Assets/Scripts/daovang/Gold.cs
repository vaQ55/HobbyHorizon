using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class Gold : MonoBehaviour
{
    public int id = 0;

    private bool registered = false;
    private bool isPicked = false;

    private void OnEnable()
    {
        TryRegister();
    }

    private void Start()
    {
        TryRegister();
    }

    private void TryRegister()
    {
        if (registered) return;

        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.Register(this);
            registered = true;
            return;
        }

        var gm = FindObjectOfType<GoldManager>();
        if (gm != null)
        {
            gm.Register(this);
            registered = true;
            return;
        }
    }

    /// <summary>
    /// Gọi khi object thực sự được player "pick" (kéo lên bờ).
    /// Sau khi gọi GamePlayScript.OnGoldCollected, hàm sẽ gọi GoldManager.OnGoldDestroyed(...) trước khi Destroy để đảm bảo prefab được spawn.
    /// </summary>
    public void NotifyPicked()
    {
        if (isPicked) return;
        isPicked = true;
        StartCoroutine(ProcessPickedCoroutine());
    }

    private IEnumerator ProcessPickedCoroutine()
    {
        // capture scene lúc bắt đầu pick để tránh race khi user chuyển scene giữa chừng
        string startScene = SceneManager.GetActiveScene().name;

        // Đảm bảo đã register; chờ tối đa vài frame nếu cần
        if (!registered) TryRegister();

        int waitFrames = 0;
        while (!registered && waitFrames < 10)
        {
            yield return null;
            TryRegister();
            waitFrames++;
        }

        Debug.Log($"[Gold] NotifyPicked start. id={id} registered={registered} instanceID={GetInstanceID()} startScene='{startScene}' currentScene='{SceneManager.GetActiveScene().name}'");

        // thông báo cho GoldManager: đánh dấu pick (reference hoặc id)
        if (GoldManager.Instance != null)
        {
            // gọi OnGoldPicked để set pick flag (GamePlayScript.pickGold) nếu cần
            GoldManager.Instance.OnGoldPicked(this);
            Debug.Log($"[Gold] Called GoldManager.OnGoldPicked(this) instanceID={GetInstanceID()} id={id}");
        }
        else
        {
            Debug.LogWarning($"[Gold] GoldManager.Instance is null when picking id={id}");
        }

        // đợi 1 frame để các side-effect (GamePlayScript.pickGold) apply
        yield return null;

        // nếu scene đã đổi thì abort - không gọi gameplay / spawn nữa
        if (SceneManager.GetActiveScene().name != startScene)
        {
            Debug.LogWarning($"[Gold] Scene changed during pick. startScene='{startScene}' currentScene='{SceneManager.GetActiveScene().name}' -> abort further pick processing for id={id}");
            yield break;
        }

        // thông báo cho gameplay (sử dụng id nếu có)
        if (GamePlayScript.instance != null)
        {
            GamePlayScript.instance.OnGoldCollected(id);
            Debug.Log($"[Gold] Called GamePlayScript.OnGoldCollected({id}). pickGold now = {GamePlayScript.instance.pickGold}");
        }
        else
        {
            Debug.LogWarning($"[Gold] GamePlayScript.instance is null when picking id={id}");
        }

        // --- GỌI OnGoldDestroyed chỉ khi GoldManager còn tồn tại và scene chưa đổi ---
        if (GoldManager.Instance != null)
        {
            // final safety: kiểm tra scene một lần nữa
            if (SceneManager.GetActiveScene().name == startScene)
            {
                GoldManager.Instance.OnGoldDestroyed(id, transform.position);
                Debug.Log($"[Gold] Called GoldManager.OnGoldDestroyed({id}) from NotifyPicked() to spawn picked-prefab.");
            }
            else
            {
                Debug.LogWarning($"[Gold] Skipped OnGoldDestroyed because scene changed. startScene='{startScene}' currentScene='{SceneManager.GetActiveScene().name}' id={id}");
            }
        }
        else
        {
            Debug.LogWarning($"[Gold] Skipped OnGoldDestroyed because GoldManager.Instance is null id={id}");
        }

        // cuối cùng destroy object (nếu vẫn tồn tại)
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // LƯU Ý: Không gọi OnGoldDestroyed ở đây nếu isPicked == true (đã spawn ở trên).
        // Ngoài ra kiểm tra GoldManager.Instance != null để tránh gọi vào manager đã bị destroy.
        if (!isPicked)
        {
            if (GoldManager.Instance != null)
            {
                // thêm check scene: nếu active scene khác với scene khi object còn (không dễ truy xuất),
                // chúng ta ít nhất đảm bảo GoldManager còn tồn tại — nếu bạn muốn chặt chẽ hơn, quản lý từ GoldManager.
                GoldManager.Instance.OnGoldDestroyed(id, transform.position);
                Debug.Log($"[Gold] OnDestroy -> OnGoldDestroyed({id}) (not picked case)");
            }
            else
            {
                Debug.Log($"[Gold] OnDestroy: not picked but GoldManager.Instance is null -> skipping OnGoldDestroyed for id={id}");
            }
        }
    }
}
