using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

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
    List<GameObject> detectables = null;
    [SerializeField] [Range(3, 15)] int maxMessages = 10;
    List<Message> messageList = new List<Message>();
    [SerializeField] [Range(-50f, 0f)] float deathHeight = -30f;
    [SerializeField] GameObject player = null;
    [SerializeField] GameObject contentObject = null;
    [SerializeField] GameObject textObject = null;
    CascadeClassifierData cascadeClassifierData;
    RunData runData;
    string runId;

    bool allowCapturing;

    // REPLACE WITH VARIABLE FROM configTransporter LATER
    bool allowEmptyCaptures;

    // paths to store gathered data
    string trainingDir;
    string yoloDir;
    string cascadeClassifierDir;
    string cascadeClassifierJSONPath;
    string modeDir;

    void Awake() {
        // fill detectablesContainer with random detectables
        detectables = new List<GameObject>();

        for(int i = 0; i < numberOfDetectables; i++) {
            GameObject chosenDetectable = detectablesPrefabs[i % detectablesPrefabs.Count];
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

        // define certain paths and folder name of this mode
        if(SystemInfo.operatingSystem.StartsWith("Windows")) {
            trainingDir = "training_data\\";
            yoloDir = "yolo\\";
            cascadeClassifierDir = "cascade_classifier\\";
            modeDir = "standard\\";
        }
        else {
            trainingDir = "training_data/";
            yoloDir = "yolo/";
            cascadeClassifierDir = "cascade_classifier/";
            modeDir = "standard/";
        }
        cascadeClassifierJSONPath = cascadeClassifierDir + "training_data.json";

        // Load cascade classifier data already existent
        if(!Directory.Exists(trainingDir + modeDir + cascadeClassifierDir)) {
            Directory.CreateDirectory(trainingDir + modeDir + cascadeClassifierDir);
        }
        if(File.Exists(trainingDir + modeDir + cascadeClassifierJSONPath)) {
            string jsonString = File.ReadAllText(trainingDir + modeDir + cascadeClassifierJSONPath);
            cascadeClassifierData = JsonUtility.FromJson<CascadeClassifierData>(jsonString);
        }
        else {
            cascadeClassifierData = new CascadeClassifierData();
        }

        // add new RunData entry
        runId = string.Format("{0}_{1}", Environment.UserName.GetHashCode(), UnityEngine.Random.Range(0, 10000));
        string version = Application.version;
        runData = new RunData(runId, configTransporter.currentMode.ModeName, version);
        cascadeClassifierData.runData.Add(runData);

        // send initial messages to the player
        sendMessage("Move: WASD");
        sendMessage("Look: Mouse");
        sendMessage("Capture: E");
        sendMessage("Zoom: Scroll Mouse Wheel");
        sendMessage("Hide Info Box: H");
        sendMessage("Toggle capture empty images: P (default: <color=green>ON</color>)");
        sendMessage("Close Application: ESC");
        sendMessage("<color=red>Wait at least one second between captures.</color>");
        sendMessage("Training data are saved in \"" + trainingDir + "\".");
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
        if(Input.GetKeyDown(KeyCode.Escape)) Application.Quit();

        // start capturing routine
        if(Input.GetKeyDown(KeyCode.E)) {
            if(allowCapturing) {
                allowCapturing = false;

                // start coroutine so hide UI for the frame the screen is captured
                StartCoroutine(CaptureScreen());

                StartCoroutine(AllowCapturingAfterOneSecond(1.1f));
            } else {
                sendMessage("<color=red>Nothing captured. Please wait at least one second. That's because the "
                            + "images are named with a timestamp messured in seconds. Multiple captures "
                            + "within the same second could lead to overwritten images and faulty labels.</color>");
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

        // perform capture accoringly to the mode
        switch(configTransporter.currentMode.ModeName) {
            case "STANDARD":
                return StandardCapture();
            case "RANDOMIZE_DETECTABLE_COLORS":
                return RandomizedDetectableColorsCapture();
            default:
                throw new ArgumentException("Could not resolve mode. ModeName given in configTransporter: "
                                            + configTransporter.currentMode.ModeName + ".");
        }

        IEnumerator StandardCapture() {
            // check if screenshot with this name was already taken
            if(!File.Exists(trainingDir + modeDir + yoloDir + timestmp + randomNumber + ".png")) {
                sendMessage("*SNAP*");

                // wait till last possible moment before rendering to hide UI
                bool canvasWasEnabled = UICanvas.enabled;
                yield return null;
                UICanvas.enabled = false;

                // wait for screen rendering to complete
                yield return new WaitForEndOfFrame();
                if(canvasWasEnabled) UICanvas.enabled = true;

                // take raw screenshot
                Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] screenshotAsPNG = screenshot.EncodeToPNG();

                // filter out unseen detectables
                bool allSizesSufficient = true;
                List<BoxData> boxDataList = new List<BoxData>();
                List<PositiveElement> positiveElements = new List<PositiveElement>();
                foreach(GameObject detectable in detectables) {
                    // is detectable seen by any camera? (first filter)
                    if(detectable.GetComponent<Renderer>().isVisible) {
                        // check for every vertex, if it is visible
                        Vector3[] vertices = detectable.GetComponent<MeshFilter>().mesh.vertices;

                        // transform its coords into world space
                        for(int i = 0; i < vertices.Length; i++) {
                            vertices[i] = detectable.transform.TransformPoint(vertices[i]);
                        }

                        // find horizontal and vertical extents of the object
                        float left = Screen.width + 1f;
                        float right = -1f;
                        float top = -1f;
                        float bottom = Screen.height + 1f;
                        bool objectInSight = false;
                        foreach(Vector3 vertex in vertices) {
                            // check if vertex is IN FRONT of the camera
                            Vector3 distanceVector = vertex - playerCamera.transform.position;
                            if(Vector3.Dot(playerCamera.transform.forward, distanceVector) > 0f) {
                                // check if corresponding screen point is on screen and if it is a newly found border point candidate
                                Vector3 screenPoint = playerCamera.WorldToScreenPoint(vertex);
                                if(new Rect(0, 0, Screen.width, Screen.height).Contains(screenPoint)) {
                                    // check if vertex is obscured by another object

                                    RaycastHit hitInfo;
                                    if(Physics.Raycast(playerCamera.transform.position,
                                                    distanceVector,
                                                    out hitInfo,
                                                    distanceVector.magnitude + 0.01f)) {

                                        if(hitInfo.collider.gameObject == detectable) {
                                            // vertex is in sight => detectable is in sight
                                            objectInSight = true;

                                            // find most horizontally or vertically extent vertex
                                            if(screenPoint.x < left) left = screenPoint.x;
                                            if(screenPoint.x > right) right = screenPoint.x;
                                            if(screenPoint.y < bottom) bottom = screenPoint.y;
                                            if(screenPoint.y > top) top = screenPoint.y;
                                        }
                                    }
                                }
                            }
                        }
                        // every vertex was checked

                        // determine detectable class
                        int classId;
                        if(detectable.name.StartsWith("Cube")) classId = 0;
                        else if(detectable.name.StartsWith("Ball")) classId = 1;
                        // else if(detectable.name.StartsWith("Tetraeder")) classId = 2; // add new detectable class
                        else {
                            Debug.LogError("Couldn't infer detectable class");
                            throw new System.Exception("Detected an object whose id cannot be infered. Unknown detectable class.");
                        }

                        // add object to data
                        if(objectInSight) {

                            // check if object is at least minimalSize pixels wide and high
                            bool tooSmall = false;
                            float width = right - left;
                            float height = top - bottom;
                            bool wasDetected = left != Screen.width + 1f && right != -1f && top != -1f && bottom != Screen.height + 1f;
                            if((Mathf.RoundToInt(width) < minimalSize || Mathf.RoundToInt(height) < minimalSize) && wasDetected)
                                tooSmall = true;

                            if(!tooSmall) {

                                // represent detected objects in the info box
                                string name = detectable.name;
                                if(detectable.name.Contains("Cube")) name = "<color=red>" + detectable.name + "</color>";
                                else if(detectable.name.Contains("Ball")) name = "<color=cyan>" + detectable.name + "</color>";
                                // else if(detectable.name.Contains("Tetraeder")) name = "<color=magenta>" + detectable.name + "</color>";
                                else throw new ArgumentOutOfRangeException("Could not resolve detectable class while writing it in the infobox.");
                                sendMessage(name + "(id: " + detectable.GetInstanceID() + ") snapped");

                                // draw boundary box
                                for(int x = (int)left; x <= (int)right; x++) {
                                    screenshot.SetPixel(x, (int)bottom, Color.red);
                                    screenshot.SetPixel(x, (int)top, Color.red);
                                }
                                for(int y = (int)bottom; y <= (int)top; y++) {
                                    screenshot.SetPixel((int)left, y, Color.red);
                                    screenshot.SetPixel((int)right, y, Color.red);
                                }

                                // gather corresponding data

                                // for yolo
                                BoxData boxData = new BoxData(  classId,
                                                                Mathf.RoundToInt(left),
                                                                Mathf.RoundToInt(Screen.height - top),
                                                                width,
                                                                height);
                                boxDataList.Add(boxData);

                                // add to positives
                                int cascadeLeft = Mathf.RoundToInt(left) - paddingForCascadeClassifier;
                                if(cascadeLeft < 0) cascadeLeft = 0;
                                int cascadeTop = Mathf.RoundToInt(Screen.height - top) - paddingForCascadeClassifier;
                                if(cascadeTop < 0) cascadeTop = 0; 
                                int cascadeWidth = Mathf.RoundToInt(width) + 2 * paddingForCascadeClassifier;
                                if(cascadeLeft + cascadeWidth > Screen.width) cascadeWidth = Screen.width - cascadeLeft;
                                int cascadeHeight = Mathf.RoundToInt(height) + 2 * paddingForCascadeClassifier;
                                if(cascadeTop + cascadeHeight > Screen.height) cascadeHeight = Screen.height - cascadeTop;

                                positiveElements.Add(new PositiveElement(   classId,
                                                                            "positives\\" + timestmp + randomNumber + ".png",
                                                                            new BBox(cascadeLeft, cascadeTop, cascadeWidth, cascadeHeight)));

                            }
                            else allSizesSufficient = false;
                        }
                    }
                }
                // all detectable in the scene where checked

                if(allSizesSufficient) {

                    // add cascade classifier data and for every class,
                    // check if there is a positive element for that class.
                    // if no: add image as negative element for that class.
                    List<NegativeElement> negativeElements = new List<NegativeElement>();
                    Dictionary<int, bool> classFound = new Dictionary<int, bool>();
                    classFound.Add(0, false);
                    classFound.Add(1, false);
                    foreach(PositiveElement posElem in positiveElements) {

                        if(posElem.classId == 0) {
                            classFound[0] = true;
                            runData.cubes.positives.Add(new PositivesData(posElem.path, posElem.bbox));
                        }
                        else if(posElem.classId == 1) {
                            classFound[1] = true;
                            runData.balls.positives.Add(new PositivesData(posElem.path, posElem.bbox));
                        }
                        /*else if(posElem.classId == 2) { // add new detectable class
                            classFound[2] = true;
                            runData.tetraeders.positives.Add(new PositivesData(posElem.path, posElem.bbox));
                        }*/
                        else {
                            Debug.LogError("The script added a non-existent detectable class to the \"positiveElements\" variable. This error is fatal. Please inform Dennis about this.");
                            throw new System.Exception("The script added a non-existent detectable class to the \"positiveElements\" variable. This error is fatal. Please inform Dennis about this.");
                        }
                    }

                    // add negative elements
                    foreach(int classId in classFound.Keys)
                        if(!classFound[classId])
                            if(classId == 0)
                                runData.cubes.negatives.Add(new NegativesData("negatives\\" + timestmp + randomNumber + ".png"));
                            else if(classId == 1)
                                runData.balls.negatives.Add(new NegativesData("negatives\\" + timestmp + randomNumber + ".png"));
                            /*else if(classId == 2) // add new detectable class
                                runData.tetraeders.negatives.Add(new NegativesData("negatives\\" + timestmp + randomNumber + ".png"));*/
                            else {
                                Debug.LogError("The script had a non-existent detectable class in the \"classFound.Key\" field. This error is fatal. Please inform Dennis about this.");
                                throw new System.Exception("The script had a non-existent detectable class in the \"classFound.Key\" field. This error is fatal. Please inform Dennis about this.");
                            }

                    // if at least one object was detected
                    if(allowEmptyCaptures || boxDataList.Count > 0) {
                        // save unlabeled image
                        if(!Directory.Exists(trainingDir + modeDir + yoloDir)) {
                            Directory.CreateDirectory(trainingDir + modeDir + yoloDir);
                        }
                        File.WriteAllBytes(trainingDir + modeDir + yoloDir + timestmp + randomNumber + ".png", screenshotAsPNG);

                        // save labeled image
                        if(configTransporter.saveLabeledImages) {
                            screenshotAsPNG = screenshot.EncodeToPNG();
                            File.WriteAllBytes(trainingDir + modeDir + yoloDir + timestmp + randomNumber + "_labeled.png", screenshotAsPNG);
                        }

                        // save json data
                        /* string jsonString = JsonUtility.ToJson(new BoxDataList(boxDataList), true);
                        File.WriteAllText(path + timestmp + ".json", jsonString);*/

                        // save txt file as yolo label
                        foreach(BoxData boxData in boxDataList) {
                            using(StreamWriter sw = File.AppendText(trainingDir + modeDir + yoloDir + timestmp + randomNumber + ".txt")) {
                                sw.WriteLine(boxData.id + " " +
                                                (float)(boxData.x + boxData.w / 2) / Screen.width + " " +
                                                (float)(boxData.y + boxData.h / 2) / Screen.height + " " +
                                                (float)boxData.w / Screen.width + " " +
                                                (float)boxData.h / Screen.height);
                            }
                        }
                        if(boxDataList.Count == 0) using(StreamWriter sw = File.AppendText(trainingDir + modeDir + yoloDir + timestmp + randomNumber + ".txt")) sw.Write("");

                        // overwrite json for cascade classifier
                        string jsonString = JsonUtility.ToJson(cascadeClassifierData, true);
                        File.WriteAllText(trainingDir + modeDir + cascadeClassifierJSONPath, jsonString);
                    }
                    // no object detected and capturing labels without detections is off
                    else sendMessage("<color=red>Nothing captured. No object in sight. If you want to capture empty images too, please press the <color=blue>P</color> button</color>");
                }
                else sendMessage(   "<color=red>Nothing captured. There is at least one object, whose width or height " +
                                    "in pixels is smaller than " + minimalSize + ". Please ensure the sizes of all visible detectables is sufficient.</color>");
            }
            // file already exits => player didn't wait at least
            // one seconds AND randomly generated number was the same
            else sendMessage("<color=red>Nothing captured. Please wait at least one second. That's because the "
                            + "screenshots are named with a timestamp messured in seconds. Multiple screenshots " 
                            + "within the same second would lead to overwritten images and faulty labels.</color>");
        }

        IEnumerator RandomizedDetectableColorsCapture() {
        // TODO
        yield return new WaitForSeconds(1);
        }
    }

    // extra mechanism to prevent overwriting images
    IEnumerator AllowCapturingAfterOneSecond(float t) {
        yield return new WaitForSecondsRealtime(t);
        allowCapturing = true;
    }

    [System.Serializable]
    public class Message {
        public string text;
        public Text textObject;
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

[System.Serializable]
class BoxDataList {
    public List<BoxData> data;

    public BoxDataList(List<BoxData> data) {
        this.data = data;
    }
}

[System.Serializable]
class BoxData {
    public int id;
    public float x;
    public float y;
    public float w;
    public float h;

    public BoxData(int objId, float posX, float posY, float width, float height) {
        this.id = objId;
        this.x = posX;
        this.y = posY;
        this.w = width;
        this.h = height;
    }
}

// Neccessary classes for cascading classifier data
[System.Serializable]
class CascadeClassifierData {
    public List<RunData> runData;

    public CascadeClassifierData() {
        this.runData = new List<RunData>();
    }
}

[System.Serializable]
class RunData {
    public string runId;
    public string mode;
    public string version;
    public DetectableData cubes;
    public DetectableData balls;
    //public List<DetectableData> tetraeders;

    public RunData(string runId, string mode, string version) {
        this.runId = runId;
        this.mode = mode;
        this.version = version;
        this.cubes = new DetectableData(); // for cubes
        this.balls = new DetectableData(); // for balls
        //this.tetraeders.Add(new DetectableData()); // for tetraeders
    }
}

[System.Serializable]
class DetectableData {
    public List<PositivesData> positives;
    public List<NegativesData> negatives;

    public DetectableData() {
        this.positives = new List<PositivesData>();
        this.negatives = new List<NegativesData>();
    }
}

[System.Serializable]
class PositivesData {
    public string path;
    public BBox boxEntry;

    public PositivesData(string path, BBox bbox) {
        this.path = path;
        this.boxEntry = bbox;
    }
}

[System.Serializable]
class BBox {
    public int x;
    public int y;
    public int w;
    public int h;

    public BBox(int x, int y, int w, int h) {
        this.x = x;
        this.y = y;
        this.w = w;
        this.h = h;
    }
}

class PositiveElement {
    public int classId;
    public string path;
    public BBox bbox;

    public PositiveElement(int classId, string path, BBox bbox) {
        this.classId = classId;
        this.path = path;
        this.bbox = bbox;
    }
}

class NegativeElement {
    public int classId;
    public string path;

    public NegativeElement(int classId, string path) {
        this.classId = classId;
        this.path = path;
    }
}

[System.Serializable]
class NegativesData {
    public string path;

    public NegativesData(string path) {
        this.path = path;
    }
}