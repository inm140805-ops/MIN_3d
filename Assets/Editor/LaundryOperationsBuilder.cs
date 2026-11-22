using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Authoring tool only: every visual and interaction reference is saved into the scene.
public static class LaundryOperationsBuilder
{
    const string Art = "Assets/Environment/LaundryOperations/";
    static TMP_FontAsset font;
    static Material type, enamel, teal, dark, cardboard, wood, metal;
    static Transform root;
    static Material Mat(string name, string hex)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Art + name + ".mat");
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, Art + name + ".mat"); }
        Color c; ColorUtility.TryParseHtmlString(hex, out c); m.color = c; m.SetFloat("_Smoothness", .18f); m.enableInstancing = true;
        EditorUtility.SetDirty(m); return m;
    }
    static Transform Group(string name, Transform parent, Vector3 position)
    { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t; }
    static GameObject Box(string name, Transform parent, Vector3 p, Vector3 size, Material material, bool solid = false)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = p; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); return g;
    }
    static TMP_Text Text3D(string name, Transform parent, string text, Vector3 p, Vector2 size, float point, float yaw = 0)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro)); g.transform.SetParent(parent, false);
        g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var t = g.GetComponent<TextMeshPro>(); t.font = font; t.fontSharedMaterial = type; t.fontStyle = FontStyles.Bold;
        t.text = text; t.fontSize = point; t.color = new Color32(40, 66, 63, 255); t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = size; t.textWrappingMode = TextWrappingModes.Normal;
        if (name.IndexOf("status", StringComparison.OrdinalIgnoreCase) >= 0)
        { t.enableAutoSizing = true; t.fontSizeMin = point * .55f; t.fontSizeMax = point; }
        return t;
    }
    static RectTransform Rect(string name, Transform parent, Vector2 p, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = p; r.sizeDelta = size; return r;
    }
    static Image Panel(string name, Transform parent, Vector2 p, Vector2 size, string sprite = "Raised")
    {
        var r = Rect(name, parent, p, size); var i = r.gameObject.AddComponent<Image>();
        i.sprite = Resources.Load<Sprite>("LaundryRetro/" + sprite); i.type = Image.Type.Sliced; i.raycastTarget = false; return i;
    }
    static TMP_Text Label(string name, Transform parent, string text, Vector2 p, Vector2 size, int point = 24)
    {
        var t = Rect(name, parent, p, size).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/LaundryRetro/Desktop Font.mat");
        t.text = text; t.fontSize = point; t.color = Color.black; t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false; t.alignment = TextAlignmentOptions.MidlineLeft; return t;
    }
    static void Button(string name, Transform parent, string text, Vector2 p, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var image = Panel(name, parent, p, size); image.raycastTarget = true;
        var b = image.gameObject.AddComponent<Button>(); b.targetGraphic = image;
        var c = b.colors; c.highlightedColor = new Color32(255, 234, 153, 255); c.selectedColor = c.highlightedColor; b.colors = c;
        Label("Caption", b.transform, text, Vector2.zero, size - new Vector2(16, 4), 23).alignment = TextAlignmentOptions.Center;
        UnityEventTools.AddPersistentListener(b.onClick, action);
    }
    static void Set(SerializedObject so, string field, UnityEngine.Object value) { so.FindProperty(field).objectReferenceValue = value; }
    static void Array(SerializedObject so, string field, UnityEngine.Object[] values)
    { var a = so.FindProperty(field); a.arraySize = values.Length; for (int i = 0; i < values.Length; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
    static Transform[] Route(Transform parent, string name, Vector3[] points)
    { var g = Group(name, parent, Vector3.zero); return points.Select((p, i) => Group("Waypoint " + i, g, p)).ToArray(); }

    static void PaintSupplies()
    {
        for (int n = 0; n < 3; n++)
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color32[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                bool body = n == 0 ? x >= 8 && x < 25 && y >= 4 && y < 24 : x >= 5 && x < 27 && y >= 5 && y < 25;
                bool border = body && (x == (n == 0 ? 8 : 5) || x == (n == 0 ? 24 : 26) || y == (n == 0 ? 4 : 5) || y == (n == 0 ? 23 : 24));
                Color32 c = new Color32(0, 0, 0, 0);
                if (body) c = border ? new Color32(45, 64, 69, 255) : n == 0 ? new Color32(106, 186, 178, 255) : n == 1 ? new Color32(225, 230, 238, 255) : new Color32(210, 169, 111, 255);
                if (body && x >= 10 && x <= 22 && y >= 10 && y <= 18) c = new Color32(255, 248, 221, 255);
                if (n == 0 && x >= 13 && x <= 20 && y >= 24 && y <= 27) c = new Color32(51, 83, 91, 255);
                if (n == 1 && x >= 9 && x <= 22 && y % 4 == 1 && y > 8 && y < 22) c = new Color32(101, 142, 177, 255);
                if (n == 2 && x >= 14 && x <= 17 && y >= 5 && y <= 24) c = new Color32(247, 224, 181, 255);
                pixels[y * 32 + x] = c;
            }
            tex.SetPixels32(pixels); tex.Apply(); string path = "Assets/Resources/LaundryRetro/Supply" + n + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
    }
    static AudioClip Bell()
    {
        string path = Art + "Delivery Bell.wav"; const int rate = 22050, samples = 11025;
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            for (int i = 0; i < samples; i++) { double t = (double)i / rate; double phase = t < .22 ? t : t - .22; double frequency = t < .22 ? 660 : 880;
                writer.Write((short)(Math.Sin(phase * frequency * Math.PI * 2) * Math.Exp(-phase * 12) * Math.Min(1, phase * 300) * 14000)); }
        }
        AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    [MenuItem("Tools/Laundry/Build Operations")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit mode required");
        if (UnityEngine.Object.FindAnyObjectByType<LaundryWorkflow>() != null) throw new InvalidOperationException("Operations already authored");
        Directory.CreateDirectory(Art); font = Resources.Load<TMP_FontAsset>("LaundrySkin/Galmuri11 SDF");
        string missing; font.TryAddCharacters("세제 건조 시트 포장재 의뢰 접수 반환 준비 작업 중 배송 주문 수량 사용 가능 정리 보관함 도착까지 완료 #×0123456789", out missing);
        type = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/LaundryBuilding/Sign Lettering.mat");
        enamel = Mat("Enamel", "#EFE9D9"); teal = Mat("Petrol", "#456F69"); dark = Mat("Rubber", "#354449");
        cardboard = Mat("Cardboard", "#BC9367"); wood = Mat("Oak", "#B58D64"); metal = Mat("Steel", "#A5B6B7");
        PaintSupplies();
        root = Group("Laundry Operations", null, Vector3.zero); Undo.RegisterCreatedObjectUndo(root.gameObject, "Author laundry operations");
        var workflow = GameObject.Find("CoreLoopManager").AddComponent<LaundryWorkflow>(); var so = new SerializedObject(workflow);
        var building = GameObject.Find("Laundry Building").transform;
        var equipment = building.Find("04 Laundry Equipment"); var stationObjects = new LaundryWorkflow.Station[5];
        for (int i = 0; i < 3; i++)
        {
            var washer = equipment.Find("Washer " + (i + 1) + " - decorative"); washer.name = "Washer " + (i + 1);
            var display = washer.Find("Cycle display").GetComponent<TMP_Text>(); display.text = "READY";
            float x = 5.15f + i * 2.35f;
            var label = Text3D("Washer status " + i, root, "세탁기 " + (i + 1) + "\n사용 가능", new Vector3(x, 2.7f, 30.37f), new Vector2(2.1f, .7f), 2.3f);
            stationObjects[i] = new LaundryWorkflow.Station { title = "세탁기 " + (i + 1), location = LaundryLocation.Washer, machine = i,
                usePoint = Group("Washer use " + i, root, new Vector3(x, .28f, 28.4f)), drum = washer.Find("Chrome door rim"), status = label };
            Box("Utility feed " + i, root, new Vector3(x, 2.75f, 31.65f), new Vector3(.075f, 1, .075f), metal);
        }
        Box("Washer utility rail", root, new Vector3(7.5f, 3.23f, 31.45f), new Vector3(5.1f, .075f, .075f), metal);
        var dryer = Group("Dryer", root, new Vector3(12.4f, 0, 10.25f));
        Box("Cabinet", dryer, new Vector3(0, 1.23f, 0), new Vector3(1.66f, 1.9f, 1.08f), enamel, true);
        Box("Control fascia", dryer, new Vector3(0, 1.96f, .56f), new Vector3(1.45f, .3f, .06f), teal);
        var ring = new GameObject("Dryer door", typeof(MeshFilter), typeof(MeshRenderer)).transform; ring.SetParent(dryer, false);
        ring.localPosition = new Vector3(0, 1.2f, .6f); ring.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Environment/LaundryBuilding/Washer Ring.asset"); ring.GetComponent<Renderer>().sharedMaterial = metal;
        Box("Door glass", dryer, new Vector3(0, 1.2f, .56f), new Vector3(.78f, .78f, .03f), dark);
        var dryerStatus = Text3D("Dryer status", dryer, "건조기\n사용 가능", new Vector3(0, 2.55f, 0), new Vector2(2.3f, .65f), 2.2f, 180);
        stationObjects[3] = new LaundryWorkflow.Station { title = "건조기", location = LaundryLocation.Dryer, usePoint = Group("Dryer use", root, new Vector3(12.4f, .28f, 12)), status = dryerStatus, drum = ring };
        var table = Group("Folding table", root, new Vector3(4.85f, 0, 14.4f));
        Box("Worktop", table, new Vector3(0, 1.19f, 0), new Vector3(1.5f, .12f, .8f), wood, true);
        foreach (float x in new[] { -.6f, .6f }) foreach (float z in new[] { -.3f, .3f }) Box("Leg", table, new Vector3(x, .7f, z), new Vector3(.08f, .85f, .08f), teal);
        Box("Storage shelf", table, new Vector3(0, .52f, 0), new Vector3(1.4f, .07f, .75f), enamel);
        var tableLabel = Text3D("Table status", table, "정리·포장대\n사용 가능", new Vector3(.77f, 1.02f, 0), new Vector2(1.5f, .4f), 1.6f, -90);
        stationObjects[4] = new LaundryWorkflow.Station { title = "정리·포장대", location = LaundryLocation.Table, usePoint = Group("Table use", root, new Vector3(6.35f, .28f, 14.4f)), status = tableLabel };
        var cabinet = Group("Supply cabinet", root, new Vector3(13.9f, 0, 13.4f));
        Box("Cabinet back", cabinet, new Vector3(.42f, 1.45f, 0), new Vector3(.08f, 2.3f, 2.4f), teal);
        foreach (float z in new[] { -1.2f, 1.2f }) Box("Cabinet side", cabinet, new Vector3(0, 1.45f, z), new Vector3(.9f, 2.3f, .07f), enamel, true);
        var supplyLabels = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            float y = .5f + i * .68f; Box("Shelf " + i, cabinet, new Vector3(0, y, 0), new Vector3(.9f, .06f, 2.4f), wood);
            supplyLabels[i] = Text3D("Supply label " + i, cabinet, ShopEconomy.SupplyNames[i] + " 3/6", new Vector3(-.47f, y + .13f, 0), new Vector2(2, .25f), 1.6f, 90);
            for (int j = 0; j < 3; j++)
            {
                Box("Stock package", cabinet, new Vector3(0, y + .23f, -.7f + j * .65f), new Vector3(.4f, .4f, .38f), i == 0 ? teal : i == 1 ? enamel : cardboard);
                if (i == 0) Box("Detergent cap", cabinet, new Vector3(0, y + .46f, -.7f + j * .65f), new Vector3(.16f, .08f, .16f), dark);
            }
        }
        Text3D("Supply heading", cabinet, "세탁용품", new Vector3(-.46f, 2.65f, 0), new Vector2(2.4f, .4f), 2.4f, 90);
        Set(so, "supplyUsePoint", Group("Supply use", root, new Vector3(12.1f, .28f, 13.6f))); Array(so, "supplyLabels", supplyLabels);
        var stations = so.FindProperty("stations"); stations.arraySize = stationObjects.Length;
        for (int i = 0; i < stationObjects.Length; i++)
        {
            var s = stations.GetArrayElementAtIndex(i); var v = stationObjects[i]; s.FindPropertyRelative("title").stringValue = v.title;
            s.FindPropertyRelative("location").enumValueIndex = (int)v.location; s.FindPropertyRelative("machine").intValue = v.machine;
            s.FindPropertyRelative("usePoint").objectReferenceValue = v.usePoint; s.FindPropertyRelative("drum").objectReferenceValue = v.drum; s.FindPropertyRelative("status").objectReferenceValue = v.status;
        }
        BuildTruck(so); BuildHUD(so); BuildComputer(); RemakeFacade(building);
        var loop = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<CoreLoopManager>());
        loop.FindProperty("dayDuration").floatValue = 420; loop.FindProperty("needMoney").floatValue = 20000; loop.ApplyModifiedPropertiesWithoutUndo();
        var events = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<LaundryEvents>());
        events.FindProperty("expressDeadline").floatValue = 120; events.FindProperty("offerDuration").floatValue = 100; events.ApplyModifiedPropertiesWithoutUndo();
        var audio = workflow.gameObject.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
        Set(so, "notificationAudio", audio); Set(so, "arrivalBell", Bell()); so.ApplyModifiedPropertiesWithoutUndo();
        var boxes = UnityEngine.Object.FindAnyObjectByType<BoxManager>();
        for (int i = 0; i < boxes.boxDatas.Length; i++) { var data = new SerializedObject(boxes.boxDatas[i]); data.FindProperty("icon").objectReferenceValue = Resources.Load<Sprite>("LaundryPixel/Cloth" + i); data.ApplyModifiedPropertiesWithoutUndo(); }
        PrefabUtility.ApplyPrefabInstance(building.gameObject, InteractionMode.AutomatedAction);
        EditorUtility.SetDirty(font); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(root.gameObject.scene); EditorSceneManager.SaveOpenScenes();
    }

    static void BuildTruck(SerializedObject so)
    {
        var truck = Group("Laundry delivery truck", root, new Vector3(-17.8f, 0, -75)); Set(so, "truck", truck);
        Box("Chassis", truck, new Vector3(0, .6f, 0), new Vector3(2.05f, .25f, 4.8f), dark);
        foreach (float x in new[] { -1.06f, 1.06f }) Box("Cargo side", truck, new Vector3(x, 1.8f, -.8f), new Vector3(.08f, 2.1f, 3.1f), enamel, true);
        Box("Cargo roof", truck, new Vector3(0, 2.85f, -.8f), new Vector3(2.24f, .08f, 3.14f), enamel);
        Box("Cargo floor", truck, new Vector3(0, .79f, -.8f), new Vector3(2.04f, .08f, 3.02f), dark);
        Box("Cargo front", truck, new Vector3(0, 1.8f, .71f), new Vector3(2.04f, 2.1f, .08f), enamel);
        Box("Cab", truck, new Vector3(0, 1.43f, 1.5f), new Vector3(2.1f, 1.55f, 1.5f), teal, true);
        Box("Roof", truck, new Vector3(0, 2.27f, 1.43f), new Vector3(2.18f, .14f, 1.55f), enamel);
        Box("Windscreen", truck, new Vector3(0, 1.84f, 2.26f), new Vector3(1.82f, .55f, .03f), dark);
        Box("Front bumper", truck, new Vector3(0, .7f, 2.34f), new Vector3(2.15f, .22f, .16f), metal);
        foreach (float x in new[] { -1.07f, 1.07f })
        {
            Box("Side window", truck, new Vector3(x, 1.85f, 1.5f), new Vector3(.03f, .55f, 1), dark);
            Box("Mirror", truck, new Vector3(x * 1.15f, 1.8f, 1.95f), new Vector3(.13f, .3f, .2f), dark);
            foreach (float z in new[] { -1.55f, 1.45f })
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wheel.name = "Wheel"; wheel.transform.SetParent(truck, false);
                wheel.transform.localPosition = new Vector3(x, .5f, z); wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                wheel.transform.localScale = new Vector3(.9f, .15f, .9f); wheel.GetComponent<Renderer>().sharedMaterial = dark; UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());
                Box("Hub", truck, new Vector3(x * 1.15f, .5f, z), new Vector3(.04f, .3f, .3f), metal);
            }
            Text3D("Delivery livery", truck, "뽀송 물류\nLAUNDRY SUPPLY", new Vector3(x * 1.04f, 1.9f, -.8f), new Vector2(2.5f, .8f), 2.8f, x < 0 ? 90 : -90);
        }
        foreach (float x in new[] { -.75f, .75f }) Box("Headlight", truck, new Vector3(x, 1.12f, 2.27f), new Vector3(.42f, .23f, .04f), enamel);
        var gate = Group("Tailgate pivot", truck, new Vector3(0, .82f, -2.38f)); Box("Tailgate", gate, new Vector3(0, .75f, 0), new Vector3(2.02f, 1.5f, .06f), teal); Set(so, "tailgate", gate);
        var body = truck.gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        Array(so, "arrivalRoute", Route(root, "Arrival route", new[] { new Vector3(-17.8f,0,-75),new Vector3(-17.8f,0,-12),new Vector3(-17.8f,0,1),new Vector3(-16.5f,0,3.6f),new Vector3(-14.1f,0,5.3f),new Vector3(-11,0,5.8f),new Vector3(-5,0,5.8f) }));
        Array(so, "departureRoute", Route(root, "Departure route", new[] {new Vector3(-5,0,5.8f),new Vector3(0,0,5.8f),new Vector3(2.5f,0,4.5f),new Vector3(3.1f,0,2),new Vector3(1,0,0),new Vector3(-12,0,-3),new Vector3(-16,0,-5),new Vector3(-17.8f,0,-8),new Vector3(-17.8f,0,-75)}));
        var parcel = Group("Delivery parcel", root, new Vector3(-9.8f, .32f, 5.8f)); Set(so, "parcel", parcel);
        var closed = Group("Closed parcel", parcel, Vector3.zero); Box("Cardboard", closed, new Vector3(0, .45f, 0), new Vector3(1.15f, .9f, .9f), cardboard);
        Box("Packing tape", closed, new Vector3(0, .906f, 0), new Vector3(.18f, .01f, .9f), enamel);
        var open = Group("Opened parcel", parcel, Vector3.zero);
        foreach (float x in new[] { -.56f, .56f }) Box("Box side", open, new Vector3(x, .45f, 0), new Vector3(.04f, .9f, .9f), cardboard);
        foreach (float z in new[] { -.43f, .43f }) Box("Box end", open, new Vector3(0, .45f, z), new Vector3(1.1f, .9f, .04f), cardboard);
        Box("Box contents", open, new Vector3(0, .35f, 0), new Vector3(.9f, .5f, .7f), enamel);
        var flap = Box("Open lid", open, new Vector3(-.8f, .88f, 0), new Vector3(.48f, .035f, .9f), cardboard); flap.transform.localRotation = Quaternion.Euler(0, 0, -24);
        open.gameObject.SetActive(false); Set(so, "closedParcel", closed.gameObject); Set(so, "openParcel", open.gameObject);
        Set(so, "parcelStart", Group("Unload start", root, new Vector3(-7.2f, 1.15f, 5.8f)));
        Set(so, "parcelEnd", Group("Unload destination", root, new Vector3(-9.8f, .32f, 5.8f)));
        Set(so, "parcelUsePoint", Group("Parcel use", root, new Vector3(-9.8f, 0, 8.6f))); parcel.gameObject.SetActive(false);
        foreach (float z in new[] { 3.3f, 8.1f }) Box("Delivery bay marking", root, new Vector3(-6, .012f, z), new Vector3(9, .015f, .08f), enamel);
        Box("Bay end marking", root, new Vector3(-10.45f, .012f, 5.7f), new Vector3(.08f, .015f, 4.9f), enamel);
        var bay = Text3D("Delivery bay sign", root, "DELIVERY\n배송 구역", new Vector3(-6, .025f, 7.5f), new Vector2(3, 1), 4); bay.transform.localRotation = Quaternion.Euler(90, 0, 0);
        // A parked van exists beyond the playable street; it drives into view instead of spawning beside the shop.
        Box("Depot road extension", root, new Vector3(-17.8f, -.02f, -60), new Vector3(6, .035f, 45), dark);
        Box("Depot side", root, new Vector3(-21.1f, 2, -78), new Vector3(.2f, 4, 10), teal);
        Box("Depot roof", root, new Vector3(-17.8f, 4, -78), new Vector3(6.8f, .2f, 10), teal);
    }
    static void BuildHUD(SerializedObject so)
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<ShopHUD>(); var canvas = hud.GetComponentInParent<Canvas>();
        if (!canvas) canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.renderMode != RenderMode.WorldSpace);
        var g = Rect("Laundry Operations HUD", canvas.transform, Vector2.zero, new Vector2(1920, 1080));
        g.anchorMin = Vector2.zero; g.anchorMax = Vector2.one; g.sizeDelta = Vector2.zero;
        var delivery = Panel("Delivery status", g, new Vector2(-690, 290), new Vector2(470, 112));
        var dt = Label("Delivery text", delivery.transform, "배송 중 · 도착까지 01:00\n세제 ×3", Vector2.zero, new Vector2(440, 92), 24);
        var jobs = Panel("Laundry tickets", g, new Vector2(620, 260), new Vector2(610, 170));
        Label("Jobs heading", jobs.transform, "접수 의뢰 / 세탁 → 건조 → 포장 → 반환", new Vector2(0, 60), new Vector2(575, 34), 22);
        var jt = Label("Jobs text", jobs.transform, "접수한 의뢰가 여기에 표시됩니다", new Vector2(0, -17), new Vector2(575, 115), 23);
        var action = Panel("Workstation prompt", g, new Vector2(0, -300), new Vector2(570, 66));
        var key = Panel("E key", action.transform, new Vector2(-245, 0), new Vector2(44, 44)); Label("Key", key.transform, "E", Vector2.zero, new Vector2(30, 40), 28).alignment = TextAlignmentOptions.Center;
        var at = Label("Action", action.transform, "세탁기 · 옷 넣기", new Vector2(28, 0), new Vector2(475, 50), 26);
        Set(so, "deliveryPanel", delivery.gameObject); Set(so, "deliveryText", dt); Set(so, "jobsPanel", jobs.gameObject); Set(so, "jobsText", jt); Set(so, "actionPanel", action.gameObject); Set(so, "actionText", at);
        delivery.gameObject.SetActive(false); jobs.gameObject.SetActive(false); action.gameObject.SetActive(false);
        var player = UnityEngine.Object.FindAnyObjectByType<Player>(); var camera = player.GetComponentInChildren<Camera>();
        var cargo = Group("Held laundry cargo", camera.transform, new Vector3(0, -.6f, 1.3f));
        var box = Group("Supply parcel", cargo, Vector3.zero); Box("Parcel", box, Vector3.zero, new Vector3(.65f, .42f, .45f), cardboard); Box("Tape", box, new Vector3(0, .215f, 0), new Vector3(.08f, .01f, .45f), enamel);
        var garment = Group("Customer garment", cargo, Vector3.zero);
        Box("Folded body", garment, Vector3.zero, new Vector3(.64f, .18f, .42f), enamel);
        Box("Sleeve left", garment, new Vector3(-.28f, .08f, 0), new Vector3(.2f, .09f, .35f), teal);
        Box("Sleeve right", garment, new Vector3(.28f, .08f, 0), new Vector3(.2f, .09f, .35f), teal);
        Box("Ticket tag", cargo, new Vector3(.16f, .14f, -.23f), new Vector3(.25f, .18f, .01f), enamel);
        var cargoText = Text3D("Ticket number", cargo, "#0001", new Vector3(.16f, .14f, -.24f), new Vector2(.24f, .15f), .9f);
        Set(so, "cargoRoot", cargo.gameObject); Set(so, "carriedParcel", box.gameObject); Set(so, "carriedGarment", garment.gameObject); Set(so, "cargoText", cargoText); cargo.gameObject.SetActive(false);
    }
    static void BuildComputer()
    {
        var computer = UnityEngine.Object.FindAnyObjectByType<ShopComputer>(); var so = new SerializedObject(computer);
        var window = (RectTransform)so.FindProperty("window").objectReferenceValue;
        window.Find("Stock Heading").GetComponent<TMP_Text>().text = "물품 발주 · 60초 배송";
        Button("Supplies tab", window, "세탁용품", new Vector2(-510, 252), new Vector2(300, 44), computer.SelectSupplies);
        Button("Clothing tab", window, "의류 재고", new Vector2(-180, 252), new Vector2(300, 44), computer.SelectClothing);
        window.Find("Stock Heading").gameObject.SetActive(false);
        for (int i = 0; i < 7; i++)
        {
            var row = (RectTransform)window.Find("Stock " + i); row.anchoredPosition = new Vector2(-340, 190 - i * 58); row.sizeDelta = new Vector2(650, 52);
        }
        Button("Quantity minus", window, "−", new Vector2(-580, -226), new Vector2(100, 40), computer.DecreaseQuantity);
        Button("Quantity plus", window, "+", new Vector2(-100, -226), new Vector2(100, 40), computer.IncreaseQuantity);
        var quantity = Label("Quantity", window, "수량  3", new Vector2(-340, -226), new Vector2(330, 40), 24); quantity.alignment = TextAlignmentOptions.Center;
        Set(so, "quantityText", quantity); so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void Polish()
    {
        font = Resources.Load<TMP_FontAsset>("LaundrySkin/Galmuri11 SDF");
        type = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/LaundryBuilding/Sign Lettering.mat");
        enamel = AssetDatabase.LoadAssetAtPath<Material>(Art + "Enamel.mat"); teal = AssetDatabase.LoadAssetAtPath<Material>(Art + "Petrol.mat");
        var workflow = UnityEngine.Object.FindAnyObjectByType<LaundryWorkflow>(); var so = new SerializedObject(workflow);
        root = GameObject.Find("Laundry Operations").transform;
        root.Find("Table use").position = new Vector3(6.65f, .28f, 14.4f);
        if (!root.Find("Delivery apron"))
        {
            var paving = Mat("Service paving", "#939C99");
            Box("Delivery apron", root, new Vector3(-6.5f, .004f, 3.8f), new Vector3(24, .008f, 10), paving);
            Box("Staff walkway", root, new Vector3(6.45f, .004f, 5.3f), new Vector3(3.6f, .008f, 6), paving);
            var mesh = new Mesh { name = "Staff entrance accessible ramp" };
            mesh.vertices = new[] { new Vector3(-1.8f,0,0),new Vector3(1.8f,0,0),new Vector3(-1.8f,0,1.6f),new Vector3(1.8f,0,1.6f),new Vector3(-1.8f,.015f,0),new Vector3(1.8f,.015f,0),new Vector3(-1.8f,.28f,1.6f),new Vector3(1.8f,.28f,1.6f) };
            mesh.triangles = new[] {4,6,5,5,6,7,0,4,1,1,4,5,2,3,6,3,7,6,0,2,4,2,6,4,1,5,3,3,5,7,0,1,2,1,3,2}; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Art + "Entry ramp.asset");
            var ramp = Group("Staff entrance ramp", root, new Vector3(6.45f,0,7.8f)); ramp.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            ramp.gameObject.AddComponent<MeshRenderer>().sharedMaterial=paving; ramp.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        var stations = so.FindProperty("stations");
        for (int i = 0; i < stations.arraySize; i++)
        {
            var s = stations.GetArrayElementAtIndex(i); var status = (TMP_Text)s.FindPropertyRelative("status").objectReferenceValue;
            if (i < 3) { status.transform.position = new Vector3(5.15f + i * 2.35f, 2.97f, 31.53f); status.rectTransform.sizeDelta = new Vector2(2.1f,.48f); }
            if (i == 3) status.transform.position = new Vector3(12.4f, 2.75f, 9.58f);
            if (!status.transform.Find("Mounted enamel plate"))
                Box("Mounted enamel plate", status.transform, new Vector3(0, 0, .04f), new Vector3(status.rectTransform.sizeDelta.x + .1f, status.rectTransform.sizeDelta.y + .1f, .05f), enamel);
            if (i < 4 || s.FindPropertyRelative("contents").objectReferenceValue != null) continue;
            Transform parent = i < 4 ? (Transform)s.FindPropertyRelative("drum").objectReferenceValue : GameObject.Find("Laundry Operations/Folding table").transform;
            var contents = Group("Laundry inside station", parent, i < 4 ? new Vector3(0, 0, i < 3 ? -.09f : .09f) : new Vector3(0, 1.34f, 0));
            for (int j = 0; j < 3; j++) Box("Folded textile " + j, contents, i < 4 ? new Vector3((j - 1) * .2f, -.16f + j * .09f, 0) : new Vector3(0, j * .06f, 0), i < 4 ? new Vector3(.23f, .13f, .055f) : new Vector3(.65f, .065f, .42f), j % 2 == 0 ? enamel : teal);
            contents.gameObject.SetActive(false); s.FindPropertyRelative("contents").objectReferenceValue = contents.gameObject;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        if (so.FindProperty("parcelLabel").objectReferenceValue == null)
        {
            var parcel = (Transform)so.FindProperty("parcel").objectReferenceValue;
            var label = Text3D("Shipment label", parcel, "배송 물품\n주문 수량 표시", new Vector3(0, .54f, .466f), new Vector2(1.03f, .45f), 1.5f, 180);
            label.enableAutoSizing = true; label.fontSizeMin = .7f; label.fontSizeMax = 1.5f;
            label.margin = new Vector4(.025f, .02f, .025f, .02f);
            Box("Label paper", label.transform, new Vector3(0, 0, .015f), new Vector3(1.08f, .49f, .01f), enamel);
            Set(so, "parcelLabel", label); so.ApplyModifiedPropertiesWithoutUndo();
        }
        var apron = root.Find("Delivery apron"); apron.position = new Vector3(-5.05f,.14f,3.1f); apron.localScale = new Vector3(26.9f,.28f,13);
        if (!apron.GetComponent<BoxCollider>()) apron.gameObject.AddComponent<BoxCollider>();
        root.Find("Staff walkway").gameObject.SetActive(false); root.Find("Staff entrance ramp").gameObject.SetActive(false);
        foreach (Transform t in root)
        {
            if (t.name.StartsWith("Delivery bay marking") || t.name == "Bay end marking" || t.name == "Delivery bay sign")
            { var p = t.position; p.y = t.name == "Delivery bay sign" ? .315f : .302f; t.position = p; }
        }
        foreach (string route in new[]{"Arrival route","Departure route"}) foreach(Transform t in root.Find(route)) { var p=t.position;p.y=.29f;t.position=p; }
        root.Find("Unload destination").position = new Vector3(-9.8f,.32f,5.8f);
        root.Find("Unload start").position = new Vector3(-7.2f,1.15f,5.8f);
        root.Find("Parcel use").position = new Vector3(-9.8f,.29f,8.6f);
        var road = root.Find("Depot road extension"); var rp=road.position;rp.y=.27f;road.position=rp;
        var truck=root.Find("Laundry delivery truck");var tp=truck.position;tp.y=.29f;truck.position=tp;
        foreach (var hub in truck.Cast<Transform>().Where(t=>t.name=="Hub").ToArray())
            hub.SetParent(truck.Cast<Transform>().Where(t=>t.name=="Wheel").OrderBy(t=>Vector3.SqrMagnitude(t.position-hub.position)).First(),true);
        var deliveryParcel = (Transform)so.FindProperty("parcel").objectReferenceValue;
        if (!deliveryParcel.GetComponent<BoxCollider>()) { var collider=deliveryParcel.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,.45f,0);collider.size=new Vector3(1.15f,.9f,.9f); }
        if (!deliveryParcel.GetComponent<Rigidbody>()) { var rb=deliveryParcel.gameObject.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false; }
        GameObject.Find("Open").GetComponent<Collider>().isTrigger=true;
        var computer = UnityEngine.Object.FindAnyObjectByType<ShopComputer>(); var cs = new SerializedObject(computer);
        var window = (RectTransform)cs.FindProperty("window").objectReferenceValue;
        for(int i=0;i<7;i++)
        {
            var row=window.Find("Stock "+i); row.gameObject.SetActive(i<4);
            if(i>=4)continue;
            row.Find("Clothing Name").GetComponent<TMP_Text>().text=i<3?ShopEconomy.SupplyNames[i]:"세탁 소모품 세트";
            row.Find("Clothing Icon").GetComponent<Image>().sprite=Resources.Load<Sprite>("LaundryRetro/Supply"+Mathf.Min(i,2));
            row.Find("Count").GetComponent<TMP_Text>().text="3 / 6 · "+new[]{200,120,160,480}[i];
        }
        ((TMP_Text)cs.FindProperty("restockText").objectReferenceValue).text="세제 ×3 발주 · 600";
        ((TMP_Text)cs.FindProperty("infoText").objectReferenceValue).text="주문 60초 뒤 도착 · 박스에서 꺼내 보관함에 직접 입고";
        var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/NPC.prefab");
        try
        {
            var ui = prefab.GetComponent<NPCUI>(); var data = new SerializedObject(ui);
            if(data.FindProperty("laundryTicketLabel").objectReferenceValue==null)
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"Ticket Text.mat");
                if(!material){material=new Material(font.material);AssetDatabase.CreateAsset(material,Art+"Ticket Text.mat");}
                material.SetFloat(ShaderUtilities.ID_OutlineWidth,.3f); material.SetColor(ShaderUtilities.ID_OutlineColor,Color.black); material.SetFloat(ShaderUtilities.ID_FaceDilate,.1f);
                var label=Rect("Laundry ticket number",prefab.GetComponentInChildren<Canvas>(true).transform,new Vector2(0,3.2f),new Vector2(3,.35f)).gameObject.AddComponent<TextMeshProUGUI>();
                label.font=font;label.fontSharedMaterial=material;label.fontSize=.2f;label.fontStyle=FontStyles.Bold;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
                label.text="#0001 · 세탁 전";label.gameObject.AddComponent<BillboardUi>();label.gameObject.SetActive(false);
                data.FindProperty("laundryTicketLabel").objectReferenceValue=label;data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(material);
            }
            if (data.FindProperty("laundryBundle").objectReferenceValue == null)
            {
                var bones = prefab.GetComponentsInChildren<Transform>(true); var hand = bones.FirstOrDefault(t => t.name.ToLower().Contains("hand") && (t.name.EndsWith(".R") || t.name.ToLower().Contains("right")));
                var bag = Group("Customer laundry bag", hand ? hand : prefab.transform, hand ? new Vector3(0, -.25f, 0) : new Vector3(.62f, .9f, .28f));
                Box("Cloth bag", bag, Vector3.zero, new Vector3(.38f, .45f, .24f), enamel);
                foreach (float x in new[] { -.12f, .12f }) Box("Handle", bag, new Vector3(x, .28f, 0), new Vector3(.04f, .14f, .04f), teal);
                Box("Handle top", bag, new Vector3(0, .35f, 0), new Vector3(.28f, .04f, .04f), teal);
                Box("Folded clothes", bag, new Vector3(0, .24f, 0), new Vector3(.3f, .08f, .19f), teal);
                data.FindProperty("laundryBundle").objectReferenceValue = bag.gameObject; data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/NPC.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var building = GameObject.Find("Laundry Building"); PrefabUtility.ApplyPrefabInstance(building, InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(workflow.gameObject.scene); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
    }

    static void RemakeFacade(Transform building)
    {
        var front = building.Find("02 Street Front");
        foreach (Transform t in front)
        {
            if (t.name.StartsWith("Awning stripe")) { var p = t.position; p.x = 1.8f; t.position = p; t.localScale = new Vector3(3.2f, .09f, .8f); t.GetComponent<Renderer>().sharedMaterial = enamel; }
            if (t.name.StartsWith("Awning valance")) { var p = t.position; p.x = .2f; p.y = 3.93f; t.position = p; t.GetComponent<Renderer>().sharedMaterial = teal; }
        }
        foreach (string name in new[] { "Shop fascia", "Fascia top trim", "Fascia bottom trim" })
        {
            var t = front.Find(name); var p = t.position; p.z = 21.7f; t.position = p; var s = t.localScale; s.z = 14; t.localScale = s;
        }
        front.Find("Since").gameObject.SetActive(false);
        var subtitle = front.Find("Shop subtitle").GetComponent<TMP_Text>(); subtitle.text = "WASH · DRY · FOLD";
        Text3D("Service hours", root, "세탁 · 건조 · 수선\n접수 후 의뢰 번호를 확인하세요", new Vector3(2.87f, 2.45f, 12.4f), new Vector2(3.1f, .8f), 2.2f, 90);
        Box("Services panel", root, new Vector3(3.0f, 2.45f, 12.4f), new Vector3(.08f, 1.2f, 3.5f), enamel);
    }
}
