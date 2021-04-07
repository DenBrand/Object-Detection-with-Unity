using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

namespace ScreenCapturerUtils {

    public class ScreenCapturer: MonoBehaviour {
        [SerializeField] Canvas UICanvas = null;
        [SerializeField] Camera playerCamera = null;
        [SerializeField] [Range(30, 100)] int numberOfDetectables = 50;
        [SerializeField] [Range(30, 50)] int minimalSize = 30;
        [SerializeField] [Range(1, 5)] int paddingForCascadeClassifier = 3;
        [SerializeField] GameObject detectablesParent = null;
        [SerializeField] List<GameObject> detectablesPrefabs = null;
        ConfigTransporter configTransporter;
        string mode;
        bool saveLabeledImages;
        bool saveEmptyImages;
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
            detectables = new List<GameObject>();
            RespawnAllDetectables();
        }

        void Start() {
            // get the configTransporter
            GameObject cfgTrans = GameObject.Find("ConfigTransporter");
            if(cfgTrans != null) {

                configTransporter = cfgTrans.GetComponent<ConfigTransporter>();

                if(configTransporter != null) {
                    mode = configTransporter.currentMode.ModeName;
                    saveLabeledImages = configTransporter.saveLabeledImages;
                    saveEmptyImages = configTransporter.saveEmptyImages;
                }
                else {
                    mode = "STANDARD";
                    saveLabeledImages = true;
                    saveEmptyImages = false;
                }
            }
            else {
                mode = "STANDARD";
                saveLabeledImages = true;
                saveEmptyImages = false;
            }

            Cursor.visible = false;
            allowCapturing = true;
            allowEmptyCaptures = saveEmptyImages;

            // define certain paths and folder name of this mode
            if(SystemInfo.operatingSystem.StartsWith("Windows")) {
                trainingDir = "training_data\\";
                yoloDir = "yolo\\";
                cascadeClassifierDir = "cascade_classifier\\";
                modeDir = configTransporter.currentMode.ModeName.ToLower() + "\\";
            }
            else {
                trainingDir = "training_data/";
                yoloDir = "yolo/";
                cascadeClassifierDir = "cascade_classifier/";
                modeDir = configTransporter.currentMode.ModeName.ToLower() + "/";
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
            runData = new RunData(runId, mode, Application.version);
            cascadeClassifierData.runData.Add(runData);

            // send initial messages to the player
            sendMessage("Move: WASD");
            sendMessage("Look: Mouse");
            sendMessage("Capture: E");
            sendMessage("Zoom: Scroll Mouse Wheel");
            sendMessage("Hide Info Box: H");
            sendMessage("Respawn Detectables: 1");
            sendMessage("Close Application: ESC");
            sendMessage("<color=red>Wait at least one second between captures.</color>");
            sendMessage("Training data are saved in \"" + trainingDir + "\".");
            sendMessage("<color=green>STARTING CAPTURING SESSION</color>");
        }

        void RespawnAllDetectables() {
            // destroy all current detectables
            foreach(GameObject detectable in detectables) {
                Destroy(detectable);
            }
            detectables = new List<GameObject>();

            // refill detectablesContainer with random detectables
            for(int i = 0; i < numberOfDetectables; i++) {
                GameObject chosenDetectable = detectablesPrefabs[i % detectablesPrefabs.Count];
                Vector3 randomPosition = new Vector3(UnityEngine.Random.Range(-100f, 100f),
                                                        10f,
                                                        UnityEngine.Random.Range(-100f, 100f));

                GameObject newDetectable = Instantiate(chosenDetectable,
                                            randomPosition,
                                            Quaternion.Euler(UnityEngine.Random.Range(0f, 180f),
                                                                UnityEngine.Random.Range(0f, 180f),
                                                                UnityEngine.Random.Range(0f, 180f)),
                                            detectablesParent.transform);

                detectables.Add(newDetectable);
            }
        }

        void Update() {
            // reposition if fallen out of bounds
            if(player.transform.position.y <= deathHeight) {
                sendMessage("<color=red>You fell out of bounds. I reset you to the spawn location.</color>");
                player.transform.position = Vector3.zero;
            }

            // button to respawn all detectables
            if(Input.GetKeyDown(KeyCode.Alpha1)) RespawnAllDetectables();

            // toggle UI button
            if(Input.GetKeyDown(KeyCode.H)) UICanvas.enabled = !UICanvas.enabled;

            // start capturing routine
            if(Input.GetKeyDown(KeyCode.E)) {
                if(allowCapturing) {
                    allowCapturing = false;

                    // get time stamp as file name
                    DateTime time = DateTime.Now;
                    string timestmp = time.Year + "-"
                                    + time.Month + "-"
                                    + time.Day + "_"
                                    + time.Hour + "h"
                                    + time.Minute + "min"
                                    + time.Second + "sec"
                                    + UnityEngine.Random.Range(0, 10000);

                    // perform capture accoringly to the mode
                    StartCoroutine(PerformCapture(timestmp, mode));

                    StartCoroutine(AllowCapturingAfterOneSecond(1.1f));
                }
                else {
                    sendMessage("<color=red>Nothing captured. Please wait at least one second. That's because the "
                                + "images are named with a timestamp messured in seconds. Multiple captures "
                                + "within the same second could lead to overwritten images and faulty labels.</color>");
                }
            }
        }

        IEnumerator PerformCapture(string timestmp, string mode) {
            // check if screenshot with this name was already taken
            if(!File.Exists(trainingDir + modeDir + yoloDir + timestmp + ".png")) {
                sendMessage("*SNAP*");

                bool canvasWasEnabled = UICanvas.enabled;
                UICanvas.enabled = false;
                float oldTimeScale = Time.timeScale;
                Time.timeScale = 0f;

                // wait till change is applied
                yield return null;

                // wait for screen rendering to complete
                yield return new WaitForEndOfFrame();

                Texture2D rawScreenshot;
                Texture2D rawRandomColorScreenshot;
                Texture2D labeledScreenshot;
                switch(mode) {
                    case "STANDARD":

                        if(canvasWasEnabled) UICanvas.enabled = true;
                        rawScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
                        labeledScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
                        rawRandomColorScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
                        break;

                    case "RANDOMIZE_DETECTABLE_COLORS":

                        rawScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
                        labeledScreenshot = ScreenCapture.CaptureScreenshotAsTexture();

                        // change colors ans store old ones
                        List<Color> originalDetectableColors = new List<Color>();
                        for(int i = 0; i < detectables.Count; i++) {
                            originalDetectableColors.Add(detectables[i].GetComponent<Renderer>().material.color);
                            detectables[i].GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f),
                                                                                                UnityEngine.Random.Range(0f, 1f),
                                                                                                UnityEngine.Random.Range(0f, 1f));
                        }
                        // wait for screen rendering to complete
                        yield return new WaitForEndOfFrame();
                        
                        if(canvasWasEnabled) UICanvas.enabled = true;
                        // restore old detectable colors
                        for(int i = 0; i < detectables.Count; i++) {
                            Renderer detectableRenderer = detectables[i].GetComponent<Renderer>();
                            detectableRenderer.material.color = originalDetectableColors[i];
                        }
                        // take raw randomly colored screenshot
                        rawRandomColorScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
                        break;
                    default:
                        throw new ArgumentNullException("Could not resolve mode when deciding how to take captures.");
                }
                Time.timeScale = oldTimeScale;

                // filter out unseen detectables
                bool allSizesSufficient = true;
                List<BoxData> boxDataList = new List<BoxData>();
                List<PositiveElement> positiveElements = new List<PositiveElement>();
                foreach(GameObject detectable in detectables) {
                    // is detectable seen by any camera? (first filter)
                    if(detectable.GetComponent<Renderer>().isVisible) {
                        // determine detectable class
                        int classId;
                        if(detectable.name.StartsWith("Cube")) classId = 0;
                        else if(detectable.name.StartsWith("Ball")) classId = 1;
                        // else if(detectable.name.StartsWith("Tetraeder")) classId = 2; // add new detectable class
                        else {
                            Debug.LogError("Couldn't infer detectable class");
                            throw new Exception("Detected an object whose id cannot be infered. Unknown detectable class.");
                        }

                        // is it visible?
                        float leftBoundary;
                        float rightBoundary;
                        float topBoundary;
                        float bottomBoundary;
                        if(IsDetectableVisible(detectable,
                                                out leftBoundary,
                                                out rightBoundary,
                                                out topBoundary,
                                                out bottomBoundary)) {

                            // object at least minimalSize pixels wide and high?
                            float width = rightBoundary - leftBoundary;
                            float height = topBoundary - bottomBoundary;
                            if(Mathf.RoundToInt(width) >= minimalSize && Mathf.RoundToInt(height) >= minimalSize) {
                                // represent detected objects in the info box
                                string name = detectable.name;
                                if(detectable.name.Contains("Cube")) name = "<color=red>" + detectable.name + "</color>";
                                else if(detectable.name.Contains("Ball")) name = "<color=cyan>" + detectable.name + "</color>";
                                // else if(detectable.name.Contains("Tetraeder")) name = "<color=magenta>" + detectable.name + "</color>";
                                else throw new ArgumentOutOfRangeException("Could not resolve detectable class while writing it in the infobox.");
                                sendMessage(name + "(id: " + detectable.GetInstanceID() + ") snapped");

                                // draw boundary box
                                for(int x = (int)leftBoundary; x <= (int)rightBoundary; x++) {
                                    labeledScreenshot.SetPixel(x, (int)bottomBoundary, Color.red);
                                    labeledScreenshot.SetPixel(x, (int)topBoundary, Color.red);
                                }
                                for(int y = (int)bottomBoundary; y <= (int)topBoundary; y++) {
                                    labeledScreenshot.SetPixel((int)leftBoundary, y, Color.red);
                                    labeledScreenshot.SetPixel((int)rightBoundary, y, Color.red);
                                }

                                // gather corresponding data
                                // (1) for yolo
                                BoxData boxData = new BoxData(classId,
                                                                Mathf.RoundToInt(leftBoundary),
                                                                Mathf.RoundToInt(Screen.height - topBoundary),
                                                                width,
                                                                height);
                                boxDataList.Add(boxData);

                                // (2) for cascade classifier: positives
                                int cascadeLeft = Mathf.RoundToInt(leftBoundary) - paddingForCascadeClassifier;
                                if(cascadeLeft < 0) cascadeLeft = 0;
                                int cascadeTop = Mathf.RoundToInt(Screen.height - topBoundary) - paddingForCascadeClassifier;
                                if(cascadeTop < 0) cascadeTop = 0;
                                int cascadeWidth = Mathf.RoundToInt(width) + 2 * paddingForCascadeClassifier;
                                if(cascadeLeft + cascadeWidth > Screen.width) cascadeWidth = Screen.width - cascadeLeft;
                                int cascadeHeight = Mathf.RoundToInt(height) + 2 * paddingForCascadeClassifier;
                                if(cascadeTop + cascadeHeight > Screen.height) cascadeHeight = Screen.height - cascadeTop;
                                positiveElements.Add(new PositiveElement(classId,
                                                                            "positives\\" + timestmp + ".png",
                                                                            new BBox(cascadeLeft,
                                                                                        cascadeTop,
                                                                                        cascadeWidth,
                                                                                        cascadeHeight)));
                            }
                            else allSizesSufficient = false;
                        }
                    }
                } // all detectable in the scene where checked

                if(allSizesSufficient) {
                    // add cascade classifier data and for every class:
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
                                runData.cubes.negatives.Add(new NegativesData("negatives\\" + timestmp + ".png"));
                            else if(classId == 1)
                                runData.balls.negatives.Add(new NegativesData("negatives\\" + timestmp + ".png"));
                            /*else if(classId == 2) // add new detectable class
                                runData.tetraeders.negatives.Add(new NegativesData("negatives\\" + timestmp + ".png"));*/
                            else {
                                Debug.LogError("The script had a non-existent detectable class in the \"classFound.Key\" field. This error is fatal. Please inform Dennis about this.");
                                throw new System.Exception("The script had a non-existent detectable class in the \"classFound.Key\" field. This error is fatal. Please inform Dennis about this.");
                            }

                    // at least one object detected or empty allowed?
                    if(allowEmptyCaptures || boxDataList.Count > 0) {

                        byte[] rawScreenshotAsPNG = rawScreenshot.EncodeToPNG();
                        byte[] rawRandomColorScreenshotAsPNG = rawRandomColorScreenshot.EncodeToPNG();

                        // save unlabeled image
                        if(!Directory.Exists(trainingDir + modeDir + yoloDir)) {
                            Directory.CreateDirectory(trainingDir + modeDir + yoloDir);
                        }
                        File.WriteAllBytes(trainingDir + modeDir + yoloDir + timestmp + ".png", rawScreenshotAsPNG);

                        // save unlabeled image with random colors
                        if(mode == "RANDOMIZE_DETECTABLE_COLORS") {
                            File.WriteAllBytes(trainingDir + modeDir + yoloDir + timestmp + "_randomized_colors.png", rawRandomColorScreenshotAsPNG);
                        }

                        // save labeled image
                            if(saveLabeledImages) {
                            byte[] labeledScreenshotAsPNG = labeledScreenshot.EncodeToPNG();
                            File.WriteAllBytes(trainingDir + modeDir + yoloDir + timestmp + "_labeled.png", labeledScreenshotAsPNG);
                        }

                        // save json data
                        /* string jsonString = JsonUtility.ToJson(new BoxDataList(boxDataList), true);
                        File.WriteAllText(path + timestmp + ".json", jsonString);*/

                        // save txt file as yolo label
                        foreach(BoxData boxData in boxDataList) {
                            using(StreamWriter sw = File.AppendText(trainingDir + modeDir + yoloDir + timestmp + ".txt")) {
                                sw.WriteLine(boxData.id + " " +
                                                (float)(boxData.x + boxData.w / 2) / Screen.width + " " +
                                                (float)(boxData.y + boxData.h / 2) / Screen.height + " " +
                                                (float)boxData.w / Screen.width + " " +
                                                (float)boxData.h / Screen.height);
                            }
                        }
                        if(boxDataList.Count == 0) using(StreamWriter sw = File.AppendText(trainingDir + modeDir + yoloDir + timestmp + ".txt")) sw.Write("");

                        // overwrite json for cascade classifier
                        string jsonString = JsonUtility.ToJson(cascadeClassifierData, true);
                        File.WriteAllText(trainingDir + modeDir + cascadeClassifierJSONPath, jsonString);
                    }
                    // no object detected and capturing labels without detections is off
                    else sendMessage("<color=red>Nothing captured. No object in sight.</color>");
                }
                else sendMessage("<color=red>Nothing captured. There is at least one object, whose width or height " +
                                    "in pixels is smaller than " + minimalSize + ". Please ensure the sizes of all visible detectables is sufficient.</color>");
            }
            // file already exits => player didn't wait at least
            // one seconds AND randomly generated number was the same
            else sendMessage("<color=red>Nothing captured. Please wait at least one second. That's because the "
                            + "screenshots are named with a timestamp messured in seconds. Multiple screenshots "
                            + "within the same second would lead to overwritten images and faulty labels.</color>");
        }

        // extra mechanism to prevent overwriting images
        IEnumerator AllowCapturingAfterOneSecond(float t) {
            yield return new WaitForSecondsRealtime(t);
            allowCapturing = true;
        }

        bool IsDetectableVisible(GameObject detectable,
                                    out float leftBoundary,
                                    out float rightBoundary,
                                    out float topBoundary,
                                    out float bottomBoundary) {

            leftBoundary = Screen.width + 1f;
            rightBoundary = -1f;
            topBoundary = -1f;
            bottomBoundary = Screen.height + 1f;

            if(detectable.GetComponent<Renderer>().isVisible) {

                Vector3[] vertices = detectable.GetComponent<MeshFilter>().mesh.vertices;

                // transform local coordinates into world coordinates
                for(int i = 0; i < vertices.Length; i++) {
                    vertices[i] = detectable.transform.TransformPoint(vertices[i]);
                }

                bool objectInSight = false;
                foreach(Vector3 vertex in vertices) {
                    // vertex in front of player?
                    Vector3 distanceVector = vertex - playerCamera.transform.position;
                    if(Vector3.Dot(playerCamera.transform.forward, distanceVector) > 0f) {
                        // is it in cone of vision?
                        Vector3 screenPoint = playerCamera.WorldToScreenPoint(vertex);
                        if(new Rect(0, 0, Screen.width, Screen.height).Contains(screenPoint)) {
                            // is it obscured?
                            RaycastHit hitInfo;
                            bool hit = Physics.Raycast(playerCamera.transform.position,
                                                        distanceVector,
                                                        out hitInfo,
                                                        distanceVector.magnitude - 0.01f);

                            if(!hit || hitInfo.collider.gameObject == detectable) {
                                // vertex is in sight => detectable is in sight
                                objectInSight = true;
                                // new most horizontally or vertically extent vertex? (for boundary box)
                                if(screenPoint.x < leftBoundary) leftBoundary = screenPoint.x;
                                if(screenPoint.x > rightBoundary) rightBoundary = screenPoint.x;
                                if(screenPoint.y < bottomBoundary) bottomBoundary = screenPoint.y;
                                if(screenPoint.y > topBoundary) topBoundary = screenPoint.y;
                            }
                        }
                    }
                }
                return objectInSight;
            }
            else return false;
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
}