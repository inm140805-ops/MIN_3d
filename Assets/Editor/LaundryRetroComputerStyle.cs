using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Original pixel artwork inspired by classic desktop controls. Edits the saved UI in place.
public static class LaundryRetroComputerStyle
{
    private const string Folder = "Assets/Resources/LaundryRetro/";
    private static readonly Color32 Gray = new Color32(192,192,192,255), Dark = new Color32(64,64,64,255),
        Shadow = new Color32(128,128,128,255), Navy = new Color32(0,0,128,255), Light = new Color32(223,223,223,255);
    private static Texture2D pixels;
    private static void Begin(int w, int h) { pixels = new Texture2D(w,h,TextureFormat.RGBA32,false); pixels.SetPixels32(new Color32[w*h]); }
    private static void Fill(int x,int y,int w,int h,Color color)
    {
        for(int yy=y;yy<y+h;yy++) for(int xx=x;xx<x+w;xx++) pixels.SetPixel(xx,yy,color);
    }
    private static void Bevel(bool inset)
    {
        Fill(0,0,12,12,inset?Color.white:Dark); Fill(0,1,11,11,inset?Shadow:Color.white);
        Fill(1,1,10,10,inset?Light:Shadow); Fill(1,2,9,9,inset?Dark:Light); Fill(2,2,8,8,Gray);
    }
    private static void Save(string name, int border = 0)
    {
        pixels.Apply(); File.WriteAllBytes(Folder+name+".png",pixels.EncodeToPNG()); Object.DestroyImmediate(pixels);
        AssetDatabase.ImportAsset(Folder+name+".png",ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+name+".png");
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100; importer.spriteBorder=Vector4.one*border;
        importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
    }
    private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder+name+".png");
    private static void Paint()
    {
        Directory.CreateDirectory(Folder);
        Begin(12,12); Bevel(false); Save("Raised",3);
        Begin(12,12); Bevel(true); Save("Inset",3);
        Begin(12,12); Bevel(false); Fill(2,2,8,8,new Color32(211,218,229,255)); Save("Hover",3);
        Begin(4,4); Fill(0,0,4,4,Color.white); Save("ListRow");
        Begin(4,4); Fill(0,0,4,4,Navy); Save("SelectedRow");
        Begin(64,4); for(int x=0;x<64;x++) Fill(x,0,1,4,Color.Lerp(Navy,new Color32(16,132,208,255),x/63f)); Save("TitleBar");
        Begin(16,16); Fill(2,1,12,14,Dark); Fill(2,2,11,13,Color.white); Fill(3,2,10,12,Gray);
        Fill(4,11,8,2,Navy); Fill(11,11,1,1,Color.green); Fill(5,4,6,6,Shadow);
        Fill(6,3,4,8,Shadow); Fill(4,5,8,4,Shadow); Fill(5,5,6,4,Navy); Fill(6,4,4,6,Navy);
        Fill(6,7,3,2,new Color32(80,200,255,255)); Save("LaundryApp");
        Begin(96,64); Fill(0,0,96,64,new Color32(0,128,128,255));
        Fill(0,0,96,7,Gray); Fill(0,6,96,1,Color.white); Fill(3,1,19,4,Light); Fill(4,2,3,2,Navy);
        Fill(13,12,72,44,Dark); Fill(13,13,71,43,Color.white); Fill(14,13,70,42,Gray);
        Fill(16,46,66,7,Navy); Fill(76,47,5,5,Gray); Fill(18,17,38,26,Shadow); Fill(19,17,37,25,Color.white);
        Fill(20,35,35,5,Navy); for(int y=19;y<34;y+=5) Fill(22,y,23,2,Shadow);
        Fill(60,33,19,8,Light); Fill(60,21,19,8,Light); Save("Desktop");
    }
    private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var found=parent.Find(name); var r=found!=null?(RectTransform)found:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f); r.anchoredPosition=position;r.sizeDelta=size; return r;
    }
    private static UnityEngine.UI.Image Panel(Transform parent,string name,Vector2 position,Vector2 size,string sprite)
    {
        var r=Rect(parent,name,position,size); var img=r.GetComponent<UnityEngine.UI.Image>();if(img==null)img=r.gameObject.AddComponent<UnityEngine.UI.Image>();
        img.sprite=Art(sprite); img.type=img.sprite.border==Vector4.zero?UnityEngine.UI.Image.Type.Simple:UnityEngine.UI.Image.Type.Sliced;
        img.pixelsPerUnitMultiplier=.5f; img.color=Color.white; img.raycastTarget=false; return img;
    }
    private static void Move(Transform parent,string name,Vector2 position,Vector2 size) { var r=(RectTransform)parent.Find(name);r.anchoredPosition=position;r.sizeDelta=size; }
    private static void SkinButton(UnityEngine.UI.Button b)
    {
        var img=b.GetComponent<UnityEngine.UI.Image>();img.sprite=Art("Raised");img.type=UnityEngine.UI.Image.Type.Sliced;img.pixelsPerUnitMultiplier=.5f;img.color=Color.white;
        b.transition=UnityEngine.UI.Selectable.Transition.SpriteSwap;
        var state=b.spriteState;state.highlightedSprite=Art("Hover");state.selectedSprite=Art("Hover");state.pressedSprite=Art("Inset");state.disabledSprite=Art("Raised");b.spriteState=state;
    }
    [MenuItem("Tools/Laundry/Apply Retro Computer Style")]
    private static void ApplyFromMenu() { Apply(); }

    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Apply in edit mode");
        var terminal=Object.FindAnyObjectByType<ShopComputer>();if(terminal==null)throw new System.InvalidOperationException("No shop computer in scene");
        Paint();
        var so=new SerializedObject(terminal);var window=(RectTransform)so.FindProperty("window").objectReferenceValue;
        Undo.RegisterFullObjectHierarchyUndo(window.gameObject,"Retro computer design");
        var bg=window.GetComponent<UnityEngine.UI.Image>();bg.sprite=Art("Raised");bg.pixelsPerUnitMultiplier=.5f;bg.color=Color.white;
        var font=Resources.Load<TMP_FontAsset>("LaundrySkin/Galmuri11 SDF");
        var face=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Desktop Font.mat");
        if(face==null) { face=new Material(font.material);face.name="Desktop Font";AssetDatabase.CreateAsset(face,Folder+"Desktop Font.mat"); }
        face.SetFloat(ShaderUtilities.ID_OutlineWidth,0);face.SetFloat(ShaderUtilities.ID_FaceDilate,.05f);EditorUtility.SetDirty(face);
        foreach(var text in window.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color=Color.black;text.fontStyle=FontStyles.Bold;
            text.fontSharedMaterial=face;text.UpdateMeshPadding();
        }
        var titleBar=Panel(window,"Desktop Title Bar",new Vector2(0,400),new Vector2(1468,66),"TitleBar");titleBar.transform.SetAsFirstSibling();
        Panel(window,"Laundry App Icon",new Vector2(-690,400),new Vector2(42,42),"LaundryApp");
        Move(window,"Title",new Vector2(-60,400),new Vector2(1160,52));var title=window.Find("Title").GetComponent<TMP_Text>();title.text="세탁소 관리 - Laundry.exe";title.color=Color.white;title.fontSize=32;
        Move(window,"Close",new Vector2(683,400),new Vector2(62,48));var close=window.Find("Close").GetComponent<UnityEngine.UI.Button>();SkinButton(close);
        var closeText=close.GetComponentInChildren<TMP_Text>();closeText.text="×";closeText.fontSize=34;closeText.rectTransform.sizeDelta=new Vector2(52,42);
        Move(window,"OS",new Vector2(-400,337),new Vector2(610,40));window.Find("OS").GetComponent<TMP_Text>().text="LAUNDRY SYSTEM  /  재고 및 설비 관리";
        Move(window,"Operating Funds",new Vector2(390,337),new Vector2(620,44));window.Find("Operating Funds").GetComponent<TMP_Text>().fontSize=28;
        var line=Panel(window,"Toolbar Divider",new Vector2(0,299),new Vector2(1424,5),"Inset");line.transform.SetSiblingIndex(1);
        var table=Panel(window,"Inventory List Border",new Vector2(-340,-17),new Vector2(670,482),"Inset");table.transform.SetSiblingIndex(1);
        for(int i=0;i<7;i++)
        {
            var row=window.Find("Stock "+i).GetComponent<UnityEngine.UI.Image>();row.sprite=Art(i==0?"SelectedRow":"ListRow");row.type=UnityEngine.UI.Image.Type.Simple;
            foreach(var text in row.GetComponentsInChildren<TMP_Text>(true))text.color=i==0?Color.white:Color.black;
            var b=row.GetComponent<UnityEngine.UI.Button>();var colors=b.colors;colors.normalColor=colors.highlightedColor=colors.selectedColor=Color.white;colors.pressedColor=new Color(.8f,.8f,.8f);b.colors=colors;
        }
        for(int i=0;i<3;i++)
        {
            var card=window.Find("Upgrade "+i).GetComponent<UnityEngine.UI.Image>();card.sprite=Art("Inset");card.pixelsPerUnitMultiplier=.7f;
            SkinButton(card.GetComponentInChildren<UnityEngine.UI.Button>(true));
        }
        SkinButton(window.Find("Restock").GetComponent<UnityEngine.UI.Button>());
        var status=Panel(window,"Status Bar",new Vector2(0,-380),new Vector2(1424,108),"Inset");status.transform.SetSiblingIndex(1);
        Move(window,"Notice",new Vector2(0,-346),new Vector2(1330,32));
        Move(window,"Economy Help",new Vector2(0,-388),new Vector2(1330,42));
        window.Find("Notice").GetComponent<TMP_Text>().fontSize=23;
        so.FindProperty("normalRow").objectReferenceValue=Art("ListRow");so.FindProperty("selectedRow").objectReferenceValue=Art("SelectedRow");so.ApplyModifiedPropertiesWithoutUndo();
        var screen=terminal.transform.Find("Pixel Screen").GetComponent<Renderer>().sharedMaterial;screen.SetTexture("_BaseMap",Art("Desktop").texture);EditorUtility.SetDirty(screen);
        string missing;font.TryAddCharacters("세탁소 관리 - Laundry.exe × LAUNDRY SYSTEM / 재고 및 설비 관리",out missing);EditorUtility.SetDirty(font);
        EditorSceneManager.MarkSceneDirty(terminal.gameObject.scene);EditorSceneManager.SaveScene(terminal.gameObject.scene);AssetDatabase.SaveAssets();
        return "Retro desktop style applied to existing scene objects; callbacks preserved";
    }
}
