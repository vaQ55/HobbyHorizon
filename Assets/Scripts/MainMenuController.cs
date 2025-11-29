using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;   // <--- THÊM DÒNG NÀY

public class MainMenuController : MonoBehaviour {

    public GameObject buttonContinue, soundOnButton, soundMuteButton, musicOnButton, musicMuteButton;
    public Animator animPanelSetting;
    public AudioSource audioMusic;
    public AudioSource audioSound;

    public AudioClip pressButton;

    bool doubleBackToExitPressedOnce = false;
    string toastString;
    AndroidJavaObject currentActivity;
    AndroidJavaClass UnityPlayer;
    AndroidJavaObject context;

    void Start () {
        Time.timeScale = 1;
        CGameManager.instance.DisableItems();

        SoundControl();
        MusicControl();
        SetButtonMusic();
        SetButtonSound();

        buttonContinue.SetActive(PlayerPrefs.GetInt("MaxLevel") != 0);

        if (Application.platform == RuntimePlatform.Android)
        {
            UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            currentActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            context = currentActivity.Call<AndroidJavaObject>("getApplicationContext");
        }
    }
	
	void Update () {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (doubleBackToExitPressedOnce)
            {
                Application.Quit();
            }
            doubleBackToExitPressedOnce = true;

            showToastOnUiThread("Please click BACK again to exit");
            StartCoroutine(DoubleClickExit());
        }
    }

    IEnumerator DoubleClickExit()
    {
        yield return new WaitForSeconds(1.5f);
        doubleBackToExitPressedOnce = false;
    }

    public void showToastOnUiThread(string toastString)
    {
        this.toastString = toastString;
        currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(showToast));
    }

    void showToast()
    {
        Debug.Log(this + ": Running on UI thread");

        AndroidJavaClass Toast = new AndroidJavaClass("android.widget.Toast");
        AndroidJavaObject javaString = new AndroidJavaObject("java.lang.String", toastString);
        AndroidJavaObject toast = Toast.CallStatic<AndroidJavaObject>("makeText", context, javaString, Toast.GetStatic<int>("LENGTH_SHORT"));
        toast.Call("show");
    }

    public void Play()
    {
        audioSound.PlayOneShot(pressButton);
        CGameManager.instance.levelCurrent = 1;
        PlayerPrefs.SetInt("Bomb", 0);
        PlayerPrefs.SetInt("MaxLevel", 0);
        PlayerPrefs.SetInt("MaxDollar", 0);

        // ---- FIXED ----
        SceneManager.LoadScene("NextLevel", LoadSceneMode.Single);
    }

    public void PlayContinue()
    {
        audioSound.PlayOneShot(pressButton);
        int maxLevel = PlayerPrefs.GetInt("MaxLevel");

        if(maxLevel == 0)
        {
            CGameManager.instance.levelCurrent = 1;
            Play();
        }
        else
        {
            CGameManager.instance.levelCurrent = maxLevel + 1;

            // ---- FIXED ----
            SceneManager.LoadScene("Shop", LoadSceneMode.Single);
        }
    }
    
    public void Setting()
    {
        audioSound.PlayOneShot(pressButton);
        animPanelSetting.SetBool("In", true);
        animPanelSetting.SetBool("Out", false);
    }
    public void ExitSetting()
    {
        audioSound.PlayOneShot(pressButton);
        animPanelSetting.SetBool("Out", true);
    }

    void SoundControl()
    {
        audioSound.enabled = PlayerPrefs.GetInt("Sound") == 1;
    }
    void MusicControl()
    {
        audioMusic.enabled = PlayerPrefs.GetInt("Music") == 1;
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
        bool on = PlayerPrefs.GetInt("Sound") == 1;
        soundOnButton.SetActive(on);
        soundMuteButton.SetActive(!on);
    }

    private void SetButtonMusic()
    {
        bool on = PlayerPrefs.GetInt("Music") == 1;
        musicOnButton.SetActive(on);
        musicMuteButton.SetActive(!on);
    }
}
