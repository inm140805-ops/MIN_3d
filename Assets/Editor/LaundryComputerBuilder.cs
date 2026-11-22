using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Authored on an integer pixel grid: no image generation, filters, or downloaded artwork.
public static class LaundryComputerBuilder
{
    private const string Art = "Assets/Resources/LaundryPixel/";
    private static readonly Color32 Ink = new Color32(27, 44, 52, 255), Shadow = new Color32(54, 79, 87, 255),
        Cream = new Color32(255, 241, 205, 255), White = new Color32(255, 253, 235, 255),
        Mint = new Color32(133, 215, 184, 255), Teal = new Color32(53, 117, 112, 255),
        Gold = new Color32(247, 195, 92, 255), Coral = new Color32(240, 137, 105, 255);
    private static TMP_FontAsset font;
    private static Texture2D canvas;

    private static void Begin(int w, int h)
    {
        canvas = new Texture2D(w, h, TextureFormat.RGBA32, false);
        canvas.filterMode = FilterMode.Point;
        canvas.SetPixels32(new Color32[w * h]);
    }
    private static void R(int x, int y, int w, int h, Color32 c)
    {
        for (int yy = Mathf.Max(0, y); yy < Mathf.Min(canvas.height, y + h); yy++)
            for (int xx = Mathf.Max(0, x); xx < Mathf.Min(canvas.width, x + w); xx++) canvas.SetPixel(xx, yy, c);
    }
    private static void Plate(int x, int y, int w, int h, Color32 fill)
    {
        R(x + 3, y, w - 6, h, Ink); R(x, y + 3, w, h - 6, Ink);
        R(x + 3, y + 3, w - 6, h - 6, Shadow);
        R(x + 4, y + 5, w - 8, h - 9, fill);
        R(x + 5, y + h - 5, w - 10, 1, White);
        R(x + 4, y + 5, 1, h - 10, White);
    }
    private static void Save(string name, Vector4 border)
    {
        canvas.Apply(); File.WriteAllBytes(Art + name + ".png", canvas.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(canvas);
        AssetDatabase.ImportAsset(Art + name + ".png", ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + name + ".png");
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true; importer.spritePixelsPerUnit = 100; importer.spriteBorder = border;
        importer.SaveAndReimport();
    }
    private static void Glyph(int x, int y, string[] rows, int scale, Color32 color)
    {
        for (int row = 0; row < rows.Length; row++) for (int col = 0; col < rows[row].Length; col++)
            if (rows[row][col] == '#') R(x + col * scale, y + (rows.Length - row - 1) * scale, scale, scale, color);
    }

    public static string Paint()
    {
        Directory.CreateDirectory(Art);
        Begin(96, 80); Plate(0, 0, 96, 80, Cream); R(7, 7, 82, 66, Ink); R(9, 9, 78, 62, Teal);
        foreach (int x in new[] { 3, 90 }) foreach (int y in new[] { 3, 74 }) { R(x, y, 3, 3, Shadow); R(x, y + 1, 3, 1, White); }
        Save("TerminalBezel", new Vector4(12, 12, 12, 12));
        Begin(64, 24); Plate(0, 0, 64, 24, Shadow); Save("StockRow", Vector4.one * 7);
        Begin(64, 24); Plate(0, 0, 64, 24, Teal); R(4, 6, 3, 12, Gold); Save("StockSelected", Vector4.one * 8);
        Begin(64, 24); Plate(0, 0, 64, 24, Teal); R(5, 5, 54, 2, Mint); Save("ActionButton", Vector4.one * 7);
        Begin(64, 24); Plate(0, 0, 64, 24, Gold); R(5, 5, 54, 2, Coral); Save("GoldButton", Vector4.one * 7);
        Begin(96, 32); Plate(0, 0, 96, 32, Teal); Plate(5, 4, 25, 25, Cream);
        Glyph(13, 11, new[] { "###", "#..", "###", "#..", "###" }, 2, Ink);
        R(34, 6, 54, 1, Mint); R(89, 11, 2, 10, Mint); Save("Interact", Vector4.zero);
        Begin(160, 64); Plate(0, 10, 160, 54, Cream);
        R(70, 5, 16, 8, Ink); R(72, 2, 10, 9, Ink); R(74, 0, 4, 8, Ink);
        R(73, 8, 9, 6, Cream); R(75, 5, 5, 8, Cream);
        R(7, 15, 3, 38, Mint); R(12, 55, 133, 2, White);
        R(146, 17, 3, 3, Teal); R(150, 21, 2, 2, Mint); Save("ChatBubble", Vector4.zero);
        Color32[] clothing = { White, Gold, new Color32(108, 179, 233, 255), Mint, White, new Color32(158, 197, 231, 255), Shadow };
        for (int i = 0; i < 7; i++)
        {
            Begin(32, 32);
            R(9, 5, 14, 20, Ink); R(5, 17, 22, 9, Ink); R(3, 16, 7, 7, Ink); R(22, 16, 7, 7, Ink);
            R(10, 6, 12, 18, clothing[i]); R(6, 18, 20, 6, clothing[i]); R(4, 17, 5, 5, clothing[i]); R(23, 17, 5, 5, clothing[i]);
            R(13, 22, 6, 4, Ink); R(14, 24, 4, 2, Cream); R(10, 6, 12, 2, Teal);
            if (i == 1) for (int y = 10; y < 21; y += 4) R(11, y, 10, 1, Cream);
            if (i == 2) { R(11, 25, 10, 3, Ink); R(12, 26, 8, 2, clothing[i]); R(16, 8, 1, 13, White); }
            if (i == 4 || i == 6) { R(15, 9, 2, 12, i == 6 ? Coral : Teal); R(11, 21, 3, 3, White); R(18, 21, 3, 3, White); }
            if (i == 3) { R(11, 25, 10, 3, Ink); R(12, 26, 8, 2, Mint); R(13, 10, 6, 3, Teal); }
            if (i == 5) { for (int y = 9; y < 22; y += 4) R(10, y, 12, 1, White); R(13, 8, 1, 14, White); R(19, 8, 1, 14, White); }
            Save("Cloth" + i, Vector4.zero);
        }
        Begin(64, 40); R(0, 0, 64, 40, Ink); R(2, 2, 60, 36, Teal);
        Glyph(5, 29, new[] { "###.#.#.###.##.", "#...#.#.#.#.#.#", "###.###.#.#.##.", "..#.#.#.#.#.#..", "###.#.#.###.#.." }, 1, White);
        for (int i = 0; i < 5; i++) { R(6, 5 + i * 4, 11, 2, Cream); R(21, 5 + i * 4, 14 + i * 5, 2, i == 0 ? Gold : Mint); }
        R(56, 31, 3, 3, Gold); Save("MonitorScreen", Vector4.zero);
        Begin(48, 20); Plate(0, 0, 48, 20, Cream);
        for (int y = 7; y <= 13; y += 3) for (int x = 6; x <= 40; x += 4) R(x, y, 3, 2, Shadow);
        R(15, 4, 18, 2, Shadow); Save("Keyboard", Vector4.zero);
        return "16 hand-pixel PNGs painted and imported with point filtering";
    }

    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go, "Build laundry computer");
        var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size;
        return r;
    }
    private static UnityEngine.UI.Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color)
    {
        var r = Rect(name, parent, position, size); var img = r.gameObject.AddComponent<UnityEngine.UI.Image>();
        img.sprite = sprite; img.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        img.color = color; img.raycastTarget = false; return img;
    }
    private static TextMeshProUGUI Label(string name, Transform parent, string value, Vector2 position, Vector2 size, float point, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var r = Rect(name, parent, position, size); var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSharedMaterial = font.material; t.text = value; t.fontSize = point; t.fontStyle = FontStyles.Bold;
        t.color = Color.white; t.alignment = align; t.raycastTarget = false; t.margin = Vector4.zero;
        t.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black); t.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, .45f);
        t.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, .12f); t.UpdateMeshPadding(); return t;
    }
    private static UnityEngine.UI.Button Button(string name, Transform parent, Vector2 position, Vector2 size, string caption, out TextMeshProUGUI text, bool gold = false)
    {
        var image = Panel(name, parent, position, size, Sprite(gold ? "GoldButton" : "ActionButton"), Color.white);
        image.raycastTarget = true; var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(1,.92f,.68f); colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.7f,.8f,.75f); colors.disabledColor = new Color(.5f,.6f,.59f,.8f); button.colors = colors;
        text = Label("Caption", image.transform, caption, Vector2.zero, size - new Vector2(24, 10), 26, TextAlignmentOptions.Center);
        return button;
    }
    private static Material Material(string name, Color color, Texture texture = null)
    {
        string path = Art + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find(texture != null ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color; if (texture != null) mat.SetTexture("_BaseMap", texture);
        if (name == "Computer Screen") { mat.SetTextureScale("_BaseMap", new Vector2(-1, -1)); mat.SetTextureOffset("_BaseMap", Vector2.one); }
        EditorUtility.SetDirty(mat); return mat;
    }
    private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    private static void Set(SerializedObject so, string name, UnityEngine.Object value) { so.FindProperty(name).objectReferenceValue = value; }
    private static void SetArray(SerializedObject so, string name, UnityEngine.Object[] values)
    {
        var p = so.FindProperty(name); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    [MenuItem("Tools/Laundry/Toggle Computer Preview")]
    private static void TogglePreview()
    {
        if (EditorApplication.isPlaying) return;
        var terminal = UnityEngine.Object.FindAnyObjectByType<ShopComputer>();
        if (terminal == null) return;
        var panel = (GameObject)new SerializedObject(terminal).FindProperty("panel").objectReferenceValue;
        Undo.RecordObject(panel, "Toggle computer preview"); panel.SetActive(!panel.activeSelf);
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(panel.scene);
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Build only outside Play Mode");
        if (GameObject.Find("Shop Computer") != null) return "Computer already exists; edit its existing objects instead of rebuilding";
        font = Resources.Load<TMP_FontAsset>("LaundrySkin/Galmuri11 SDF");
        var hud = UnityEngine.Object.FindAnyObjectByType<ShopHUD>();
        var hudRoot = (GameObject)new SerializedObject(hud).FindProperty("root").objectReferenceValue;
        var canvasRoot = hudRoot.GetComponentInParent<Canvas>();
        if (canvasRoot == null) throw new InvalidOperationException("Shop HUD has no screen Canvas");
        var boxes = UnityEngine.Object.FindAnyObjectByType<BoxManager>(); var loop = UnityEngine.Object.FindAnyObjectByType<CoreLoopManager>();
        if (loop.GetComponent<ShopEconomy>() == null) Undo.AddComponent<ShopEconomy>(loop.gameObject);
        var computer = new GameObject("Shop Computer"); Undo.RegisterCreatedObjectUndo(computer, "Build laundry computer");
        computer.transform.position = new Vector3(10.2f, .28f, 12.7f); computer.transform.rotation = Quaternion.Euler(0,90,0);
        var terminal = computer.AddComponent<ShopComputer>();
        var cream = Material("Computer Cream", Cream); var teal = Material("Computer Teal", Teal); var dark = Material("Computer Ink", Ink);
        Cube("Desk Top", computer.transform, new Vector3(0,1,0), new Vector3(2,.14f,1.05f), cream, true);
        foreach(float x in new[]{-.78f,.78f}) foreach(float z in new[]{-.35f,.35f}) Cube("Desk Leg",computer.transform,new Vector3(x,.48f,z),new Vector3(.12f,.96f,.12f),teal,true);
        Cube("Monitor Foot",computer.transform,new Vector3(0,1.12f,.05f),new Vector3(.68f,.1f,.48f),dark);
        Cube("Monitor Stand",computer.transform,new Vector3(0,1.33f,.1f),new Vector3(.22f,.38f,.2f),teal);
        Cube("Monitor Shell",computer.transform,new Vector3(0,1.77f,0),new Vector3(1.46f,1.04f,.3f),cream,true);
        Cube("Display Recess",computer.transform,new Vector3(0,1.8f,-.16f),new Vector3(1.28f,.81f,.03f),dark);
        Cube("Pixel Screen",computer.transform,new Vector3(0,1.8f,-.182f),new Vector3(1.17f,.71f,.01f),Material("Computer Screen",Color.white,Sprite("MonitorScreen").texture));
        Cube("Power Light",computer.transform,new Vector3(.51f,1.33f,-.16f),new Vector3(.09f,.04f,.02f),Material("Computer LED",Mint));
        Cube("Keyboard",computer.transform,new Vector3(0,1.11f,-.34f),new Vector3(1.08f,.06f,.36f),cream);
        var keys = GameObject.CreatePrimitive(PrimitiveType.Quad); keys.name = "Keyboard Top";
        keys.transform.SetParent(computer.transform,false); keys.transform.localPosition = new Vector3(0,1.146f,-.34f);
        keys.transform.localRotation = Quaternion.Euler(90,0,0); keys.transform.localScale = new Vector3(1.08f,.36f,1);
        keys.GetComponent<Renderer>().sharedMaterial = Material("Keyboard Paint",Color.white,Sprite("Keyboard").texture);
        UnityEngine.Object.DestroyImmediate(keys.GetComponent<Collider>());
        var point = new GameObject("Use Position").transform; point.SetParent(computer.transform); point.position = new Vector3(9f,.28f,12.7f);
        var world = new GameObject("Computer Prompt",typeof(RectTransform),typeof(Canvas),typeof(BillboardUi)); world.transform.SetParent(computer.transform,false);
        world.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; world.transform.localPosition = new Vector3(0,1.8f,-.3f);
        world.transform.localScale = Vector3.one * .7f;
        var prompt = Panel("Interact",world.transform,Vector2.zero,new Vector2(1.65f,.55f),Sprite("Interact"),Color.white);
        prompt.type = UnityEngine.UI.Image.Type.Simple;
        Label("Action",prompt.transform,"가게 관리",new Vector2(.23f,0),new Vector2(1.08f,.35f),.16f,TextAlignmentOptions.Center);
        var overlay = Panel("Shop Computer UI",canvasRoot.transform,Vector2.zero,new Vector2(1920,1080),null,new Color(.04f,.09f,.12f,.8f));
        overlay.rectTransform.anchorMin = Vector2.zero; overlay.rectTransform.anchorMax = Vector2.one; overlay.rectTransform.sizeDelta = Vector2.zero;
        overlay.raycastTarget = true;
        var window = Panel("Terminal Window",overlay.transform,Vector2.zero,new Vector2(1500,900),Sprite("TerminalBezel"),Color.white);
        window.pixelsPerUnitMultiplier = .25f;
        Label("Title",window.transform,"세탁소 관리",new Vector2(-485,365),new Vector2(370,62),40);
        Label("OS",window.transform,"LAUNDRY / STOCK & WORKSHOP",new Vector2(-460,315),new Vector2(420,36),20);
        var funds = Label("Operating Funds",window.transform,"운영자금  6,000",new Vector2(295,355),new Vector2(470,64),34,TextAlignmentOptions.Right);
        TextMeshProUGUI closeLabel; var close = Button("Close",window.transform,new Vector2(620,355),new Vector2(150,62),"닫기 ESC",out closeLabel); closeLabel.fontSize=22;
        UnityEventTools.AddPersistentListener(close.onClick,terminal.Close);
        Label("Stock Heading",window.transform,"의류 재고",new Vector2(-445,250),new Vector2(450,50),30);
        Label("Upgrade Heading",window.transform,"가게 설비",new Vector2(345,250),new Vector2(560,50),30);
        var buttons = new UnityEngine.UI.Button[7]; var counts = new TMP_Text[7];
        for(int i=0;i<7;i++)
        {
            var row = Panel("Stock " + i,window.transform,new Vector2(-340,182-i*66),new Vector2(650,58),Sprite(i==0?"StockSelected":"StockRow"),Color.white);row.raycastTarget=true;
            buttons[i]=row.gameObject.AddComponent<UnityEngine.UI.Button>(); buttons[i].targetGraphic=row;
            var icon=Panel("Clothing Icon",row.transform,new Vector2(-278,0),new Vector2(48,48),Sprite("Cloth"+i),Color.white);icon.preserveAspect=true;
            Label("Clothing Name",row.transform,boxes.boxDatas[i].BoxName,new Vector2(-90,0),new Vector2(290,46),28);
            counts[i]=Label("Count",row.transform,"6 / 6",new Vector2(205,0),new Vector2(215,46),26,TextAlignmentOptions.Right);
            UnityEventTools.AddIntPersistentListener(buttons[i].onClick,terminal.SelectStock,i);
        }
        TextMeshProUGUI restockText; var restock=Button("Restock",window.transform,new Vector2(-340,-287),new Vector2(650,62),"품목을 선택해 발주하세요",out restockText,true);
        UnityEventTools.AddPersistentListener(restock.onClick,terminal.RestockSelected);
        string[] titles={"01  창고 확장","02  거래처 계약","03  프리미엄 포장"};
        string[] defaults={"품목별 보관량 6 → 9벌","한 벌 발주 단가 300 → 250","주문당 운영자금 +1,000 → +1,250"};
        var upgrades=new UnityEngine.UI.Button[3];var descriptions=new TMP_Text[3];var prices=new TMP_Text[3];
        for(int i=0;i<3;i++)
        {
            var card=Panel("Upgrade "+i,window.transform,new Vector2(355,133-i*174),new Vector2(600,158),Sprite("StockRow"),Color.white);
            Label("Upgrade Title",card.transform,titles[i],new Vector2(0,48),new Vector2(550,40),28);
            descriptions[i]=Label("Description",card.transform,"LV 0 / 2 · "+defaults[i],new Vector2(0,7),new Vector2(550,40),21);
            TextMeshProUGUI price;upgrades[i]=Button("Purchase",card.transform,new Vector2(0,-43),new Vector2(550,48),"업그레이드 · "+new[]{4000,5000,6000}[i].ToString("N0"),out price);price.fontSize=24;prices[i]=price;
        }
        UnityEventTools.AddPersistentListener(upgrades[0].onClick,terminal.UpgradeCapacity);UnityEventTools.AddPersistentListener(upgrades[1].onClick,terminal.UpgradeSupply);UnityEventTools.AddPersistentListener(upgrades[2].onClick,terminal.UpgradeService);
        var notice=Label("Notice",window.transform,"재고와 설비는 다음 날에도 유지됩니다.",new Vector2(0,-339),new Vector2(1330,32),25,TextAlignmentOptions.Center);
        var info=Label("Economy Help",window.transform,"주문당 자금 +1,000 · 발주 단가 300\n오늘 매출과 별도로 모이는 가게 운영자금입니다.",new Vector2(0,-380),new Vector2(1330,42),19,TextAlignmentOptions.Center);
        var so=new SerializedObject(terminal);Set(so,"usePosition",point);Set(so,"prompt",world);Set(so,"panel",overlay.gameObject);Set(so,"window",window.rectTransform);
        Set(so,"fundsText",funds);Set(so,"infoText",info);Set(so,"noticeText",notice);Set(so,"restockText",restockText);Set(so,"restockButton",restock);
        Set(so,"normalRow",Sprite("StockRow"));Set(so,"selectedRow",Sprite("StockSelected"));SetArray(so,"stockCounts",counts);SetArray(so,"stockButtons",buttons);SetArray(so,"upgradeDescriptions",descriptions);SetArray(so,"upgradePrices",prices);SetArray(so,"upgradeButtons",upgrades);so.ApplyModifiedPropertiesWithoutUndo();
        overlay.gameObject.SetActive(false); world.SetActive(false);
        EditorSceneManager.MarkSceneDirty(computer.scene);EditorSceneManager.SaveScene(computer.scene);AssetDatabase.SaveAssets();
        return "World computer, saved Canvas UI, seven stock rows and three upgrades created";
    }
}
