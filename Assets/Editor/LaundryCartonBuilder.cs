using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Reuses Kenney Furniture Kit (CC0). All instances are authored assets, not runtime geometry.
public static class LaundryCartonBuilder
{
    const string Folder="Assets/Environment/LaundryOperations/";
    const string Source="Assets/ThirdParty/KenneyFurniture/cardboardBoxClosed.fbx";
    static Material Material(string name,Color color)
    {
        string path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(m);return m;
    }
    static void Clear(Transform t){for(int i=t.childCount-1;i>=0;i--)Object.DestroyImmediate(t.GetChild(i).gameObject);}
    static GameObject Instance(GameObject prefab,Transform parent,string name)
    {
        var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.name=name;g.transform.SetParent(parent,false);
        g.transform.localPosition=Vector3.zero;g.transform.localRotation=Quaternion.identity;g.transform.localScale=Vector3.one;return g;
    }
    static TMP_Text Text(Transform parent,string name,string value,Vector3 position)
    {
        var t=new GameObject(name,typeof(RectTransform),typeof(TextMeshPro)).GetComponent<TextMeshPro>();t.transform.SetParent(parent,false);
        t.transform.localPosition=position;t.font=Resources.Load<TMP_FontAsset>("LaundrySkin/Galmuri11 SDF");
        t.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/LaundryBuilding/Sign Lettering.mat");
        t.fontSize=.45f;t.fontStyle=FontStyles.Bold;t.text=value;t.color=new Color32(37,59,52,255);t.alignment=TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta=new Vector2(.205f,.047f);t.textWrappingMode=TextWrappingModes.NoWrap;return t;
    }
    [MenuItem("Laundry/Apply unified carton assets")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Author in Edit Mode");
        var root=new GameObject("Laundry Carton");
        var wrapper=new GameObject("Normalized asset").transform;wrapper.SetParent(root.transform,false);
        var source=Instance(AssetDatabase.LoadAssetAtPath<GameObject>(Source),wrapper,"Kenney cardboard box");
        var renderer=source.GetComponentInChildren<MeshRenderer>();Bounds bounds=renderer.bounds;
        wrapper.localScale=new Vector3(.64f/bounds.size.x,.36f/bounds.size.y,.44f/bounds.size.z);
        wrapper.localPosition=-Vector3.Scale(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),wrapper.localScale);
        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Material("Carton "+m.name,m.color)).ToArray();
        var label=GameObject.CreatePrimitive(PrimitiveType.Cube);label.name="Waybill";label.transform.SetParent(root.transform,false);
        label.transform.localPosition=new Vector3(.14f,.16f,-.221f);label.transform.localScale=new Vector3(.23f,.115f,.002f);
        Object.DestroyImmediate(label.GetComponent<Collider>());label.GetComponent<Renderer>().sharedMaterial=Material("Carton paper",new Color32(237,238,218,255));
        Text(root.transform,"Waybill title","세탁물",new Vector3(.14f,.177f,-.223f));
        string path=Folder+"Laundry Carton.prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
        var workflow=Object.FindAnyObjectByType<LaundryWorkflow>();var so=new SerializedObject(workflow);
        var cargo=((GameObject)so.FindProperty("cargoRoot").objectReferenceValue).transform;cargo.localPosition=new Vector3(0,-.62f,1.05f);
        foreach(string key in new[]{"carriedGarment","carriedParcel"})
        {
            var holder=(GameObject)so.FindProperty(key).objectReferenceValue;Clear(holder.transform);
            holder.transform.localPosition=Vector3.zero;holder.transform.localRotation=Quaternion.identity;holder.transform.localScale=Vector3.one;
            var box=Instance(prefab,holder.transform,"Laundry carton");
            if(key=="carriedParcel")box.GetComponentInChildren<TMP_Text>(true).text="세탁용품";
        }
        var oldTag=cargo.Find("Ticket tag");if(oldTag)Object.DestroyImmediate(oldTag.gameObject);
        var number=(TMP_Text)so.FindProperty("cargoText").objectReferenceValue;
        number.transform.localPosition=new Vector3(.14f,.132f,-.224f);number.transform.localRotation=Quaternion.identity;number.transform.localScale=Vector3.one;
        number.rectTransform.sizeDelta=new Vector2(.205f,.04f);number.fontSize=.4f;number.color=new Color32(37,59,52,255);
        var station=so.FindProperty("stations").GetArrayElementAtIndex(4);var contents=((GameObject)station.FindPropertyRelative("contents").objectReferenceValue).transform;
        foreach(Transform child in contents)child.gameObject.SetActive(false);
        contents.position=new Vector3(4.85f,1.25f,14.4f);contents.localRotation=Quaternion.Euler(0,-90,0);contents.localScale=Vector3.one;Instance(prefab,contents,"Laundry carton");
        var grab=Object.FindAnyObjectByType<Grab>();var gs=new SerializedObject(grab);var previous=(GameObject)gs.FindProperty("item").objectReferenceValue;previous.SetActive(false);
        var cam=grab.GetComponentInChildren<Camera>(true).transform;var legacy=Instance(prefab,cam,"Held collection carton");legacy.transform.localPosition=cargo.localPosition;legacy.SetActive(false);gs.FindProperty("item").objectReferenceValue=legacy;
        var grips=cam.Find("Carton grips");if(!grips){grips=new GameObject("Carton grips").transform;grips.SetParent(cam,false);}Clear(grips);
        foreach(var side in new[]{"R","L"}){var grip=new GameObject("Grip "+side).transform;grip.SetParent(grips,false);grip.localPosition=new Vector3(side=="R"?.4f:-.4f,-.61f,1.04f);gs.FindProperty("hand"+side+"Pos").objectReferenceValue=grip;}
        gs.ApplyModifiedPropertiesWithoutUndo();
        var npc=PrefabUtility.LoadPrefabContents("Assets/Prefabs/NPC.prefab");
        try
        {
            var ns=new SerializedObject(npc.GetComponentInChildren<NPCUI>(true));var bag=((GameObject)ns.FindProperty("laundryBundle").objectReferenceValue).transform;
            Clear(bag);bag.name="Customer laundry carton";
            // Preserve hand animation, with a small carton below the grasp and clear of the body.
            Vector3 hand=bag.parent.position;bag.rotation=npc.transform.rotation;bag.position=hand+npc.transform.right*.07f+Vector3.down*.28f;
            float inherited=bag.parent.lossyScale.x;bag.localScale=Vector3.one*(.75f/inherited);Instance(prefab,bag,"Laundry carton");
            PrefabUtility.SaveAsPrefabAsset(npc,"Assets/Prefabs/NPC.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(npc);}
        EditorSceneManager.MarkSceneDirty(workflow.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        return "Kenney carton prefab applied to both held cargo types, collection garments, NPC bundle and folding table";
    }
}
