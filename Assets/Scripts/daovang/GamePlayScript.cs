using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class GamePlayScript : MonoBehaviour {
    public static GamePlayScript instance;
    public Text timeText, levelText, targetText, scoreText;
    public Text boomText;
    public Text scoreVictoryText, scoreFailText;
    public int score, scoreTarget;
    private int time;
    private float countDown;
    public GameObject panelMenu, panelVictory, panelFail, soundOnButton, soundMuteButton, musicOnButton, musicMuteButton;
    public Animator animPanelMenu, animPanelDark, animPanelPlay;
    public Button restartGame, restartFailPanel;
    public int level;
    public bool endgame = false;

    public GameObject scoreTextFly, boomFly, boomObject, buttonNextLevel;
    public Transform canvas;

    public int numberBoom;
    public string itemSeclected;
    private GameObject itemDestroy, fireObject;

    private bool nextLevel = false;

    public AudioSource audioMusic;
    public AudioSource audioSound;
    public AudioClip pressButton, explosive, lowValue, normalValue, highValue, last10S, pull, lose, win;

    bool victory, fail, isPause;

    [HideInInspector]
    public int goldCollected = 0;
    public int goldTarget = 3;
    public Text goldCountText;

    [HideInInspector]
    public bool pickGold = false;

    [HideInInspector]
    public int grabCount = 0;
    public int maxGrab = 3;

    void Start () {
        MakeInstance();

        score = PlayerPrefs.GetInt("MaxDollar");
        scoreText.text = "$" + score;
        level = 0;

        goldCollected = 0;
        pickGold = false;
        grabCount = 0;
        UpdateGoldUI();

        levelText.text = "LEVEL " + CGameManager.instance.levelCurrent;
        scoreTarget = CGameManager.instance.GetScoreTarget(CGameManager.instance.levelCurrent);
        targetText.text = "$" + scoreTarget.ToString();

        if (CGameManager.instance.clock) countDown = 76;
        else countDown = 61;

        SoundControl();
        MusicControl();
        SetButtonMusic();
        SetButtonSound();
        SetNumberBoom();
    }

    void MakeInstance()
    {
        if (instance == null) instance = this;
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.Escape)) PauseGame();

        countDown -= UnityEngine.Time.deltaTime;
        time = (int)countDown;
        if (time > 0)
        {
            timeText.text = time.ToString();
            if(time < 10)
            {
                if(!(victory || fail)) PlaySound(6);
            }
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause && !isPause)
        {
            if (!(victory && fail)) PauseGame();
        }
    }

    public void PlaySound(int i)
    {
        switch (i)
        {
            case 1: audioSound.PlayOneShot(lowValue); break;
            case 2: audioSound.PlayOneShot(normalValue); break;
            case 3: audioSound.PlayOneShot(highValue); break;
            case 4: audioSound.PlayOneShot(explosive); break;
            case 5:
                if (!audioSound.isPlaying && !(victory || fail)) audioSound.PlayOneShot(pull);
                break;
            case 6:
                if (!audioSound.isPlaying) audioSound.PlayOneShot(last10S);
                break;
            case 7: audioSound.PlayOneShot(lose); break;
            case 8: audioSound.PlayOneShot(win); break;
            case 9: audioSound.PlayOneShot(pressButton); break;
        }
    }

    public void PauseGame()
    {
        isPause = true;
        audioSound.enabled = false;
        animPanelDark.SetBool("In", true);
        animPanelMenu.SetBool("In", true);
        animPanelMenu.SetBool("Out", false);
        UnityEngine.Time.timeScale = 0;
        restartGame.onClick.RemoveAllListeners();
        restartGame.onClick.AddListener(() => RestartGame());
    }

    public void ResumeGame()
    {
        isPause = false;
        audioSound.enabled = true;
        PlaySound(9);
        UnityEngine.Time.timeScale = 1;
        animPanelDark.SetBool("In", false);
        animPanelMenu.SetBool("Out", true);
    }

    public void Victory()
    {
        if (victory) return;
        audioMusic.enabled = false;
        PlaySound(8);
        victory = true;
        UnityEngine.Time.timeScale = 0;
        panelVictory.SetActive(true);
        scoreVictoryText.text = "$" + score;
        PlayerPrefs.SetInt("MaxLevel", CGameManager.instance.levelCurrent);
        PlayerPrefs.SetInt("MaxDollar", score);
    }

    public void Fail()
    {
        if (fail) return;
        audioMusic.enabled = false;
        PlaySound(7);
        fail = true;
        UnityEngine.Time.timeScale = 0;
        panelFail.SetActive(true);
        scoreFailText.text = "$" + score;
        restartFailPanel.onClick.RemoveAllListeners();
        restartFailPanel.onClick.AddListener(() => RestartGame());
    }

    public void RestartGame()
    {
        UnityEngine.Time.timeScale = 1;
        CGameManager.instance.powerCurrent = false;
        goldCollected = 0;
        pickGold = false;
        grabCount = 0;
        PlayerPrefs.SetInt("Bomb", PlayerPrefs.GetInt("Bomb"));
        Application.LoadLevel(Application.loadedLevel);
    }

    public void NextLevel()
    {
        CGameManager.instance.DisableItems();
        goldCollected = 0;
        pickGold = false;
        grabCount = 0;
        Application.LoadLevel("Shop");
        CGameManager.instance.levelCurrent++;
    }

    public void NextLevelAndSave()
    {
        PlayerPrefs.SetInt("MaxLevel", CGameManager.instance.levelCurrent);
        PlayerPrefs.SetInt("MaxDollar", score);
        CGameManager.instance.DisableItems();
        goldCollected = 0;
        pickGold = false;
        grabCount = 0;
        Application.LoadLevel("Shop");
        CGameManager.instance.levelCurrent++;
    }

    public void BackToMenu()
    {
        pickGold = false;
        grabCount = 0;
        Application.LoadLevel("MainMenu");
    }

    public void Boom()
    {
        OngGiaScript.instance.DropBomb();
        itemDestroy = GameObject.Find(itemSeclected);
        Vector3 vector3 = itemDestroy.transform.localPosition;
        PlaySound(4);

        if(itemDestroy.tag == "Gold")
        {
            fireObject = Instantiate(Resources.Load("GoldFire"), vector3, Quaternion.identity) as GameObject;
            fireObject.transform.localScale = itemDestroy.transform.localScale;
        }
        else if(itemDestroy.tag == "Stone")
        {
            fireObject = Instantiate(Resources.Load("StoneFire"), vector3, Quaternion.identity) as GameObject;
            fireObject.transform.localScale = itemDestroy.transform.localScale;
        }
        else
        {
            fireObject = Instantiate(Resources.Load("OtherFire"), vector3, Quaternion.identity) as GameObject;
        }

        Destroy(itemDestroy);
        numberBoom = PlayerPrefs.GetInt("Bomb");
        numberBoom--;
        PlayerPrefs.SetInt("Bomb", numberBoom);
        SetNumberBoom();
        if(GameObject.Find("dayCau").GetComponent<DayCauScript>().typeAction == TypeAction.KeoCau)
        {
            LuoiCauScript.instance.speed = 4;
        }
    }

    public void Power()
    {
        CGameManager.instance.powerCurrent = true;
        OngGiaScript.instance.Happy();
    }

    public void SetNumberBoom()
    {
        if (PlayerPrefs.GetInt("Bomb") > 0)
        {
            boomObject.SetActive(true);
            boomText.text = PlayerPrefs.GetInt("Bomb").ToString();
        }
        else
        {
            boomObject.SetActive(false);
        }
    }

    public void CreateScoreFly(int score)
    {
        Vector3 vector3 = scoreTextFly.transform.position;
        Instantiate(scoreTextFly, vector3, Quaternion.identity).transform.SetParent(canvas, false);
        TextScoreScript.score = score;
    }

    public void ScoreZoomEffect()
    {
        animPanelPlay.SetBool("Zoom", true);
        StartCoroutine(ScoreZoomOut());
    }
    IEnumerator ScoreZoomOut()
    {
        yield return new WaitForSeconds(1f);
        animPanelPlay.SetBool("Zoom", false);
    }
    public void CreateBoomFly()
    {
        Vector3 vector3 = boomFly.transform.position;
        Instantiate(boomFly, vector3, Quaternion.identity).transform.SetParent(canvas, false);
    }

    public void SetScoreText()
    {
        scoreText.text = "$" + score.ToString();
    }

    public void OnGoldCollected(int goldId = -1)
    {
        goldCollected++;
        grabCount++;

        if (goldId >= 0 && GoldManager.Instance != null)
        {
            GoldManager.Instance.OnGoldPicked(goldId);
        }

        UpdateGoldUI();
        Debug.Log($"Gold collected: {goldCollected} | grabCount: {grabCount} | pickGold: {pickGold}");

        if (grabCount >= maxGrab)
        {
            EvaluatePickResult();
        }
    }

    public void EvaluatePickResult(bool withDelay = true, float delaySeconds = 0.5f)
    {
        if (victory || fail) return;

        if (pickGold)
        {
            if (withDelay) StartCoroutine(DelayedVictory(delaySeconds));
            else Victory();
        }
        else
        {
            Fail();
        }
    }

    IEnumerator DelayedVictory(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
        Victory();
    }

    void UpdateGoldUI()
    {
        if (goldCountText != null)
        {
            goldCountText.text = goldCollected.ToString() + "/" + goldTarget.ToString();
        }
    }

    void SoundControl()
    {
        audioSound.enabled = (PlayerPrefs.GetInt("Sound") == 1);
    }
    void MusicControl()
    {
        audioMusic.enabled = (PlayerPrefs.GetInt("Music") == 1);
    }
    public void SetOnMusic()
    {
        PlayerPrefs.SetInt("Music", 1);
        SetButtonMusic();
        MusicControl();
    }
    public void SetOnSound()
    {
        PlayerPrefs.SetInt("Sound", 1);
        SetButtonSound();
        SoundControl();
    }
    public void SetMuteMusic()
    {
        PlayerPrefs.SetInt("Music", 0);
        SetButtonMusic();
        MusicControl();
    }
    public void SetMuteSound()
    {
        PlayerPrefs.SetInt("Sound", 0);
        SetButtonSound();
        SoundControl();
    }
    private void SetButtonSound()
    {
        if (PlayerPrefs.GetInt("Sound") == 1) { soundOnButton.SetActive(true); soundMuteButton.SetActive(false); }
        else { soundOnButton.SetActive(false); soundMuteButton.SetActive(true); }
    }
    private void SetButtonMusic()
    {
        if (PlayerPrefs.GetInt("Music") == 1) { musicOnButton.SetActive(true); musicMuteButton.SetActive(false); }
        else { musicMuteButton.SetActive(true); musicOnButton.SetActive(false); }
    }
}
