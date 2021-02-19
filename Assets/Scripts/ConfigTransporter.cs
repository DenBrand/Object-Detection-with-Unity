using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public class ConfigTransporter : MonoBehaviour
{
    public static ConfigTransporter instance;
    [SerializeField] Text modeButtonText = null;
    [SerializeField] Text modeDescriptionText = null;
    public List<Mode> modes;
    public Mode currentMode { get; set; }
    public bool saveLabeledImages { get; set; }
    private int currentModeIdx;

    private void Awake() {
        if(instance == null) instance = this;
        else if(instance != this) Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
    }

    private void Start() {
        saveLabeledImages = true;

        if(modes.Count == 0) {
            Debug.LogError("There are no modes given in the ConfigTransporter.");
            throw new MissingReferenceException("There are no modes given in the ConfigTransporter.");
        }

        currentMode = modes[0]; // STANDARD mode is default mode
        currentModeIdx = 0;
        modeButtonText.text = currentMode.ButtonText;
        modeDescriptionText.text = currentMode.Description;
    }

    private void Update() {
        if(Input.GetKeyDown(KeyCode.Escape)) {
            Application.Quit();
        }
    }

    public void NextMode() {
        if(currentMode == modes[modes.Count - 1]) {
            currentMode = modes[0];
            currentModeIdx = 0;
        }
        else {
            currentMode = modes[currentModeIdx + 1];
            currentModeIdx++;
        }

        modeButtonText.text = currentMode.ButtonText;
        modeDescriptionText.text = currentMode.Description;
    }

    public void LoadMainScene() { SceneManager.LoadScene(1); }
}