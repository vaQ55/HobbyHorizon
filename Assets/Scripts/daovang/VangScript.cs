using UnityEngine;
using System.Collections;

public class VangScript : MonoBehaviour {

    public bool isMoveFollow = false;
    public float maxY;
    public int score;
    public float speed;
    Vector3 position;
    public Animator anim;

    void Start () {
        isMoveFollow = false;
        StartCoroutine(RunEffect());
    }

    void Update () { }

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
                if(score > 0 && score <= 150) GamePlayScript.instance.PlaySound(1);
                else if(score >150 && score <= 400) GamePlayScript.instance.PlaySound(2);
                else GamePlayScript.instance.PlaySound(3);

                if (gameObject.tag == "Stone")
                {
                    if (CGameManager.instance.bookStone)
                    {
                        GamePlayScript.instance.score += this.score * 3;
                        GamePlayScript.instance.CreateScoreFly(this.score * 3);
                    }
                    else
                    {
                        GamePlayScript.instance.score += this.score;
                        GamePlayScript.instance.CreateScoreFly(this.score);
                        OngGiaScript.instance.Angry();
                    }
                }
                else if (gameObject.tag == "Diamond" && CGameManager.instance.diamond)
                {
                    GamePlayScript.instance.score += this.score  +100;
                    GamePlayScript.instance.CreateScoreFly(this.score + 100);
                }
                else
                {
                    GamePlayScript.instance.score += this.score;
                    GamePlayScript.instance.CreateScoreFly(this.score);
                }

                if (gameObject.tag == "Gold")
                {
                    Gold goldComp = GetComponent<Gold>();
                    if (goldComp != null)
                    {
                        goldComp.NotifyPicked();
                    }
                    else
                    {
                        Debug.LogWarning("VangScript: tag is Gold but no Gold component found. Falling back to direct call.");
                        if (GamePlayScript.instance != null) GamePlayScript.instance.OnGoldCollected();
                        Destroy(gameObject);
                    }
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
