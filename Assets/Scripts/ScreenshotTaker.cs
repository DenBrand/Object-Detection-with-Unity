using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class ScreenshotTaker: MonoBehaviour {

    [SerializeField] private Camera playerCamera = null;
    [SerializeField] private Canvas UICanvas = null;
    private GameObject[] detectables;
    private string path;
    public int maxMessages = 30;
    [SerializeField] List<Message> messageList = new List<Message>();
    public GameObject contentObject, textObject;

    // Start is called before the first frame update
    void Start()
    {
        sendMessage("STARTING CAPTURING SESSION");

        if(SystemInfo.operatingSystem.StartsWith("Windows"))
            path = "screenshots\\";
        else
            path = "screenshots/";

        detectables = GameObject.FindGameObjectsWithTag("Detectable");
    }

    // Update is called once per frame
    void Update()
    {
        
        if(Input.GetKeyDown(KeyCode.E)) {

            // start coroutine so hide UI for the frame the screen is captured
            StartCoroutine(CaptureObjects());

        }

        IEnumerator CaptureObjects() {

            sendMessage("*SNAP*");

            // get time stamp as file name
            System.DateTime time = System.DateTime.Now;
            string timestmp = time.Year + "-"
                            + time.Month + "-"
                            + time.Day + "_"
                            + time.Hour + "h"
                            + time.Minute + "min"
                            + time.Second + "sec";

            // wait till the last possible moment before screen rendering to hide UI
            yield return null;
            UICanvas.enabled = false;

            // wait for screen rendering to complete
            yield return new WaitForEndOfFrame();
            UICanvas.enabled = true;

            // take screenshot and save raw version
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] screenshotAsPNG = screenshot.EncodeToPNG();
            if(!Directory.Exists(path)) {

                Directory.CreateDirectory(path);

            }
            File.WriteAllBytes(path + timestmp + ".png", screenshotAsPNG);

            List<BoxData> boxDataList = new List<BoxData>();

            foreach(GameObject detectable in detectables) {

                Renderer renderer = detectable.GetComponent<Renderer>();

                // get boundary box
                if(renderer.isVisible) {

                    sendMessage(detectable.name + "(id: " + detectable.GetInstanceID() + ") snapped");

                    // get an array of all vertices of the object
                    Mesh mesh = detectable.GetComponent<MeshFilter>().mesh;
                    Vector3[] vertices = mesh.vertices;

                    // since vertices are given relative to their gameobject, we need to convert them into worldSpace
                    for(int i = 0; i < vertices.Length; i++)
                        vertices[i] = detectable.transform.TransformPoint(vertices[i]);

                    float left = Screen.width + 1f;
                    float right = -1f;
                    float top = -1f;
                    float bottom = Screen.height + 1f;

                    // with camera.WorldToScreenPoint(Vector3 worldPoint) search for the top, bottom, most left and most right point
                    foreach(Vector3 vertex in vertices) {

                        // check if vertex is NOT BEHIND the camera
                        if(Vector3.Dot(playerCamera.transform.forward, vertex - playerCamera.transform.position) >= 0f) {

                            // check if corresponding screen point is on screen and if it is a newly found border point candidate
                            Vector3 screenPoint = playerCamera.WorldToScreenPoint(vertex);
                            if(new Rect(0, 0, Screen.width, Screen.height).Contains(screenPoint)) {

                                if(screenPoint.x < left) left = screenPoint.x;
                                if(screenPoint.x > right) right = screenPoint.x;
                                if(screenPoint.y < bottom) bottom = screenPoint.y;
                                if(screenPoint.y > top) top = screenPoint.y;

                            }
                        }
                    }

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
                    BoxData boxData = new BoxData(detectable.GetInstanceID(),
                                                    detectable.name,
                                                    (int)left,
                                                    (int)bottom,
                                                    (int)right - (int)left,
                                                    (int)top - (int)bottom);
                    boxDataList.Add(boxData);

                }
            }

            // save labeled image
            screenshotAsPNG = screenshot.EncodeToPNG();
            File.WriteAllBytes(path + timestmp + "_labeled.png", screenshotAsPNG);

            // save json data
            string jsonString = JsonUtility.ToJson(new BoxDataList(boxDataList), true);
            File.WriteAllText(path + timestmp + ".json", jsonString);

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

    public int obj_id;
    public string obj_name;
    public int pos_x;
    public int pos_y;
    public int width;
    public int height;

    public BoxData(int objId, string objName, int posX, int posY, int width, int height) {
        this.obj_id = objId;
        this.obj_name = objName;
        this.pos_x = posX;
        this.pos_y = posY;
        this.width = width;
        this.height = height;
    }
}