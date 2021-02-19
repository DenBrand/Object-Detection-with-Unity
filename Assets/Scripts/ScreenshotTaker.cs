//using system.collections;
//using system.collections.generic;
//using unityengine;
//using unityengine.ui;
//using system.io;
//using system;

//public class screenshottaker: monobehaviour {

//    [serializefield] camera playercamera = null;
//    [serializefield] canvas uicanvas = null;
//    [serializefield] list<gameobject> detectables = null;
//    [serializefield] int detecablecount = 0;
//    [serializefield] gameobject ballprefab = null;
//    [serializefield] gameobject cubeprefab = null;
//    [serializefield] gameobject detectablesparent = null;
//    [serializefield] int minimaldetectionsize = 0;
//    [serializefield] int paddingperside = 0;
//    [serializefield] configtransporter configtransporter = null;
//    string trainingdatapath;
//    string yolodatapath;
//    string cascadeclassifierdatapath;
//    string cascadeclassifierdatajsonpath;
//    public int maxmessages;
//    [serializefield] list<message> messagelist = new list<message>();
//    public gameobject contentobject, textobject;
//    public float deathheight;
//    public gameobject player;
//    bool allowcapturing;
//    bool allowemptycaptures;
//    cascadeclassifierdata cascadeclassifierdata;
//    string runid;
//    rundata rundata;

//    // start is called before the first frame update
//    void start() {
//        try {
//            configtransporter = gameobject.find("configtransporter").getcomponent<configtransporter>();
//        }
//        catch(exception ex) {
//            debug.logerror(ex);
//        }

//        cursor.visible = false;
//        allowcapturing = true;
//        allowemptycaptures = true;

//        // specify certain paths
//        if(systeminfo.operatingsystem.startswith("windows")) {
//            trainingdatapath = "training_data\\";
//            yolodatapath = trainingdatapath + "yolo\\";
//            cascadeclassifierdatapath = trainingdatapath + "cascade_classifier\\";
//            cascadeclassifierdatajsonpath = cascadeclassifierdatapath + "training_data.json";
//        }
//        else {
//            trainingdatapath = "training_data/";
//            yolodatapath = trainingdatapath + "yolo/";
//            cascadeclassifierdatapath = trainingdatapath + "cascade_classifier/";
//            cascadeclassifierdatajsonpath = cascadeclassifierdatapath + "training_data.json";
//        }

//        sendmessage("move: wasd");
//        sendmessage("look: mouse");
//        sendmessage("capture: e");
//        sendmessage("zoom: scroll mouse wheel");
//        sendmessage("hide info box: h");
//        sendmessage("toggle capture empty images: p (default: <color=green>on</color>)");
//        sendmessage("close application: esc");
//        sendmessage("<color=red>wait at least one second between captures.</color>");
//        sendmessage("training data are saved in \"" + trainingdatapath + "\".");
//        sendmessage("<color=green>starting capturing session</color>");

//        // instatiate detectables at random positions all over the map
//        for(int i = 0; i < detecablecount; i++) {

//            // gernerate randomly chosen detectables
//            vector3 randomposition = new vector3(unityengine.random.range(-100f, 100f), 10f, unityengine.random.range(-100f, 100f));
//            gameobject chosengameobject = null;
//            if(i % 2 == 0) chosengameobject = cubeprefab;
//            else if(i % 2 == 1) chosengameobject = ballprefab;
//            //else if(i % 2 == 2) chosengameobject = tetraederprefab; // add new detectable class
//            else {
//                debug.logerror("game tried to instantiate a detectable, whose type could not be infered.");
//                throw new argumentnullexception("game tried to instantiate a detectable, whose type could not be infered.");
//            }

//            gameobject detectable = instantiate(chosengameobject,
//                                                    randomposition,
//                                                    quaternion.euler(unityengine.random.range(0f, 180f),
//                                                                        unityengine.random.range(0f, 180f),
//                                                                        unityengine.random.range(0f, 180f)),
//                                                    detectablesparent.transform);

//            try {
//                if(configtransporter.currentmode.modename == "randomize_detectable_colors") {
//                    color newcolor = new color(unityengine.random.range(0f, 1f),
//                                                unityengine.random.range(0f, 1f),
//                                                unityengine.random.range(0f, 1f));

//                    detectable.getcomponent<meshrenderer>().material.setcolor("_color", newcolor);
//                }
//            }
//            catch(exception ex) {
//                debug.log(ex);
//            }

//            detectables.add(detectable);
//        }

//        // load cascade classifier data already existent
//        if(!directory.exists(cascadeclassifierdatapath)) {
//            directory.createdirectory(cascadeclassifierdatapath);
//        }
//        if(file.exists(cascadeclassifierdatajsonpath)) {
//            string jsonstring = file.readalltext(cascadeclassifierdatajsonpath);
//            cascadeclassifierdata = jsonutility.fromjson<cascadeclassifierdata>(jsonstring);
//        }
//        else {
//            cascadeclassifierdata = new cascadeclassifierdata();
//        }

//        // add new rundata entry
//        runid = string.format("{0}_{1}", environment.username.gethashcode(), unityengine.random.range(0, 10000));
//        string version = application.version;
//        rundata = new rundata(runid, configtransporter.currentmode.modename, version);
//        cascadeclassifierdata.rundata.add(rundata);
//    }

//    // update is called once per frame
//    void update() {
//        if(player.transform.position.y <= deathheight) {
//            sendmessage("<color=red>you fell out of bounds. i reset you to the spawn location.</color>");
//            player.transform.position = vector3.zero;
//        }

//        if(input.getkeydown(keycode.escape)) application.quit();

//        if(input.getkeydown(keycode.e)) {
//            if(allowcapturing) {
//                allowcapturing = false;

//                // start coroutine so hide ui for the frame the screen is captured
//                startcoroutine(captureobjects());

//                startcoroutine(allowcapturingafteronesecond(1.1f));
//            }
//            else {
//                sendmessage("<color=red>nothing captured. please wait at least one second. that's because the " +
//                            "images are named with a timestamp messured in seconds. multiple captures " +
//                            "within the same second would lead to overwritten images and faulty labels.</color>");
//            }
//        }

//        if(input.getkeydown(keycode.h)) uicanvas.enabled = !uicanvas.enabled;

//        if(input.getkeydown(keycode.p)) {
//            allowemptycaptures = !allowemptycaptures;
//            if(allowemptycaptures) sendmessage("capturing empty images is toggled <color=green>on</color>");
//            else sendmessage("capturing empty images is toggled <color=red>off</color>.");
//        }

//        // extra mechanism to prevent overwriting images
//        ienumerator allowcapturingafteronesecond(float n) {
//            yield return new waitforsecondsrealtime(n);
//            allowcapturing = true;
//        }

//        ienumerator captureobjects() {

//            // get time stamp as file name
//            system.datetime time = system.datetime.now;
//            string timestmp = time.year + "-"
//                            + time.month + "-"
//                            + time.day + "_"
//                            + time.hour + "h"
//                            + time.minute + "min"
//                            + time.second + "sec";

//            int randomnumber = unityengine.random.range(0, 10000);

//            if(!file.exists(trainingdatapath + timestmp + randomnumber + ".png")) {

//                sendmessage("*snap*");

//                // wait till the last possible moment before screen rendering to hide ui
//                bool canvaswasenabled = uicanvas.enabled;
//                yield return null;
//                uicanvas.enabled = false;

//                // wait for screen rendering to complete
//                yield return new waitforendofframe();
//                if(canvaswasenabled) uicanvas.enabled = true;

//                // take screenshot
//                texture2d screenshot = screencapture.capturescreenshotastexture();
//                byte[] screenshotaspng = screenshot.encodetopng();

//                bool allsizessufficient = true;
//                list<boxdata> boxdatalist = new list<boxdata>();
//                list<positiveelement> positiveelements = new list<positiveelement>();
//                foreach(gameobject detectable in detectables) {

//                    renderer renderer = detectable.getcomponent<renderer>();

//                    // get boundary box
//                    if(renderer.isvisible) {

//                        // get an array of all vertices of the object
//                        mesh mesh = detectable.getcomponent<meshfilter>().mesh;
//                        vector3[] vertices = mesh.vertices;

//                        // since vertices are given relative to their gameobject, we need to convert them into worldspace
//                        for(int i = 0; i < vertices.length; i++) {

//                            vertices[i] = detectable.transform.transformpoint(vertices[i]);

//                        }

//                        float left = screen.width + 1f;
//                        float right = -1f;
//                        float top = -1f;
//                        float bottom = screen.height + 1f;

//                        // with camera.worldtoscreenpoint(vector3 worldpoint) search for the top, bottom, most left and most right point
//                        bool objectinsight = false;
//                        foreach(vector3 vertex in vertices) {

//                            // check if vertex is not behind the camera
//                            if(vector3.dot(playercamera.transform.forward, vertex - playercamera.transform.position) >= 0f) {

//                                // check if corresponding screen point is on screen and if it is a newly found border point candidate
//                                vector3 screenpoint = playercamera.worldtoscreenpoint(vertex);
//                                if(new rect(0, 0, screen.width, screen.height).contains(screenpoint)) {

//                                    // check if vertex is obscured by another object
//                                    bool vertexisobscured = false;
//                                    raycasthit hitinfo;
//                                    if(physics.raycast(playercamera.transform.position,
//                                                        vertex - playercamera.transform.position,
//                                                        out hitinfo,
//                                                        (vertex - playercamera.transform.position).magnitude - 0.01f)) {

//                                        if(hitinfo.collider.gameobject != detectable) {

//                                            vertexisobscured = true;

//                                        }
//                                        else {

//                                            objectinsight = true;

//                                        }
//                                    }

//                                    if(!vertexisobscured) {

//                                        if(screenpoint.x < left) left = screenpoint.x;
//                                        if(screenpoint.x > right) right = screenpoint.x;
//                                        if(screenpoint.y < bottom) bottom = screenpoint.y;
//                                        if(screenpoint.y > top) top = screenpoint.y;

//                                    }
//                                }
//                            }
//                        }

//                        // determine object class
//                        int classid = -1;
//                        if(detectable.name.startswith("cube")) classid = 0;
//                        else if(detectable.name.startswith("ball")) classid = 1;
//                        // else if(detectable.name.startswith("tetraeder")) classid = 2; // add new detectable class
//                        else {
//                            debug.logerror("couldn't infer object class");
//                            throw new system.exception("detected an object whose id cannot be infered.");
//                        }

//                        // add object to data
//                        if(objectinsight) {

//                            // check if object is at least 12 pixels wide and high
//                            bool toosmall = false;
//                            float width = right - left;
//                            float height = top - bottom;
//                            bool wasdetected = left != screen.width + 1f && right != -1f && top != -1f && bottom != screen.height + 1f;
//                            if((mathf.roundtoint(width) < minimaldetectionsize || mathf.roundtoint(height) < minimaldetectionsize) && wasdetected)
//                                toosmall = true;

//                            if(!toosmall) {

//                                // represent detected objects in the info box
//                                string name = detectable.name;
//                                if(detectable.name.contains("cube")) name = "<color=red>" + detectable.name + "</color>";
//                                else if(detectable.name.contains("ball")) name = "<color=cyan>" + detectable.name + "</color>";
//                                sendmessage(name + "(id: " + detectable.getinstanceid() + ") snapped");

//                                // draw the lines
//                                for(int x = (int)left; x <= (int)right; x++) {
//                                    screenshot.setpixel(x, (int)bottom, color.red);
//                                    screenshot.setpixel(x, (int)top, color.red);
//                                }
//                                for(int y = (int)bottom; y <= (int)top; y++) {
//                                    screenshot.setpixel((int)left, y, color.red);
//                                    screenshot.setpixel((int)right, y, color.red);
//                                }

//                                // gather corresponding data

//                                // for yolo
//                                boxdata boxdata = new boxdata(classid,
//                                                                mathf.roundtoint(left),
//                                                                mathf.roundtoint(screen.height - top),
//                                                                width,
//                                                                height);
//                                boxdatalist.add(boxdata);

//                                // add to positives
//                                int cascadeleft = mathf.roundtoint(left) - paddingperside;
//                                if(cascadeleft < 0) cascadeleft = 0;
//                                int cascadetop = mathf.roundtoint(screen.height - top) - paddingperside;
//                                if(cascadetop < 0) cascadetop = 0;
//                                int cascadewidth = mathf.roundtoint(width) + 2 * paddingperside;
//                                if(cascadeleft + cascadewidth > screen.width) cascadewidth = screen.width - cascadeleft;
//                                int cascadeheight = mathf.roundtoint(height) + 2 * paddingperside;
//                                if(cascadetop + cascadeheight > screen.height) cascadeheight = screen.height - cascadetop;

//                                positiveelements.add(new positiveelement(classid,
//                                                                            "positives\\" + timestmp + randomnumber + ".png",
//                                                                            new bbox(cascadeleft, cascadetop, cascadewidth, cascadeheight)));

//                            }
//                            else allsizessufficient = false;
//                        }
//                    }
//                }

//                if(allsizessufficient) {

//                    // add cascade classifier data and for every class,
//                    // check if there is a positive element for that class.
//                    // if no: add image as negative element for that class.
//                    list<negativeelement> negativeelements = new list<negativeelement>();
//                    dictionary<int, bool> classfound = new dictionary<int, bool>();
//                    classfound.add(0, false);
//                    classfound.add(1, false);
//                    foreach(positiveelement poselem in positiveelements) {

//                        if(poselem.classid == 0) {

//                            classfound[0] = true;
//                            rundata.cubes.positives.add(new positivesdata(poselem.path, poselem.bbox));

//                        }
//                        else if(poselem.classid == 1) {

//                            classfound[1] = true;
//                            rundata.balls.positives.add(new positivesdata(poselem.path, poselem.bbox));

//                        }
//                        /*else if(poselem.classid == 2) { // add new detectable class

//                            classfound[2] = true;
//                            rundata.tetraeders.positives.add(new positivesdata(poselem.path, poselem.bbox));

//                        }*/
//                        else {

//                            debug.logerror("the script added a non-existent detectable class to the \"positiveelements\" variable. this error is fatal. please inform dennis about this.");
//                            throw new system.exception("the script added a non-existent detectable class to the \"positiveelements\" variable. this error is fatal. please inform dennis about this.");

//                        }
//                    }

//                    // add negative elements
//                    foreach(int classid in classfound.keys)
//                        if(!classfound[classid])
//                            if(classid == 0)
//                                rundata.cubes.negatives.add(new negativesdata("negatives\\" + timestmp + randomnumber + ".png"));
//                            else if(classid == 1)
//                                rundata.balls.negatives.add(new negativesdata("negatives\\" + timestmp + randomnumber + ".png"));
//                            /*else if(classid == 2) // add new detectable class
//                                rundata.tetraeders.negatives.add(new negativesdata("negatives\\" + timestmp + randomnumber + ".png"));*/
//                            else {

//                                debug.logerror("the script had a non-existent detectable class in the \"classfound.key\" field. this error is fatal. please inform dennis about this.");
//                                throw new system.exception("the script had a non-existent detectable class in the \"classfound.key\" field. this error is fatal. please inform dennis about this.");

//                            }

//                    // if at least one object was detected
//                    if(allowemptycaptures || boxdatalist.count > 0) {

//                        // save unlabeled image
//                        if(!directory.exists(yolodatapath)) {

//                            directory.createdirectory(yolodatapath);

//                        }
//                        file.writeallbytes(yolodatapath + timestmp + randomnumber + ".png", screenshotaspng);

//                        // save labeled image
//                        if(configtransporter.savelabeledimages) {
//                            screenshotaspng = screenshot.encodetopng();
//                            file.writeallbytes(yolodatapath + timestmp + randomnumber + "_labeled.png", screenshotaspng);
//                        }

//                        // save json data
//                        /* string jsonstring = jsonutility.tojson(new boxdatalist(boxdatalist), true);
//                        file.writealltext(path + timestmp + ".json", jsonstring);*/

//                        // save txt file as yolo label
//                        foreach(boxdata boxdata in boxdatalist) {

//                            using(streamwriter sw = file.appendtext(yolodatapath + timestmp + randomnumber + ".txt")) {

//                                sw.writeline(boxdata.id + " " +
//                                                (float)(boxdata.x + boxdata.w / 2) / screen.width + " " +
//                                                (float)(boxdata.y + boxdata.h / 2) / screen.height + " " +
//                                                (float)boxdata.w / screen.width + " " +
//                                                (float)boxdata.h / screen.height);

//                            }
//                        }
//                        if(boxdatalist.count == 0) using(streamwriter sw = file.appendtext(yolodatapath + timestmp + randomnumber + ".txt")) sw.write("");

//                        // overwrite json for cascade classifier
//                        string jsonstring = jsonutility.tojson(cascadeclassifierdata, true);
//                        file.writealltext(cascadeclassifierdatajsonpath, jsonstring);

//                    }
//                    // no object detected and capturing labels without detections is off
//                    else sendmessage("<color=red>nothing captured. no object in sight. if you want to capture empty images too, please press the <color=blue>p</color> button</color>");

//                }
//                else sendmessage("<color=red>nothing captured. there is at least one object, whose width or height " +
//                                    "in pixels is smaller than " + minimaldetectionsize + ". please ensure the sizes of all visible detectables is sufficient.</color>");

//            }
//            // file already exits => player didn't wait at least
//            // one seconds and randomly generated number was the same
//            else sendmessage("<color=red>nothing captured. please wait at least one second. that's because the " +
//                              "screenshots are named with a timestamp messured in seconds. multiple screenshots " +
//                              "within the same second would lead to overwritten images and faulty labels.</color>");
//        }
//    }

//    public void sendmessage(string text) {
//        if(messagelist.count >= maxmessages) {
//            destroy(messagelist[0].textobject.gameobject);
//            messagelist.remove(messagelist[0]);
//        }

//        message newmessage = new message();
//        newmessage.text = text;
//        gameobject newtext = instantiate(textobject, contentobject.transform);
//        newmessage.textobject = newtext.getcomponent<text>();
//        newmessage.textobject.text = newmessage.text;
//        messagelist.add(newmessage);
//    }
//}

//[system.serializable]
//public class message {
//    public string text;
//    public text textobject;
//}

///*[system.serializable]
//class boxdatalist {
//    public list<boxdata> data;

//    public boxdatalist(list<boxdata> data) {
//        this.data = data;
//    }
//}

//[system.serializable]
//class boxdata {
//    public int id;
//    public float x;
//    public float y;
//    public float w;
//    public float h;

//    public boxdata(int objid, float posx, float posy, float width, float height) {
//        this.id = objid;
//        this.x = posx;
//        this.y = posy;
//        this.w = width;
//        this.h = height;
//    }
//}

//// neccessary classes for cascading classifier data
//[system.serializable]
//class cascadeclassifierdata {
//    public list<rundata> rundata;

//    public cascadeclassifierdata() {
//        this.rundata = new list<rundata>();
//    }
//}

//[system.serializable]
//class rundata {
//    public string runid;
//    public string mode;
//    public string version;
//    public detectabledata cubes;
//    public detectabledata balls;
//    //public list<detectabledata> tetraeders;

//    public rundata(string runid, string mode, string version) {
//        this.runid = runid;
//        this.mode = mode;
//        this.version = version;
//        this.cubes = new detectabledata(); // for cubes
//        this.balls = new detectabledata(); // for balls
//        //this.tetraeders.add(new detectabledata()); // for tetraeders
//    }
//}

//[system.serializable]
//class detectabledata {
//    public list<positivesdata> positives;
//    public list<negativesdata> negatives;

//    public detectabledata() {
//        this.positives = new list<positivesdata>();
//        this.negatives = new list<negativesdata>();
//    }
//}

//[system.serializable]
//class positivesdata {
//    public string path;
//    public bbox boxentry;

//    public positivesdata(string path, bbox bbox) {
//        this.path = path;
//        this.boxentry = bbox;
//    }
//}

//[system.serializable]
//class bbox {
//    public int x;
//    public int y;
//    public int w;
//    public int h;

//    public bbox(int x, int y, int w, int h) {
//        this.x = x;
//        this.y = y;
//        this.w = w;
//        this.h = h;
//    }
//}

//class positiveelement {
//    public int classid;
//    public string path;
//    public bbox bbox;

//    public positiveelement(int classid, string path, bbox bbox) {
//        this.classid = classid;
//        this.path = path;
//        this.bbox = bbox;
//    }
//}

//class negativeelement {
//    public int classid;
//    public string path;

//    public negativeelement(int classid, string path) {
//        this.classid = classid;
//        this.path = path;
//    }
//}

//[system.serializable]
//class negativesdata {
//    public string path;

//    public negativesdata(string path) {
//        this.path = path;
//    }
//}*/
