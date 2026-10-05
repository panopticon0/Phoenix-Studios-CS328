using UnityEngine;

/// Attach this to ONE empty GameObject in an empty scene. It sets up the camera and creates everything else.
public class Level1Bootstrap : MonoBehaviour
{
    void Awake()
    {
        var cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = new Color(0.08f, 0.1f, 0.08f);

        new GameObject("GameManager").AddComponent<GameManager>();
        new GameObject("Map").AddComponent<MapBuilder>();
        new GameObject("BuildManager").AddComponent<BuildManager>();
        new GameObject("WaveSpawner").AddComponent<WaveSpawner>();
    }
}
