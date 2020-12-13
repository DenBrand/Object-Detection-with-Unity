using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class ScreenshotTaker: MonoBehaviour {

    [SerializeField] private Camera playerCamera = null;
    [SerializeField] private Canvas UICanvas = null;
    [SerializeField] private List<GameObject> detectables = null;
    [SerializeField] private int detecableCount = 0;
    [SerializeField] private GameObject ballPrefab = null;
    [SerializeField] private GameObject cubePrefab = null;
    [SerializeField] private GameObject detectablesParent = null;
    private string path;
    public int maxMessages;
    [SerializeField] List<Message> messageList = new List<Message>();
    public GameObject contentObject, textObject;
    public float deathHeight;
    public GameObject player;
    private bool allowCapturing;

    // Start is called before the first frame update
    void Start()
    {

        Cursor.visible = false;
        allowCapturing = true;

        if(SystemInfo.operatingSystem.StartsWith("Windows"))
            path = "screenshots\\";
        else
            path = "screenshots/";

        sendMessage("Move: WASD");
        sendMessage("Look: Mouse");
        sendMessage("Capture: E");
        sendMessage("Zoom: Scroll Mouse Wheel");
        sendMessage("Hide Info Box: H");
        sendMessage("Close Application: ESC");
        sendMessage("<color=red>Wait at least one second between captures.</color>");
        sendMessage("Screenshots are saved in \"" + path + "\".");
        sendMessage("<color=green>STARTING CAPTURING SESSION</color>");

        // instatiate 30 detectables at random positions all over the map
        for(int i = 0; i < detecableCount; i++) {

            Vector3 randomPosition = new Vector3(Random.Range(-100f, 100f), 10f, Random.Range(-100f, 100f));

            GameObject detectable = Instantiate(   i % 2 == 0 ? cubePrefab : ballPrefab,
                                                    randomPosition,
                                                    Quaternion.Euler(   Random.Range(0f, 180f),
                                                                        Random.Range(0f, 180f),
                                                                        Random.Range(0f, 180f)),
                                                    detectablesParent.transform);
            
            detectables.Add(detectable);

        }
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

        if(Input.GetKeyDown(KeyCode.H)) {

            UICanvas.enabled = !UICanvas.enabled;

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

            int randomNumber = Random.Range(0, 9999);

            if(!File.Exists(path + timestmp + randomNumber + ".jpg")) {

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
                byte[] screenshotAsJPG = screenshot.EncodeToJPG();

                List<BoxData> boxDataList = new List<BoxData>();
                
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

                        if(objectInSight) {

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
                            int id = -1;
                            if(detectable.name.StartsWith("Cube")) id = 0;
                            else if(detectable.name.StartsWith("Ball")) id = 1;
                            else Debug.Log("Couldn't infer object class");

                            int width = (int)right - (int)left;
                            int height = (int)top - (int)bottom;
                            int center_x = (int)left + width / 2;
                            int center_y = (int)bottom + height / 2;

                            BoxData boxData = new BoxData(  id,
                                                            (float)center_x / Screen.width,
                                                            (float)center_y / Screen.height,
                                                            (float)width / Screen.width,
                                                            (float)height / Screen.height);
                            boxDataList.Add(boxData);

                        }
                    }
                }

                // if at least one object was detected
                if(boxDataList.Count > 0) {
                    // save unlabeled image
                    if(!Directory.Exists(path)) {

                        Directory.CreateDirectory(path);

                    }
                    File.WriteAllBytes(path + timestmp + randomNumber + ".jpg", screenshotAsJPG);

                    // save labeled image
                    screenshotAsJPG = screenshot.EncodeToJPG();
                    File.WriteAllBytes(path + timestmp + randomNumber + "_labeled.jpg", screenshotAsJPG);

                    /* // save json data
                    string jsonString = JsonUtility.ToJson(new BoxDataList(boxDataList), true);
                    File.WriteAllText(path + timestmp + ".json", jsonString);*/

                    // save txt file
                    foreach(BoxData boxData in boxDataList) {

                        using(StreamWriter sw = File.AppendText(path + timestmp + randomNumber + ".txt")) {

                            sw.WriteLine(boxData.id + " " +
                                            boxData.pos_x + " " +
                                            boxData.pos_y + " " +
                                            boxData.width + " " +
                                            boxData.height);

                        }
                    }
                }
                // no object detected
                else sendMessage("<color=red>Nothing capured. No object in sight.</color>");

            }
            // file already exits => player didn't wait ar least
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
    public float pos_x;
    public float pos_y;
    public float width;
    public float height;

    public BoxData(int objId, float posX, float posY, float width, float height) {
        
        this.id = objId;
        this.pos_x = posX;
        this.pos_y = posY;
        this.width = width;
        this.height = height;
    }
}