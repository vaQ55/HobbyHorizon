using UnityEngine;

public class Gold : MonoBehaviour
{
    [HideInInspector] public int id = 0;

    private bool registered = false;

    private void OnEnable()
    {
        TryRegister();
    }

    private void Start()
    {
        // đảm bảo đăng ký nếu OnEnable lúc đó chưa có manager
        TryRegister();
    }

    private void TryRegister()
    {
        if (registered) return;

        // Nếu có Instance thì register ngay
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.Register(this);
            registered = true;
            return;
        }

        // Nếu Instance null, cố gắng tìm GoldManager trong scene và register trực tiếp
        var gm = FindObjectOfType<GoldManager>();
        if (gm != null)
        {
            gm.Register(this);
            registered = true;
            return;
        }

        // Nếu vẫn không có, đợi Start (không block) — Start() sẽ gọi TryRegister lại
    }

    private void OnDestroy()
    {
        // Nếu manager vẫn tồn tại thông báo destroyed
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.OnGoldDestroyed(id, transform.position);
        }
    }

    private void OnDisable()
    {
        // Nếu bạn dùng pooling và không muốn OnDestroy gọi, bạn có thể báo manager ở đây.
        // Tuy nhiên mặc định ta không unregister để giữ id nếu object re-enable.
    }
}
