using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LaundryMachineRepair
{
    const string Folder = "Assets/Environment/LaundryOperations/";
    static Mesh Perforations()
    {
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"Drum perforations.asset"); if(mesh)return mesh;
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int ring=0;ring<2;ring++)for(int hole=0;hole<18;hole++)
        {
            float angle=(hole+(ring*.5f))*Mathf.PI*2/18, radius=ring==0?.27f:.34f;
            var center=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0);int index=vertices.Count;vertices.Add(center);
            for(int j=0;j<8;j++){float a=j*Mathf.PI*2/8;vertices.Add(center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.013f);}
            for(int j=0;j<8;j++){triangles.Add(index);triangles.Add(index+1+(j+1)%8);triangles.Add(index+1+j);}
        }
        mesh=new Mesh{name="Perforated drum face"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"Drum perforations.asset");return mesh;
    }
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Edit mode only");
        var workflow=Object.FindAnyObjectByType<LaundryWorkflow>();var so=new SerializedObject(workflow);var stations=so.FindProperty("stations");
        for(int i=0;i<4;i++)stations.GetArrayElementAtIndex(i).FindPropertyRelative("contents").objectReferenceValue=null;
        so.ApplyModifiedPropertiesWithoutUndo();
        string path="Assets/Environment/LaundryBuilding/Laundry Building.prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach(var t in prefab.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Laundry inside station").ToArray())Object.DestroyImmediate(t.gameObject);
            PrefabUtility.SaveAsPrefabAsset(prefab,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        var dryer=GameObject.Find("Laundry Operations/Dryer").transform;
        foreach(var t in dryer.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Laundry inside station").ToArray())Object.DestroyImmediate(t.gameObject);
        var metal=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Steel.mat");var dark=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Rubber.mat");
        so.Update();stations=so.FindProperty("stations");
        for(int i=0;i<4;i++)
        {
            var s=stations.GetArrayElementAtIndex(i);var rim=(Transform)s.FindPropertyRelative("drum").objectReferenceValue;
            var display=rim.parent.Find("Cycle display");
            if(!display && i==3)
            {
                var source=GameObject.Find("Laundry Building/04 Laundry Equipment/Washer 1/Cycle display");
                var label=Object.Instantiate(source,rim.parent);label.name="Cycle display";display=label.transform;
                display.localPosition=new Vector3(0,1.96f,.602f);display.localRotation=Quaternion.Euler(0,180,0);
            }
            s.FindPropertyRelative("display").objectReferenceValue=display.GetComponent<TMPro.TMP_Text>();
            if(rim.name=="Rotating inner drum")continue;
            rim.localRotation=Quaternion.identity;
            var drum=new GameObject("Rotating inner drum").transform;drum.SetParent(rim.parent,false);drum.position=rim.position+new Vector3(0,0,i<3?-.01f:.01f);
            if(i==3)drum.rotation=Quaternion.Euler(0,180,0);
            var disk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);disk.name="Steel drum face";disk.transform.SetParent(drum,false);disk.transform.localRotation=Quaternion.Euler(90,0,0);disk.transform.localScale=new Vector3(.77f,.008f,.77f);
            disk.GetComponent<Renderer>().sharedMaterial=metal;Object.DestroyImmediate(disk.GetComponent<Collider>());
            var holes=new GameObject("Perforations",typeof(MeshFilter),typeof(MeshRenderer));holes.transform.SetParent(drum,false);holes.transform.localPosition=new Vector3(0,0,-.009f);holes.GetComponent<MeshFilter>().sharedMesh=Perforations();holes.GetComponent<Renderer>().sharedMaterial=dark;
            for(int rib=0;rib<3;rib++)
            {
                float a=(rib*120+30)*Mathf.Deg2Rad;var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Drum lifter";g.transform.SetParent(drum,false);
                g.transform.localPosition=new Vector3(Mathf.Cos(a)*.16f,Mathf.Sin(a)*.16f,-.02f);g.transform.localRotation=Quaternion.Euler(0,0,rib*120+30);g.transform.localScale=new Vector3(.22f,.035f,.025f);g.GetComponent<Renderer>().sharedMaterial=dark;Object.DestroyImmediate(g.GetComponent<Collider>());
            }
            s.FindPropertyRelative("drum").objectReferenceValue=drum;
        }
        ((Transform)stations.GetArrayElementAtIndex(2).FindPropertyRelative("usePoint").objectReferenceValue).position=new Vector3(9.7f,.28f,28.4f);
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.ApplyPrefabInstance(GameObject.Find("Laundry Building"),InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(workflow.gameObject.scene);EditorSceneManager.SaveOpenScenes();
        return "Removed all washer/dryer clothing meshes; saved four perforated inner drums with stationary outer rims";
    }
    public static string RepairNpcMaterials()
    {
        var font=Resources.Load<TMPro.TMP_FontAsset>("LaundrySkin/Galmuri11 SDF");
        var materials=new Material[2];
        for(int i=0;i<2;i++)
        {
            string path=Folder+(i==0?"NPC Prompt Outline.mat":"NPC Chat Outline.mat");
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(font.material);AssetDatabase.CreateAsset(m,path);}
            m.SetColor(TMPro.ShaderUtilities.ID_OutlineColor,Color.black);m.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth,i==0?.3f:.45f);m.SetFloat(TMPro.ShaderUtilities.ID_FaceDilate,.1f);EditorUtility.SetDirty(m);materials[i]=m;
        }
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/NPC.prefab");int changed=0;
        try
        {
            foreach(var label in prefab.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if(label.fontSharedMaterial && AssetDatabase.Contains(label.fontSharedMaterial))continue;
                label.font=font;label.fontSharedMaterial=materials[label.name=="ChatText"?1:0];changed++;
            }
            PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/NPC.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        AssetDatabase.SaveAssets();return "Persisted NPC text material references: "+changed;
    }
}
