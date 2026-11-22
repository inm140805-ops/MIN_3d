using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Editor authoring only: UI and navigation references are serialized in SampleScene.
public static class LaundryTutorialBuilder
{
    static TMP_FontAsset font;
    static Material desktop, outline;
    static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent,false);r.sizeDelta=size;return r;
    }
    static void Stretch(RectTransform r, Vector2 min, Vector2 max)
    {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=min;r.offsetMax=max;}
    static TMP_Text Label(string name,Transform parent,string text,float size,bool white=false)
    {
        var t=Rect(name,parent,Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font;t.fontSharedMaterial=white?outline:desktop;t.color=white?Color.white:Color.black;
        t.fontSize=size;t.fontStyle=FontStyles.Bold;t.text=text;t.raycastTarget=false;
        t.textWrappingMode=TextWrappingModes.Normal;t.alignment=TextAlignmentOptions.MidlineLeft;
        return t;
    }
    static object Field(Object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(SerializedObject so,string name,Object value)=>so.FindProperty(name).objectReferenceValue=value;
    [MenuItem("Laundry/Build contextual tutorial")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Build outside Play Mode.");
        var computer=Object.FindAnyObjectByType<ShopComputer>();var workflow=Object.FindAnyObjectByType<LaundryWorkflow>();
        var canvas=((GameObject)Field(computer,"panel")).GetComponentInParent<Canvas>();
        var old=canvas.transform.Find("First Shift Tutorial");if(old)Object.DestroyImmediate(old.gameObject);
        font=Resources.Load<TMP_FontAsset>("LaundrySkin/Galmuri11 SDF");
        string missing;
        font.TryAddCharacters(new string(System.IO.File.ReadAllText("Assets/Scripts/LaundryTutorial.cs").Where(c=>c>=' ').Distinct().ToArray()),out missing);
        EditorUtility.SetDirty(font);
        desktop=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/LaundryRetro/Desktop Font.mat");
        outline=AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/LaundryOperations/NPC Prompt Outline.mat");
        var root=Rect("First Shift Tutorial",canvas.transform,Vector2.zero);Stretch(root,Vector2.zero,Vector2.zero);
        var tutorial=root.gameObject.AddComponent<LaundryTutorial>();var so=new SerializedObject(tutorial);
        var panel=Rect("Operating Guide",root,new Vector2(540,230));panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(0,1);panel.anchoredPosition=new Vector2(32,-330);
        var image=panel.gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>("LaundryRetro/Raised");image.type=Image.Type.Sliced;image.raycastTarget=false;
        var bar=Rect("Title Bar",panel,Vector2.zero);bar.anchorMin=new Vector2(0,1);bar.anchorMax=Vector2.one;bar.pivot=new Vector2(.5f,1);bar.sizeDelta=new Vector2(-12,40);bar.anchoredPosition=new Vector2(0,-6);
        var blue=bar.gameObject.AddComponent<Image>();blue.color=new Color32(0,0,128,255);blue.raycastTarget=false;
        var heading=Label("Heading",bar,"영업을 시작하세요",26,true);Stretch(heading.rectTransform,new Vector2(12,0),new Vector2(-12,0));
        var body=Label("Instruction",panel,"카운터의 영업 표지판에 가까이 가서 E를 누르세요.\n손님은 옷을 맡기거나 세탁된 옷을 찾으러 옵니다.",25);Stretch(body.rectTransform,new Vector2(18,45),new Vector2(-18,-53));
        var footer=Label("Progress",panel,"운영 실습 0 / 4    ·    F1 안내 끄기",20);footer.color=new Color32(55,55,55,255);
        footer.rectTransform.anchorMin=footer.rectTransform.anchorMax=footer.rectTransform.pivot=Vector2.zero;footer.rectTransform.anchoredPosition=new Vector2(18,9);footer.rectTransform.sizeDelta=new Vector2(504,30);
        var arrowRoot=Rect("Objective Pointer",root,Vector2.zero);arrowRoot.anchoredPosition=new Vector2(0,150);
        var arrow=Rect("Pixel Arrow",arrowRoot,new Vector2(52,68));var graphic=arrow.gameObject.AddComponent<LaundryTutorialArrow>();graphic.color=new Color32(255,225,82,255);graphic.raycastTarget=false;
        var label=Label("Destination",arrowRoot,"영업 표지판",24,true);label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(300,72);label.rectTransform.anchoredPosition=new Vector2(0,-77);
        var help=Label("Reopen Hint",root,"F1 운영 안내 다시 보기",23,true);help.rectTransform.anchorMin=help.rectTransform.anchorMax=help.rectTransform.pivot=new Vector2(0,1);help.rectTransform.anchoredPosition=new Vector2(32,-330);help.rectTransform.sizeDelta=new Vector2(420,40);help.gameObject.SetActive(false);
        Set(so,"panel",panel.gameObject);Set(so,"panelRect",panel);Set(so,"arrowRoot",arrowRoot.gameObject);Set(so,"arrow",arrow);Set(so,"arrowCanvas",root);
        Set(so,"heading",heading);Set(so,"message",body);Set(so,"footer",footer);Set(so,"arrowLabel",label);Set(so,"helpHint",help);
        Set(so,"terminalPoint",(Transform)Field(computer,"usePosition"));Set(so,"deliveryPoint",(Transform)Field(workflow,"parcelUsePoint"));Set(so,"supplyPoint",(Transform)Field(workflow,"supplyUsePoint"));
        Set(so,"opening",(Transform)Field(Object.FindAnyObjectByType<CoreLoopManager>(),"openOBJ"));
        Set(so,"terminalVisual",computer.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Pixel Screen"));
        Set(so,"deliveryVisual",(Transform)Field(workflow,"parcel"));Set(so,"supplyVisual",((TMP_Text[])Field(workflow,"supplyLabels"))[0].transform);
        var source=new SerializedObject(workflow);so.CopyFromSerializedProperty(source.FindProperty("stations"));
        var window=(RectTransform)Field(computer,"window");
        Set(so,"orderButton",((Button)Field(computer,"restockButton")).transform);
        foreach(var pair in new[]{new[]{"closeComputer","Close"},new[]{"suppliesTab","Supplies tab"},new[]{"clothingTab","Clothing tab"},new[]{"decreaseQuantity","Quantity minus"}})
        {
            var match=window.GetComponentsInChildren<RectTransform>(true).First(t=>t.name==pair[1]);Set(so,pair[0],match);
        }
        var rows=(Button[])Field(computer,"stockButtons");var rowProp=so.FindProperty("stockRows");rowProp.arraySize=rows.Length;for(int i=0;i<rows.Length;i++)rowProp.GetArrayElementAtIndex(i).objectReferenceValue=rows[i].transform;
        Vector2[] nodes={new Vector2(6.45f,3),new Vector2(6.45f,7.6f),new Vector2(6.45f,12.65f),new Vector2(6.9f,12.65f),new Vector2(6.9f,15),new Vector2(6.9f,17.72f),new Vector2(6.9f,22.57f),new Vector2(6.9f,26.45f),new Vector2(6.9f,27.7f),new Vector2(11,27.7f),new Vector2(11,25),new Vector2(11,22),new Vector2(11,19),new Vector2(11,16.5f),new Vector2(12.1f,15),new Vector2(12.1f,13.6f),new Vector2(-11.5f,3),new Vector2(-11.5f,8.6f),new Vector2(-9.8f,8.6f)};
        var np=so.FindProperty("routeNodes");np.arraySize=nodes.Length;for(int i=0;i<nodes.Length;i++)np.GetArrayElementAtIndex(i).vector3Value=new Vector3(nodes[i].x,.3f,nodes[i].y);
        int[] edges={0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13,4,4,14,14,15,0,16,16,17,17,18};
        var ep=so.FindProperty("routeEdges");ep.arraySize=edges.Length;for(int i=0;i<edges.Length;i++)ep.GetArrayElementAtIndex(i).intValue=edges[i];
        so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
    }
}
