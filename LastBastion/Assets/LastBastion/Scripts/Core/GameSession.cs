using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastBastion
{
    public enum Difficulty { Recruit, Warden, Veteran }

    /// <summary>State carried between scenes (chosen level, difficulty, saved stars).</summary>
    public static class GameSession
    {
        public static int SelectedLevel = 1;
        public static Difficulty CurrentDifficulty = Difficulty.Warden;

        public static float EnemyHealthMult
        {
            get
            {
                switch (CurrentDifficulty)
                {
                    case Difficulty.Recruit: return 0.8f;
                    case Difficulty.Veteran: return 1.25f;
                    default: return 1f;
                }
            }
        }

        public static int StartLives => CurrentDifficulty == Difficulty.Veteran ? 10 : 20;
        public static bool EarlyCallBonus => CurrentDifficulty != Difficulty.Veteran;

        static string StarKey(int level, Difficulty d) => "LB_L" + level + "_" + d + "_stars";

        public static int GetStars(int level, Difficulty d) => PlayerPrefs.GetInt(StarKey(level, d), 0);

        public static int BestStars(int level)
        {
            int best = 0;
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
                best = Mathf.Max(best, GetStars(level, d));
            return best;
        }

        public static void SaveStars(int level, Difficulty d, int stars)
        {
            if (stars > GetStars(level, d))
            {
                PlayerPrefs.SetInt(StarKey(level, d), stars);
                PlayerPrefs.Save();
            }
        }
    }

    public static class SceneNames
    {
        public const string Title = "Title";
        public const string LevelSelect = "LevelSelect";
        public const string Level1 = "Level1";
    }

    /// <summary>
    /// Every scene is empty on disk; this router builds the right screen when a scene loads.
    /// That keeps the project merge-friendly (no scene YAML conflicts) and lets the whole game live in code.
    /// </summary>
    public static class SceneRouter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Build(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Build(scene);
        }

        public static void Go(string sceneName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Build(Scene scene)
        {
            switch (scene.name)
            {
                case SceneNames.Title: Ensure<TitleScreen>("Title Screen"); break;
                case SceneNames.LevelSelect: Ensure<LevelSelectScreen>("Level Select"); break;
                case SceneNames.Level1: Ensure<LevelManager>("Level 1 - The Ashen Pass"); break;
                case "SampleScene":
                case "":
                    // Pressing Play in the template scene: jump to the title screen.
                    if (Application.CanStreamedLevelBeLoaded(SceneNames.Title))
                    {
                        SceneManager.LoadScene(SceneNames.Title);
                    }
                    else
                    {
                        Debug.LogWarning("[Last Bastion] Scenes are not in Build Settings yet. Run the menu: Last Bastion > Set Up Scenes and Build Settings.");
                        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
                        Ensure<TitleScreen>("Title Screen");
                    }
                    break;
            }
        }

        static void Ensure<T>(string name) where T : Component
        {
            if (Object.FindAnyObjectByType<T>() == null) new GameObject(name).AddComponent<T>();
        }
    }
}
