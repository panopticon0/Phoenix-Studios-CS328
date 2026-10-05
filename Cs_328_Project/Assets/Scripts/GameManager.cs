using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int Gold { get; private set; } = 150;
    public int Lives { get; private set; } = 10;
    public int Wave, TotalWaves;
    public bool GameOver { get; private set; }
    bool won;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    public void AddGold(int amount) => Gold += amount;

    public bool TrySpend(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }

    public void LoseLife(int amount)
    {
        Lives -= amount;
        if (Lives <= 0) { Lives = 0; EndGame(false); }
    }

    public void Victory() => EndGame(true);

    void EndGame(bool victory)
    {
        if (GameOver) return;
        GameOver = true;
        won = victory;
        Time.timeScale = 0f;
    }

    void Update()
    {
        if (GameOver && Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        s.normal.textColor = Color.white;
        GUI.Label(new Rect(10, 8, 600, 30), $"Gold: {Gold}     Lives: {Lives}     Wave: {Wave}/{TotalWaves}", s);

        if (!GameOver) return;
        var big = new GUIStyle(s) { fontSize = 52, alignment = TextAnchor.MiddleCenter };
        big.normal.textColor = won ? Color.yellow : new Color(1f, 0.3f, 0.3f);
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), won ? "VICTORY!" : "THE KEEP HAS FALLEN", big);
        var small = new GUIStyle(s) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0, 80, Screen.width, Screen.height), "Press R to restart", small);
    }
}
