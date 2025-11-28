using UnityEngine;
using System.Collections;

public class VangScript : MonoBehaviour {

    public bool isMoveFollow = false;
    public float maxY;
    public int score;
    public float speed;
    Vector3 position;
    public Animator anim;

    // Use this for initialization
    void Start () {
        isMoveFollow = false;
        StartCoroutine(RunEffect());
    }

    // Update is called once per frame
    void Update () {
        // (không cần thay đổi ở đây)
    }

    IEnumerator RunEffect()
    {
        int ran = Random.Range(2, 5);
        yield return new WaitForSeconds(ran);
        anim.SetBool("Run", true);
        StartCoroutine(StopEffect());
    }
    IEnumerator StopEffect()
    {
        int ran = Random.Range(1, 3);
        yield return new WaitForSeconds(ran);
        anim.SetBool("Run", false);
        StartCoroutine(RunEffect());
    }

    void FixedUpdate() {
        Transform luoi = GameObject.Find("luoiCau") != null ? GameObject.Find("luoiCau").transform : null;
        if (luoi != null) moveFllowTarget(luoi);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if(other.gameObject.name == "luoiCau"
            && GameObject.Find("dayCau").GetComponent<DayCauScript>().typeAction != TypeAction.KeoCau)
        {
            isMoveFollow = true;
            LuoiCauScript.instance.cameraOut = false;
            GameObject.Find("dayCau").GetComponent<DayCauScript>().typeAction = TypeAction.KeoCau;
            GameObject.Find("luoiCau").GetComponent<LuoiCauScript>().velocity = -GameObject.Find("luoiCau").GetComponent<LuoiCauScript>().velocity;
            GameObject.Find("luoiCau").GetComponent<LuoiCauScript>().speed -= this.speed;
            GamePlayScript.instance.itemSeclected = gameObject.name;
        }
        else if (other.tag == "fire")
        {
            position = gameObject.transform.position;
            Instantiate(Resources.Load("GoldFire"), position, Quaternion.identity);
            Destroy(gameObject);
        }
    }

    void moveFllowTarget(Transform target) {
        if(isMoveFollow) 
        {
            Quaternion tg = Quaternion.Euler(target.parent.transform.rotation.x,
                                             target.parent.transform.rotation.y,
                                             target.parent.transform.rotation.z * 40);
            transform.rotation = Quaternion.Slerp(transform.rotation, tg, 0.5f);
            transform.position = new Vector3(target.position.x,
                                                target.position.y - gameObject.GetComponent<Collider2D>().bounds.size.y / 3,
                                                transform.position.z);
            if (GameObject.Find("dayCau").GetComponent<DayCauScript>().typeAction == TypeAction.Nghi) {
                // play sound based on value
                if(score > 0 && score <= 150)
                {
                    GamePlayScript.instance.PlaySound(1);
                }
                else if( score >150 && score <= 400)
                {
                    GamePlayScript.instance.PlaySound(2);
                }
                else
                {
                    GamePlayScript.instance.PlaySound(3);
                }

                // increase score
                if (gameObject.tag == "Stone")
                {
                    if (CGameManager.instance.bookStone)
                    {
                        GameObject.Find("GamePlay").GetComponent<GamePlayScript>().score += this.score * 3;
                        GameObject.Find("GamePlay").GetComponent<GamePlayScript>().CreateScoreFly(this.score * 3);
                    }
                    else
                    {
                        GameObject.Find("GamePlay").GetComponent<GamePlayScript>().score += this.score;
                        GameObject.Find("GamePlay").GetComponent<GamePlayScript>().CreateScoreFly(this.score);
                        OngGiaScript.instance.Angry();
                    }
                }
                else if (gameObject.tag == "Diamond" && CGameManager.instance.diamond)
                {
                    GameObject.Find("GamePlay").GetComponent<GamePlayScript>().score += this.score  +100;
                    GameObject.Find("GamePlay").GetComponent<GamePlayScript>().CreateScoreFly(this.score + 100);
                }
                else
                {
                    GameObject.Find("GamePlay").GetComponent<GamePlayScript>().score += this.score;
                    GameObject.Find("GamePlay").GetComponent<GamePlayScript>().CreateScoreFly(this.score);
                }

                // NEW: nếu là vàng (tag == "Gold") thì báo với GamePlayScript
                if (gameObject.tag == "Gold")
                {
                    if (GamePlayScript.instance != null)
                    {
                        GamePlayScript.instance.OnGoldCollected();
                    }
                }

                Destroy(gameObject);
            }
        }
    }
}
