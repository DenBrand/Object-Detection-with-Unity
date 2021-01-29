using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConfigTransporter : MonoBehaviour
{
    public static ConfigTransporter instance;
    private bool randomizeColors;
    private bool varyLighting;
    private bool addNoise;
    private bool smoothImages;

    private void Start() {

        randomizeColors = false;
        varyLighting = false;
        addNoise = false;
        smoothImages = false;

    }

    private void Awake() {
        if(instance == null) instance = this;
        else if(instance != this) Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
    }

    public void LoadMainScene() {

        SceneManager.LoadScene(1);

    }
}
