using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ScreenshotTaker: MonoBehaviour {

    [SerializeField] private Camera camera = null;
    private GameObject[] detectables;

    private string path;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("STARTING CAPTURING SESSION");

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

            Debug.Log("*SNAP*");

            // get time stamp as file name
            System.DateTime time = System.DateTime.Now;
            string timestmp = time.Year + "-"
                            + time.Month + "-"
                            + time.Day + "_"
                            + time.Hour + "h"
                            + time.Minute + "min"
                            + time.Second + "sec";

            // take screenshot and save raw version
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] screenshotAsPNG = screenshot.EncodeToPNG();

            if(!Directory.Exists(path)) {

                Directory.CreateDirectory(path);

            }

            File.WriteAllBytes(path + timestmp + ".png", screenshotAsPNG);

            foreach(GameObject detectable in detectables) {

                Renderer renderer = detectable.GetComponent<Renderer>();

                if(renderer.isVisible) {

                    // TODO

                }

            }

        }

    }
}
