using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

public class ScreenshotTaker: MonoBehaviour {

    [SerializeField] Camera playerCamera = null;
    [SerializeField] Canvas UICanvas = null;
    [SerializeField] List<GameObject> detectables = null;
    [SerializeField] int detecableCount = 0;
    [SerializeField] GameObject ballPrefab = null;
    [SerializeField] GameObject cubePrefab = null;
    [SerializeField] GameObject detectablesParent = null;
    [SerializeField] int minimalDetectionSize = 0;
    [SerializeField] int paddingPerSide = 0;
    [SerializeField] ConfigTransporter configTransporter = null;
    string trainingDataPath;
    string yoloDataPath;
    string cascadeClassifierDataPath;
    string cascadeClassifierDataJSONPath;
    public int maxMessages;
    [SerializeField] List<Message> messageList = new List<Message>();
    public GameObject contentObject, textObject;
    public float deathHeight;
    public GameObject player;
    bool allowCapturing;
    bool allowEmptyCaptures;
    CascadeClassifierData cascadeClassifierData;
    string runId;
    RunData runData;
    
    // Start is called before the first frame update
    void Start()
    {
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

        // instatiate detectables at random positions all over the map
        for(int i = 0; i < detecableCount; i++) {

            // gernerate randomly chosen detectables
            Vector3 randomPosition = new Vector3(UnityEngine.Random.Range(-100f, 100f), 10f, UnityEngine.Random.Range(-100f, 100f));
            GameObject chosenGameObject = null;
            if(i % 2 == 0) chosenGameObject = cubePrefab;
            else if(i % 2 == 1) chosenGameObject = ballPrefab;
            //else if(i % 2 == 2) chosenGameObject = tetraederPrefab; // add new detectable class
            else {
                Debug.LogError("Game tried to instantiate a detectable, whose type could not be infered.");
                throw new ArgumentNullException("Game tried to instantiate a detectable, whose type could not be infered.");
            }

            GameObject detectable = Instantiate(    chosenGameObject,
                                                    randomPosition,
                                                    Quaternion.Euler(   UnityEngine.Random.Range(0f, 180f),
                                                                        UnityEngine.Random.Range(0f, 180f),
                                                                        UnityEngine.Random.Range(0f, 180f)),
                                                    detectablesParent.transform);

            try {
                if(configTransporter.currentMode.ModeName == "RANDOMIZE_DETECTABLE_COLORS") {
                    Color newColor = new Color( UnityEngine.Random.Range(0f, 1f),
                                                UnityEngine.Random.Range(0f, 1f),
                                                UnityEngine.Random.Range(0f, 1f));

                    detectable.GetComponent<MeshRenderer>().material.SetColor("_Color", newColor);
                }
            } catch(Exception ex) {
                Debug.Log(ex);
            }

            detectables.Add(detectable);
        }

        // Load cascade classifier data already existent
        if(!Directory.Exists(cascadeClassifierDataPath)) {
            Directory.CreateDirectory(cascadeClassifierDataPath);
        }
        if(File.Exists(cascadeClassifierDataJSONPath)) {
            string jsonString = File.ReadAllText(cascadeClassifierDataJSONPath);
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
    }

    // Update is called once per frame
    void Update()
    {
        if(player.transform.position.y <= deathHeight) {
            sendMessage("<color=red>You fell out of bounds. I reset you to the spawn location.</color>");
            player.transform.position = Vector3.zero;
        }

        if(Input.GetKeyDown(KeyCode.Escape)) Application.Quit();

        if(Input.GetKeyDown(KeyCode.E)) {
            if(allowCapturing) {
                allowCapturing = false;

                // start coroutine so hide UI for the frame the screen is captured
                StartCoroutine(CaptureObjects());

                StartCoroutine(AllowCapturingAfterOneSecond(1.1f));
            } else {
                sendMessage("<color=red>Nothing captured. Please wait at least one second. That's because the " +
                            "images are named with a timestamp messured in seconds. Multiple captures " +
                            "within the same second would lead to overwritten images and faulty labels.</color>");
            }
        }

        if(Input.GetKeyDown(KeyCode.H)) UICanvas.enabled = !UICanvas.enabled;

        if(Input.GetKeyDown(KeyCode.P)) {
            allowEmptyCaptures = !allowEmptyCaptures;
            if(allowEmptyCaptures) sendMessage("Capturing empty images is toggled <color=green>ON</color>");
            else sendMessage("Capturing empty images is toggled <color=red>OFF</color>.");
        }

        // extra mechanism to prevent overwriting images
        IEnumerator AllowCapturingAfterOneSecond(float n) {
            yield return new WaitForSecondsRealtime(n);
            allowCapturing = true;
        }

        IEnumerator CaptureObjects() {

            // get time stamp as file name
            System.DateTime time = System.DateTime.Now;
            string timestmp = time.Year + "-"
                            + time.Month + "-"
                            + time.Day + "_"
                            + time.Hour + "h"
                            + time.Minute + "min"
                            + time.Second + "sec";

            int randomNumber = UnityEngine.Random.Range(0, 10000);

            if(!File.Exists(trainingDataPath + timestmp + randomNumber + ".png")) {

                sendMessage("*SNAP*");

                // wait till the last possible moment before screen rendering to hide UI
                bool canvasWasEnabled = UICanvas.enabled;
                yield return null;
                UICanvas.enabled = false;

                // wait for screen rendering to complete
                yield return new WaitForEndOfFrame();
                if(canvasWasEnabled) UICanvas.enabled = true;

                // take screenshot
                Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] screenshotAsPNG = screenshot.EncodeToPNG();

                bool allSizesSufficient = true;
                List<BoxData> boxDataList = new List<BoxData>();
                List<PositiveElement> positiveElements = new List<PositiveElement>();
                foreach(GameObject detectable in detectables) {

                    Renderer renderer = detectable.GetComponent<Renderer>();

                    // get boundary box
                    if(renderer.isVisible) {

                        // get an array of all vertices of the object
                        Mesh mesh = detectable.GetComponent<MeshFilter>().mesh;
                        Vector3[] vertices = mesh.vertices;

                        // since vertices are given relative to their gameobject, we need to convert them into worldSpace
                        for(int i = 0; i < vertices.Length; i++) {

                            vertices[i] = detectable.transform.TransformPoint(vertices[i]);

                        }

                        float left = Screen.width + 1f;
                        float right = -1f;
                        float top = -1f;
                        float bottom = Screen.height + 1f;

                        // with camera.WorldToScreenPoint(Vector3 worldPoint) search for the top, bottom, most left and most right point
                        bool objectInSight = false;
                        foreach(Vector3 vertex in vertices) {

                            // check if vertex is NOT BEHIND the camera
                            if(Vector3.Dot(playerCamera.transform.forward, vertex - playerCamera.transform.position) >= 0f) {

                                // check if corresponding screen point is on screen and if it is a newly found border point candidate
                                Vector3 screenPoint = playerCamera.WorldToScreenPoint(vertex);
                                if(new Rect(0, 0, Screen.width, Screen.height).Contains(screenPoint)) {

                                    // check if vertex is obscured by another object
                                    bool vertexIsObscured = false;
                                    RaycastHit hitInfo;
                                    if(Physics.Raycast(playerCamera.transform.position,
                                                        vertex - playerCamera.transform.position,
                                                        out hitInfo,
                                                        (vertex - playerCamera.transform.position).magnitude - 0.01f)) {

                                        if(hitInfo.collider.gameObject != detectable) {

                                            vertexIsObscured = true;

                                        }
                                        else {

                                            objectInSight = true;

                                        }
                                    }

                                    if(!vertexIsObscured) {

                                        if(screenPoint.x < left) left = screenPoint.x;
                                        if(screenPoint.x > right) right = screenPoint.x;
                                        if(screenPoint.y < bottom) bottom = screenPoint.y;
                                        if(screenPoint.y > top) top = screenPoint.y;

                                    }
                                }
                            }
                        }

                        // determine object class
                        int classId = -1;
                        if(detectable.name.StartsWith("Cube")) classId = 0;
                        else if(detectable.name.StartsWith("Ball")) classId = 1;
                        // else if(detectable.name.StartsWith("Tetraeder")) classId = 2; // add new detectable class
                        else {
                            Debug.LogError("Couldn't infer object class");
                            throw new System.Exception("Detected an object whose id cannot be infered.");
                        }

                        // add object to data
                        if(objectInSight) {

                            // check if object is at least 12 pixels wide and high
                            bool tooSmall = false;
                            float width = right - left;
                            float height = top - bottom;
                            bool wasDetected = left != Screen.width + 1f && right != -1f && top != -1f && bottom != Screen.height + 1f;
                            if((Mathf.RoundToInt(width) < minimalDetectionSize || Mathf.RoundToInt(height) < minimalDetectionSize) && wasDetected)
                                tooSmall = true;

                            if(!tooSmall) {

                                // represent detected objects in the info box
                                string name = detectable.name;
                                if(detectable.name.Contains("Cube")) name = "<color=red>" + detectable.name + "</color>";
                                else if(detectable.name.Contains("Ball")) name = "<color=cyan>" + detectable.name + "</color>";
                                sendMessage(name + "(id: " + detectable.GetInstanceID() + ") snapped");

                                // draw the lines
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
                                int cascadeLeft = Mathf.RoundToInt(left) - paddingPerSide;
                                if(cascadeLeft < 0) cascadeLeft = 0;
                                int cascadeTop = Mathf.RoundToInt(Screen.height - top) - paddingPerSide;
                                if(cascadeTop < 0) cascadeTop = 0; 
                                int cascadeWidth = Mathf.RoundToInt(width) + 2 * paddingPerSide;
                                if(cascadeLeft + cascadeWidth > Screen.width) cascadeWidth = Screen.width - cascadeLeft;
                                int cascadeHeight = Mathf.RoundToInt(height) + 2 * paddingPerSide;
                                if(cascadeTop + cascadeHeight > Screen.height) cascadeHeight = Screen.height - cascadeTop;

                                positiveElements.Add(new PositiveElement(   classId,
                                                                            "positives\\" + timestmp + randomNumber + ".png",
                                                                            new BBox(cascadeLeft, cascadeTop, cascadeWidth, cascadeHeight)));

                            }
                            else allSizesSufficient = false;
                        }
                    }
                }

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
                        if(!Directory.Exists(yoloDataPath)) {

                            Directory.CreateDirectory(yoloDataPath);

                        }
                        File.WriteAllBytes(yoloDataPath + timestmp + randomNumber + ".png", screenshotAsPNG);

                        // save labeled image
                        if(configTransporter.saveLabeledImages) {
                            screenshotAsPNG = screenshot.EncodeToPNG();
                            File.WriteAllBytes(yoloDataPath + timestmp + randomNumber + "_labeled.png", screenshotAsPNG);
                        }

                        // save json data
                        /* string jsonString = JsonUtility.ToJson(new BoxDataList(boxDataList), true);
                        File.WriteAllText(path + timestmp + ".json", jsonString);*/

                        // save txt file as yolo label
                        foreach(BoxData boxData in boxDataList) {

                            using(StreamWriter sw = File.AppendText(yoloDataPath + timestmp + randomNumber + ".txt")) {

                                sw.WriteLine(boxData.id + " " +
                                                (float)(boxData.x + boxData.w / 2) / Screen.width + " " +
                                                (float)(boxData.y + boxData.h / 2) / Screen.height + " " +
                                                (float)boxData.w / Screen.width + " " +
                                                (float)boxData.h / Screen.height);

                            }
                        }
                        if(boxDataList.Count == 0) using(StreamWriter sw = File.AppendText(yoloDataPath + timestmp + randomNumber + ".txt")) sw.Write("");

                        // overwrite json for cascade classifier
                        string jsonString = JsonUtility.ToJson(cascadeClassifierData, true);
                        File.WriteAllText(cascadeClassifierDataJSONPath, jsonString);

                    }
                    // no object detected and capturing labels without detections is off
                    else sendMessage("<color=red>Nothing captured. No object in sight. If you want to capture empty images too, please press the <color=blue>P</color> button</color>");

                }
                else sendMessage(   "<color=red>Nothing captured. There is at least one object, whose width or height " +
                                    "in pixels is smaller than " + minimalDetectionSize + ". Please ensure the sizes of all visible detectables is sufficient.</color>");

            }
            // file already exits => player didn't wait at least
            // one seconds AND randomly generated number was the same
            else sendMessage("<color=red>Nothing captured. Please wait at least one second. That's because the " +
                              "screenshots are named with a timestamp messured in seconds. Multiple screenshots " + 
                              "within the same second would lead to overwritten images and faulty labels.</color>");
        }
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
public class Message {
    public string text;
    public Text textObject;
}

/*[System.Serializable]
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
}*/