using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConfigTransporter : MonoBehaviour
{
    public static ConfigTransporter instance;
    public bool RandomizeColors { get; set; }
    public bool VaryLighting { get; set; }
    public bool AddNoise { get; set; }
    public bool SmoothImages { get; set; }

    private void Awake() {
        if(instance == null) instance = this;
        else if(instance != this) Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
    }

    public void LoadMainScene() {

        SceneManager.LoadScene(1);

    }
}
