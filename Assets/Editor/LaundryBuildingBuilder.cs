using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// A saved, modular street laundry. Existing stock, service positions and NPC routes remain authoritative.
public static class LaundryBuildingBuilder
{
    private const string Folder = "Assets/Environment/LaundryBuilding/";
    private static Transform root;
    private static Material plaster, mint, teal, ink, cream, metal, glass, timber, warm, tiles;
    private static TMP_FontAsset font;
    private static Material lettering;
    private static Color Hex(string value) { Color c; ColorUtility.TryParseHtmlString(value,out c); return c; }
    private static Material Mat(string name,string hex,bool emission=false)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,Folder+name+".mat");}
        m.color=Hex(hex);m.SetFloat("_Smoothness",.18f);m.enableInstancing=true;
        if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Hex(hex)*1.2f);}
        EditorUtility.SetDirty(m);return m;
    }
    private static Transform Group(string name) { var g=new GameObject(name);g.transform.SetParent(root,false);return g.transform; }
    private static GameObject Shape(string name,Transform parent,PrimitiveType kind,Vector3 position,Vector3 scale,Material material,bool solid=false)
    {
        var g=GameObject.CreatePrimitive(kind);g.name=name;g.transform.SetParent(parent,false);g.transform.position=position;g.transform.localScale=scale;
        g.GetComponent<Renderer>().sharedMaterial=material;
        if(!solid)ObjectDestroy(g.GetComponent<Collider>());
        GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic);
        return g;
    }
    private static void ObjectDestroy(UnityEngine.Object o) { UnityEngine.Object.DestroyImmediate(o); }
    private static GameObject Box(string name,Transform parent,float x,float y,float z,float sx,float sy,float sz,Material material,bool solid=false)
        => Shape(name,parent,PrimitiveType.Cube,new Vector3(x,y,z),new Vector3(sx,sy,sz),material,solid);
    private static void Text(string name,Transform parent,string value,Vector3 position,Vector2 size,float point,Color color,float yaw)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(MeshRenderer),typeof(TextMeshPro));go.transform.SetParent(parent,false);
        go.transform.position=position;go.transform.rotation=Quaternion.Euler(0,yaw,0);
        var t=go.GetComponent<TextMeshPro>();t.font=font;t.fontSharedMaterial=lettering;t.text=value;t.fontSize=point;t.fontStyle=FontStyles.Bold;
        t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=size;t.textWrappingMode=TextWrappingModes.NoWrap;
        t.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }
    private static Mesh RingMesh()
    {
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"Washer Ring.asset");if(mesh!=null)return mesh;
        var vertices=new List<Vector3>();var tris=new List<int>();
        const int sides=24, tube=6;
        for(int i=0;i<sides;i++)for(int j=0;j<tube;j++)
        {
            float a=i*Mathf.PI*2/sides,b=j*Mathf.PI*2/tube;
            vertices.Add(new Vector3(Mathf.Cos(a)*(.45f+.065f*Mathf.Cos(b)),Mathf.Sin(a)*(.45f+.065f*Mathf.Cos(b)),.065f*Mathf.Sin(b)));
        }
        for(int i=0;i<sides;i++)for(int j=0;j<tube;j++)
        {
            int a=i*tube+j,b=((i+1)%sides)*tube+j,c=((i+1)%sides)*tube+(j+1)%tube,d=i*tube+(j+1)%tube;
            tris.AddRange(new[]{a,b,c,a,c,d});
        }
        mesh=new Mesh{name="24 segment washer door ring"};mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"Washer Ring.asset");return mesh;
    }
    private static void Ring(string name,Transform parent,Vector3 p,Material mat)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.position=p;
        go.GetComponent<MeshFilter>().sharedMesh=RingMesh();go.GetComponent<Renderer>().sharedMaterial=mat;
    }
    private static void FloorTexture()
    {
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            bool grout=x%16==0||y%16==0;int tile=(x/16)+(y/16)*4;
            texture.SetPixel(x,y,grout?Hex("#96A9A2"):tile==5||tile==14?Hex("#C2DCD1"):((x/16+y/16)%2==0?Hex("#EFE9D8"):Hex("#E1E4D7")));
        }
        texture.Apply();File.WriteAllBytes(Folder+"Floor Tiles.png",texture.EncodeToPNG());ObjectDestroy(texture);AssetDatabase.ImportAsset(Folder+"Floor Tiles.png");
        var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"Floor Tiles.png");importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        tiles=Mat("Ceramic Floor","#FFFFFF");tiles.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Floor Tiles.png"));tiles.SetTextureScale("_BaseMap",new Vector2(4,8));
    }
    private static void Washer(Transform parent,float x,int number)
    {
        var g=new GameObject("Washer "+number+" - decorative").transform;g.SetParent(parent,false);
        Box("Cabinet",g,x,1.23f,30.4f,1.66f,1.9f,1.08f,cream,true);
        Box("Recessed front",g,x,1.17f,29.847f,1.47f,1.48f,.04f,metal);
        Box("Control strip",g,x,1.94f,29.81f,1.45f,.3f,.055f,teal);
        var drum=Shape("Door glass",g,PrimitiveType.Cylinder,new Vector3(x,1.16f,29.785f),new Vector3(.85f,.025f,.85f),glass);drum.transform.rotation=Quaternion.Euler(90,0,0);
        Ring("Chrome door rim",g,new Vector3(x,1.16f,29.75f),metal);
        Box("Handle",g,x+.44f,1.16f,29.66f,.12f,.32f,.12f,ink);
        Box("Display",g,x-.25f,1.94f,29.765f,.53f,.17f,.025f,ink);
        Text("Cycle display",g,"0:35",new Vector3(x-.25f,1.94f,29.744f),new Vector2(.48f,.15f),1.1f,Hex("#BFE4AE"),0);
        var knob=Shape("Program dial",g,PrimitiveType.Cylinder,new Vector3(x+.41f,1.94f,29.75f),new Vector3(.17f,.045f,.17f),cream);knob.transform.rotation=Quaternion.Euler(90,0,0);
        for(int i=0;i<4;i++)Box("Vent",g,x-.43f+i*.28f,.45f,29.83f,.19f,.035f,.035f,ink);
        Text("Machine number",g,"WASH / 0"+number,new Vector3(x,2.3f,31.04f),new Vector2(1.8f,.3f),2,Hex("#315F59"),0);
    }
    [MenuItem("Tools/Laundry/Build Laundry Building")]
    private static void BuildFromMenu(){Build();}
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Build only outside play mode");
        if(GameObject.Find("Laundry Building")!=null)return "Building already exists; edit its objects in place";
        if(GameObject.Find("Boxs")==null||GameObject.Find("Shop Computer")==null)throw new InvalidOperationException("Open the laundry gameplay scene first");
        Directory.CreateDirectory(Folder);
        plaster=Mat("Cream Plaster","#F0E5CC");mint=Mat("Mint Tile","#A5CCBC");teal=Mat("Petrol Trim","#326B65");ink=Mat("Roof Charcoal","#334448");
        cream=Mat("Warm Enamel","#F5EDD9");metal=Mat("Satin Metal","#A8B8B7");glass=Mat("Blue Glass","#36596B");timber=Mat("Counter Oak","#B48960");warm=Mat("Light Diffuser","#FFF0C9",true);
        FloorTexture();
        font=Resources.Load<TMP_FontAsset>("LaundrySkin/Galmuri11 SDF");string missing;
        font.TryAddCharacters("뽀송 세탁소 세탁 · 수선 · 의류 관리 접수 수령 의류 보관 직원 출입구 매장 관리 세탁실 LAUNDRY & REPAIR SINCE 1998 WASH / 0123456789:↗",out missing);
        lettering=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Sign Lettering.mat");if(lettering==null){lettering=new Material(font.material);AssetDatabase.CreateAsset(lettering,Folder+"Sign Lettering.mat");}
        lettering.SetFloat(ShaderUtilities.ID_OutlineWidth,0);lettering.SetFloat(ShaderUtilities.ID_FaceDilate,.08f);EditorUtility.SetDirty(lettering);EditorUtility.SetDirty(font);
        var go=new GameObject("Laundry Building");Undo.RegisterCreatedObjectUndo(go,"Build laundry architecture");root=go.transform;
        var shell=Group("01 Structure");var facade=Group("02 Street Front");var inside=Group("03 Interior");var props=Group("04 Laundry Equipment");var lights=Group("05 Lighting");
        // Existing platform top is y=.28. Only its uncovered edge strips receive additional floor colliders.
        Box("Rear floor extension",shell,9.25f,-.02f,30.85f,11.1f,.6f,2.03f,plaster,true);
        Box("East floor extension",shell,14.2f,-.02f,19.84f,1.01f,.6f,19.99f,plaster,true);
        Box("Entry floor extension",shell,9.25f,-.02f,9.55f,11.1f,.6f,.59f,plaster,true);
        var floor=Shape("Staff ceramic tiles",inside,PrimitiveType.Quad,new Vector3(9.25f,.287f,20.56f),new Vector3(10.7f,22.6f,1),tiles);floor.transform.rotation=Quaternion.Euler(90,0,0);
        Box("East wall",shell,14.65f,2.45f,20.6f,.3f,4.34f,22.6f,plaster,true);
        Box("Back wall",shell,9.25f,2.45f,31.85f,11.1f,4.34f,.3f,plaster,true);
        Box("Entry wall left",shell,4.22f,2.45f,9.4f,.64f,4.34f,.3f,plaster,true);
        Box("Entry wall right",shell,11.5f,2.45f,9.4f,6.3f,4.34f,.3f,plaster,true);
        Box("Entry lintel",shell,6.45f,3.97f,9.4f,3.8f,1.3f,.3f,plaster,true);
        Box("Ceiling and roof",shell,8.85f,4.65f,20.6f,12.2f,.3f,23.6f,ink,true);
        Box("Ceiling finish",inside,9.1f,4.48f,20.6f,11.7f,.04f,23.2f,cream);
        Box("East tile dado",inside,14.48f,1.03f,20.6f,.035f,1.5f,22.3f,mint);
        Box("Back tile dado",inside,9.25f,1.03f,31.68f,10.7f,1.5f,.035f,mint);
        Box("East chair rail",inside,14.44f,1.83f,20.6f,.08f,.1f,22.4f,teal);
        Box("East skirting",inside,14.44f,.39f,20.6f,.09f,.22f,22.4f,teal);
        Box("Back skirting",inside,9.25f,.39f,31.64f,10.7f,.22f,.09f,teal);
        for(int i=0;i<22;i++)Box("Tile vertical joint",inside,14.455f,1.03f,9.7f+i, .008f,1.4f,.012f,cream);
        foreach(float y in new[]{.8f,1.3f})Box("Tile horizontal joint",inside,14.455f,y,20.6f,.008f,.012f,22.3f,cream);
        foreach(float z in new[]{10.1f,14.3f,20.1f,24.45f,30.05f})
            Box("Service frame",facade,3.12f,2.2f,z,.28f,3.84f,.2f,teal); // All frames sit inside the existing counter's collision footprint.
        Box("Service transom",facade,3.12f,3.94f,20.1f,.35f,.5f,20.1f,teal);
        Box("Counter front cladding",facade,2.285f,.57f,19.84f,.035f,.57f,19.99f,mint);
        Box("Counter top",facade,3.11f,.93f,19.84f,1.7f,.08f,20.02f,timber);
        for(int i=0;i<20;i++)Box("Counter fluting",facade,2.26f,.57f,10.3f+i,.03f,.52f,.025f,teal);
        // Open arcade: no columns or walls are introduced in the existing customer approach routes.
        for(int i=0;i<28;i++)
        {
            float z=9.1f+(i+.5f)*.8f;var stripe=Box("Awning stripe "+i,facade,1f,4.12f,z,4.8f,.09f,.8f,i%2==0?teal:cream);
            stripe.transform.rotation=Quaternion.Euler(0,0,5.5f);
            Box("Awning valance "+i,facade,-1.39f,3.73f,z,.06f,.32f,.8f,i%2==0?teal:cream);
        }
        var fascia=Mat("Sign Enamel","#F5EDD9");fascia.shader=Shader.Find("Universal Render Pipeline/Unlit");EditorUtility.SetDirty(fascia);
        Box("Shop fascia",facade,2.98f,5.22f,20.1f,.28f,1.5f,21.1f,fascia);
        Box("Fascia top trim",facade,2.79f,6.02f,20.1f,.12f,.12f,21.4f,teal);
        Box("Fascia bottom trim",facade,2.79f,4.48f,20.1f,.12f,.12f,21.4f,teal);
        Text("Shop name",facade,"뽀송 세탁소",new Vector3(2.81f,5.4f,21.7f),new Vector2(13,1.2f),10.5f,Hex("#275E58"),90);
        Text("Shop subtitle",facade,"LAUNDRY & REPAIR",new Vector3(2.81f,4.82f,21.7f),new Vector2(13,.35f),2.5f,Hex("#275E58"),90);
        Text("Since",facade,"SINCE\n1998",new Vector3(2.81f,5.05f,12.4f),new Vector2(2.8f,.8f),2.9f,Hex("#275E58"),90);
        float[] serviceZ={26.45f,22.57f,17.72f};
        for(int i=0;i<serviceZ.Length;i++)
        {
            Text("Service window "+i,facade,"0"+(i+1)+"  접수 · 수령",new Vector3(2.91f,3.93f,serviceZ[i]),new Vector2(3.5f,.35f),2.8f,Hex("#FFF1CF"),90);
            Box("Queue mat "+i,facade,.46f,.292f,serviceZ[i],1.7f,.012f,1.5f,teal);
        }
        Text("Stock wall sign",inside,"의류 보관",new Vector3(14.43f,3.98f,23.2f),new Vector2(6,.6f),3.6f,Hex("#326B65"),90);
        Text("Staff entrance",facade,"직원 출입구",new Vector3(6.45f,3.75f,9.23f),new Vector2(3.4f,.5f),3,Hex("#326B65"),0);
        Text("Workroom sign",inside,"세탁실 / WASH ROOM",new Vector3(8,3.45f,31.66f),new Vector2(6.5f,.6f),3.7f,Hex("#326B65"),0);
        Text("Computer wall sign",inside,"매장 관리",new Vector3(11.4f,3.25f,9.57f),new Vector2(4,.5f),3.4f,Hex("#326B65"),180);
        foreach(float x in new[]{4.62f,8.27f})Box("Door jamb",facade,x,1.8f,9.4f,.12f,3.04f,.38f,teal);
        Box("Door top frame",facade,6.45f,3.32f,9.4f,3.77f,.12f,.38f,teal);
        // Frosted clerestory windows provide a clear architectural scale without blocking the stock labels.
        var frosted=Mat("Frosted Daylight Glass","#B2D1D3");
        foreach(float z in new[]{14.9f,20.5f,26.1f})
        {
            Box("Window outer frame",inside,14.44f,3.05f,z,.08f,1.02f,3.7f,teal);
            Box("Frosted window",inside,14.38f,3.05f,z,.05f,.84f,3.5f,frosted);
            Box("Window mullion",inside,14.34f,3.05f,z,.04f,.86f,.07f,cream);
            Box("Window sill",inside,14.23f,2.52f,z,.42f,.1f,3.9f,cream);
        }
        Washer(props,5.15f,1);Washer(props,7.5f,2);Washer(props,9.85f,3);
        foreach(float x in new[]{5.0f,5.4f,7.4f})
        {
            Box("Detergent bottle",props,x,2.3075f,30.8f,.115f,.245f,.1f,mint);
            Box("Bottle cap",props,x,2.4425f,30.8f,.06f,.04f,.065f,teal);
            Box("Bottle label",props,x,2.3125f,30.746f,.09f,.105f,.008f,cream);
        }
        foreach(float z in new[]{14,21,28})
        {
            Box("Ceiling light housing",lights,8.6f,4.31f,z,2.3f,.16f,.48f,metal);
            Box("Warm diffuser",lights,8.6f,4.21f,z,2.1f,.06f,.34f,warm);
            var lamp=new GameObject("Interior fill").AddComponent<Light>();lamp.transform.SetParent(lights,false);lamp.transform.position=new Vector3(8.6f,3.65f,z);
            lamp.type=LightType.Point;lamp.color=Hex("#FFF1DC");lamp.intensity=2.6f;lamp.range=8;lamp.shadows=LightShadows.None;
        }
        foreach(float z in new[]{11.1f,30.7f})
        {
            Box("Rain downpipe",facade,14.87f,2.5f,z,.13f,4.4f,.13f,teal);
            Box("Roof coping",shell,9.1f,4.92f,z,12f,.2f,.22f,teal);
        }
        var children=new List<Transform>();var positions=new List<Vector3>();
        foreach(Transform child in root){children.Add(child);positions.Add(child.position);}
        root.position=new Vector3(6.7f,.28f,20.6f);
        for(int i=0;i<children.Count;i++)children[i].position=positions[i];
        PrefabUtility.SaveAsPrefabAssetAndConnect(go,Folder+"Laundry Building.prefab",InteractionMode.AutomatedAction);
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(go.scene);EditorSceneManager.SaveScene(go.scene);AssetDatabase.SaveAssets();
        return "Laundry Building saved: open service arcade, staff room, three decorative washers, three lights. Existing gameplay objects unchanged.";
    }
}
