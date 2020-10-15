using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ScreenshotTaker: MonoBehaviour {

    [SerializeField] private Camera camera = null;
    private GameObject[] detectables;

    // Start is called before the first frame update
    void Start()
    {
        detectables = GameObject.FindGameObjectsWithTag("Detectable");
    }

    // Update is called once per frame
    void Update()
    {
        
        if(Input.GetKeyDown(KeyCode.E)) {

            // Take screenshot and save raw version
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] screenshotAsPNG = screenshot.EncodeToPNG();

            string path = "D:\\UnityProjects\\Object-Detection-with-Unity\\screenshots\\";

            if(!Directory.Exists(path)) {

                Directory.CreateDirectory(path);

            }

            File.WriteAllBytes(path + "srn.png", screenshotAsPNG);

            foreach(GameObject detectable in detectables) {

                Renderer renderer = detectable.GetComponent<Renderer>();

                if(renderer.isVisible) {

                    // TODO

                }

            }

        }

    }
}
