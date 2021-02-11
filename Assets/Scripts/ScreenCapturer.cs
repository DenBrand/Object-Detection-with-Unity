using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenCapturer : MonoBehaviour
{
    [SerializeField] Canvas UICanvas = null;
    [SerializeField] Camera playerCamera = null;
    [SerializeField] [Range(30, 100)] int numberOfDetectables = 50;
    [SerializeField] [Range(30, 50)] int minimalSize = 30;
    [SerializeField] [Range(1, 5)] int paddingForCascadeClassifier = 3;
    [SerializeField] GameObject detectablesParent = null;
    [SerializeField] List<GameObject> detectablesPrefabs = null;
    [SerializeField] ConfigTransporter configTransporter = null;
    private List<GameObject> detectables = null;
    [SerializeField] [Range(3, 15)] int maxMessages = 10;
    private List<Message> messageList = new List<Message>();
    [SerializeField] [Range(-50f, 0f)] float deathHeight = -30f;
    [SerializeField] GameObject player;

    bool allowCapturing;

    // REPLACE WITH VARIABLE FROM configTransporter LATER
    bool allowEmptyCaptures;

    // paths to store gathered data
    string trainingDataPath;
    string yoloDataPath;
    string cascadeClassifierDataPath;
    string cascadeClassifierDataJSONPath;

    void Awake() {
        // fill detectablesContainer with random detectables
        detectables = new List<GameObject>()

        for(int i = 0; i < numberOfDetectables; i++) {
            GameObject chosenDetectable = detectablePrefabs[i % detectablesPrefabs.Count];
            Vector3 randomPosition = new Vector3(   UnityEngine.Random.Range(-100f, 100f),
                                                    10f,
                                                    UnityEngine.Random.Range(-100f, 100f));

            GameObject newDetectable = Instantiate(chosenDetectable,
                                        randomPosition,
                                        Quaternion.Euler(   UnityEngine.Random.Range(0f, 180f),
                                                            UnityEngine.Random.Range(0f, 180f),
                                                            UnityEngine.Random.Range(0f, 180f)),
                                        detectablesParent.transform);

            detectables.Add(newDetectable);
        }
    }

    void Start()
    {
        // get the configTransporter
        try {
            configTransporter = GameObject.Find("ConfigTransporter").GetComponent<ConfigTransporter>();
        } catch(Exception ex) {
            Debug.LogError(ex);
        }

        Cursor.visible = false;
        allowCapturing = true;
        allowEmptyCaptures = true;

        // specify certain paths
        if(SystemInfo.operatingSystem.StartsWith("Windows")) {
            trainingDataPath = "training_data\\";
            yoloDataPath = trainingDataPath + "yolo\\";
            cascadeClassifierDataPath = trainingDataPath + "cascade_classifier\\";
            cascadeClassifierDataJSONPath = cascadeClassifierDataPath + "training_data.json";
        }
        else {
            trainingDataPath = "training_data/";
            yoloDataPath = trainingDataPath + "yolo/";
            cascadeClassifierDataPath = trainingDataPath + "cascade_classifier/";
            cascadeClassifierDataJSONPath = cascadeClassifierDataPath + "training_data.json";
        }

        sendMessage("Move: WASD");
        sendMessage("Look: Mouse");
        sendMessage("Capture: E");
        sendMessage("Zoom: Scroll Mouse Wheel");
        sendMessage("Hide Info Box: H");
        sendMessage("Toggle capture empty images: P (default: <color=green>ON</color>)");
        sendMessage("Close Application: ESC");
        sendMessage("<color=red>Wait at least one second between captures.</color>");
        sendMessage("Training data are saved in \"" + trainingDataPath + "\".");
        sendMessage("<color=green>STARTING CAPTURING SESSION</color>");
    }

    void Update()
    {
        // reposition if fallen out of bounds
        if(player.transform.position.y <= deathHeight) {
            sendMessage("<color=red>You fell out of bounds. I reset you to the spawn location.</color>");
            player.transform.position = Vector3.zero;
        }

        // reload title scene when ESC is pressed
        if(Input.GetKeyDown(KeyCode.Escape)) configTransporter.LoadTitleScene();

        // start capturing routine
        if(Input.GetKeyDown(KeyCode.E)) {
            if(allowCapturing) {
                allowCapturing = false;

                // start coroutine so hide UI for the frame the screen is captured
                StartCoroutine(CaptureScreen());

                StartCoroutine(AllowCapturingAfterOneSecond(1.1f));
            } else {
                sendMessage("<color=red>Nothing captured. Please wait at least one second. That's because the " +
                            "images are named with a timestamp messured in seconds. Multiple captures " +
                            "within the same second could lead to overwritten images and faulty labels.</color>");
            }
        }
    }

    IEnumerator CaptureScreen() {
        // get time stamp as file name
        System.DateTime time = System.DateTime.Now;
        string timestmp = time.Year + "-"
                        + time.Month + "-"
                        + time.Day + "_"
                        + time.Hour + "h"
                        + time.Minute + "min"
                        + time.Second + "sec";

        int randomNumber = UnityEngine.Random.Range(0, 10000);

        // filter out illegal objects
        // TODO
    }

    // extra mechanism to prevent overwriting images
    IEnumerator AllowCapturingAfterOneSecond(float n) {
        yield return new WaitForSecondsRealtime(n);
        allowCapturing = true;
    }

    public void sendMessage(string text) {
        if(messageList.Count >= maxMessages) {
            Destroy(messageList[0].textObject.gameObject);
            messageList.Remove(messageList[0]);

        }

        Message newMessage = new Message();
        newMessage.text = text;
        GameObject newText = Instantiate(textObject, contentObject.transform);
        newMessage.textObject = newText.GetComponent<Text>();
        newMessage.textObject.text = newMessage.text;
        messageList.Add(newMessage);
    }
}
