using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LastBastion.EditorTools
{
    /// <summary>
    /// One-time project setup. Runs automatically when the editor loads (and from the menu
    /// Last Bastion > Set Up Scenes and Build Settings):
    ///  - creates the empty Title, LevelSelect and Level1 scenes (the game builds itself from code),
    ///  - puts them first in Build Settings so a build starts on the title screen,
    ///  - creates an unlit sprite material that matches the render pipeline (URP or Built-in).
    /// </summary>
    [InitializeOnLoad]
    public static class LastBastionSetup
    {
        const string Root = "Assets/LastBastion";
        const string ScenesDir = Root + "/Scenes";
        const string ResourcesDir = Root + "/Resources";
        const string MaterialPath = ResourcesDir + "/LB_SpriteUnlit.mat";
        static readonly string[] SceneNamesInOrder = { "Title", "LevelSelect", "Level1" };

        static LastBastionSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static string ScenePath(string name) => ScenesDir + "/" + name + ".unity";

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (NeedsSetup()) Run(true);
        }

        static bool NeedsSetup()
        {
            if (!File.Exists(MaterialPath)) return true;
            var inBuild = new HashSet<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) inBuild.Add(s.path);
            foreach (var n in SceneNamesInOrder)
            {
                string p = ScenePath(n);
                if (!File.Exists(p) || !inBuild.Contains(p)) return true;
            }
            return false;
        }

        [MenuItem("Last Bastion/Set Up Scenes and Build Settings")]
        static void MenuSetup()
        {
            Run(true);
            EditorUtility.DisplayDialog("Last Bastion", "Scenes, Build Settings and the sprite material are ready.\n\nOpen Assets/LastBastion/Scenes/Title and press Play.", "OK");
        }

        [MenuItem("Last Bastion/Open Title Scene")]
        static void OpenTitle()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath("Title"), OpenSceneMode.Single);
        }

        static void Run(bool openTitleIfIdle)
        {
            Directory.CreateDirectory(ScenesDir);
            Directory.CreateDirectory(ResourcesDir);
            AssetDatabase.Refresh();

            EnsureMaterial();
            bool created = EnsureScenes();
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();

            if (created && openTitleIfIdle)
            {
                var active = SceneManager.GetActiveScene();
                bool idle = string.IsNullOrEmpty(active.path) || active.name == "SampleScene";
                if (idle && !active.isDirty) EditorSceneManager.OpenScene(ScenePath("Title"), OpenSceneMode.Single);
            }
            UnityEngine.Debug.Log("[Last Bastion] Setup complete: scenes in " + ScenesDir + ", Build Settings updated, sprite material at " + MaterialPath + ".");
        }

        static void EnsureMaterial()
        {
            Shader shader = null;
            if (GraphicsSettings.currentRenderPipeline != null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                UnityEngine.Debug.LogWarning("[Last Bastion] Could not find a sprite shader; sprites will use the default material.");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "LB_SpriteUnlit" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
        }

        static bool EnsureScenes()
        {
            bool createdAny = false;
            // If an untitled scene is open, additive scene creation is not allowed, so use Single mode.
            bool untitledOpen = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) untitledOpen = true;

            foreach (var n in SceneNamesInOrder)
            {
                string path = ScenePath(n);
                if (File.Exists(path)) continue;

                if (untitledOpen)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return createdAny;
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(scene, path);
                    untitledOpen = false;
                }
                else
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    EditorSceneManager.SaveScene(scene, path);
                    EditorSceneManager.CloseScene(scene, true);
                }
                createdAny = true;
            }
            if (createdAny) AssetDatabase.Refresh();
            return createdAny;
        }

        static void EnsureBuildSettings()
        {
            var ours = new List<EditorBuildSettingsScene>();
            foreach (var n in SceneNamesInOrder) ours.Add(new EditorBuildSettingsScene(ScenePath(n), true));

            var others = new List<EditorBuildSettingsScene>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                bool isOurs = false;
                foreach (var n in SceneNamesInOrder) if (s.path == ScenePath(n)) isOurs = true;
                if (!isOurs) others.Add(s);
            }
            ours.AddRange(others);
            EditorBuildSettings.scenes = ours.ToArray();
        }
    }
}
