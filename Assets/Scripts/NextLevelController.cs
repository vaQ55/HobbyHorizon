using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;   // <--- THÊM DÒNG NÀY

public class NextLevelController : MonoBehaviour {

    public Text textTarget, textLevel;
    float timeDelay;

	void Start () {
        textTarget.text = "$" + CGameManager.instance.GetScoreTarget(CGameManager.instance.levelCurrent).ToString();
        textLevel.text = "LEVEL " + CGameManager.instance.levelCurrent;
	}
	
	void Update () {
        timeDelay += Time.deltaTime * 10;
        if(timeDelay > 25)
        {
            int level = CGameManager.instance.levelCurrent;

            if(level <= 10)
            {
                SceneManager.LoadScene("Level" + level, LoadSceneMode.Single);
            }
            else
            {
                int ran = Random.Range(1, 10);
                SceneManager.LoadScene("Level" + ran, LoadSceneMode.Single);
            }
        }
	}
}
