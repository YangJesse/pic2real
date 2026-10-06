using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Authored geometry: photographs restore these same objects at a fixed viewpoint.
public static class PhotoRealmWorld
{
    static Material stone, dark, metal, brass, wood, white, glow, glass, water;
    static Transform root;
    public static readonly Vector3 StairCenter = new Vector3(0, 0, 34);
    public const float StairRadius = 3f;

    public static void Build(PhotoRealmGame game)
    {
        root = new GameObject("Photo Realm — architecture").transform;
        stone = Mat("Weathered limestone", new Color(.57f,.55f,.49f), .05f, .25f, true);
        dark = Mat("Basalt", new Color(.15f,.18f,.19f), .1f,.28f,true);
        metal = Mat("Painted steel", new Color(.095f,.12f,.135f), .7f,.42f);
        brass = Mat("Aged brass", new Color(.43f,.29f,.12f), .65f,.43f,true);
        wood = Mat("Oak", new Color(.31f,.20f,.105f), .02f,.25f,true);
        white = Mat("Photographic paper", new Color(.92f,.88f,.75f),0,.25f);
        glow = Mat("Warm porcelain", new Color(.9f,.73f,.41f),0,.35f);
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor",new Color(1.7f,1.1f,.45f));
        glass = Mat("Wind screen glass",new Color(.35f,.45f,.47f,.38f),.05f,.8f);
        glass.SetFloat("_Surface",1); glass.SetFloat("_ZWrite",0);
        glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.renderQueue=3000;
        water=Mat("Court water",new Color(.10f,.25f,.28f),.5f,.94f,true);
        if(Shader.Find("PhotoRealm/Water"))water=new Material(Shader.Find("PhotoRealm/Water"));
        Surface(stone,"Stone",new Color(.88f,.85f,.77f));Surface(dark,"Stone",new Color(.26f,.30f,.31f));
        Surface(wood,"Oak",new Color(.65f,.57f,.45f));Surface(metal,"Steel",new Color(.4f,.45f,.47f));
        SetupLight(game);

        Floor("Departure terrace",new Vector3(0,0,0),new Vector2(12,12));
        Rail(new Vector3(-5.8f,0,-5.6f),new Vector3(-5.8f,0,5.5f));
        Rail(new Vector3(5.8f,0,-5.6f),new Vector3(5.8f,0,5.5f));
        Rail(new Vector3(-5.8f,0,-5.6f),new Vector3(5.8f,0,-5.6f));
        for(int i=0;i<4;i++) Box("Terrace seam",new Vector3(-4.5f+i*3, .004f,0),new Vector3(.018f,.008f,12),dark,null,false);
        Box("Bridge socket",new Vector3(0,-.12f,5.7f),new Vector3(4,.3f,.6f),brass);

        Floor("Tower forecourt",new Vector3(0,0,20),new Vector2(14,8));
        Floor("Tower base",new Vector3(0,0,30),new Vector2(16,16));
        for(int sign=-1;sign<=1;sign+=2)
        {
            Box("Entrance pier",new Vector3(sign*4.75f,3,23),new Vector3(6.5f,6,.65f),stone);
            Box("Tower side wall",new Vector3(sign*7.8f,3,30),new Vector3(.5f,6,16),stone);
            Box("Wall plinth",new Vector3(sign*7.45f,.35f,30),new Vector3(.3f,.7f,16),dark);
            Floor("Upper side gallery",new Vector3(sign*6.1f,6,30),new Vector2(3.8f,16));
            for(int z=25;z<38;z+=4) Box("Wall joint",new Vector3(sign*7.53f,3,z),new Vector3(.025f,5.8f,.024f),dark,null,false);
        }
        Box("Entry lintel",new Vector3(0,5.25f,23),new Vector3(3,1.5f,.65f),stone);
        Box("Tower rear wall",new Vector3(0,3,37.8f),new Vector3(16,6,.5f),stone);
        // Enclosed interior with a small roof aperture instead of an open courtyard.
        Box("Upper north enclosure",new Vector3(0,8,37.8f),new Vector3(16,4,.5f),stone);
        Box("Upper west enclosure",new Vector3(-7.8f,8,30),new Vector3(.5f,4,16),stone);
        Box("Upper entrance enclosure",new Vector3(0,8,23),new Vector3(16,4,.5f),stone);
        Box("Upper east south pier",new Vector3(7.8f,8,24),new Vector3(.5f,4,4),stone);
        Box("Upper east north pier",new Vector3(7.8f,8,33.5f),new Vector3(.5f,4,9),stone);
        Box("Wind doorway lintel",new Vector3(7.8f,9.8f,27.5f),new Vector3(.5f,.4f,3),stone);
        Box("Roof west",new Vector3(-4.75f,10.15f,30),new Vector3(6.5f,.3f,16.5f),stone);
        Box("Roof east",new Vector3(4.75f,10.15f,30),new Vector3(6.5f,.3f,16.5f),stone);
        Box("Roof south",new Vector3(0,10.15f,27.25f),new Vector3(3,.3f,10.5f),stone);
        Box("Roof north",new Vector3(0,10.15f,36.85f),new Vector3(3,.3f,2.7f),stone);
        var guidance=LightAt("Dim light at photograph table",new Vector3(-4.8f,1.7f,26.2f),new Color(.54f,.63f,.7f),.45f,3.5f);guidance.shadows=LightShadows.None;
        Floor("Upper front gallery",new Vector3(0,6,26),new Vector2(16,7));
        Floor("Stair landing",new Vector3(0,6,29.65f),new Vector2(2.4f,2.3f));
        Box("Tower cap rear",new Vector3(0,6.18f,38),new Vector3(16,.5f,.5f),dark);
        Rail(new Vector3(-7.7f,6,22.4f),new Vector3(7.7f,6,22.4f));
        Rail(new Vector3(-7.7f,6,22.5f),new Vector3(-7.7f,6,37.5f));
        Box("Upper wind shelter",new Vector3(5.8f,7.2f,25),new Vector3(3.5f,2.4f,.35f),stone);
        game.closedWall=Box("Sealed entry panel",new Vector3(0,2.25f,23),new Vector3(3,4.5f,.64f),stone);

        Floor("Wind bridge",new Vector3(16,6,27.5f),new Vector2(16,2.8f));
        for(int z=0;z<2;z++) Box("Wind bridge kerb",new Vector3(16,6.055f,26.1f+z*2.8f),new Vector3(16,.11f,.13f),dark);
        Beam("Viaduct beam",new Vector3(8,5.45f,26.6f),new Vector3(24,5.45f,26.6f),.2f,metal);
        Beam("Viaduct beam",new Vector3(8,5.45f,28.4f),new Vector3(24,5.45f,28.4f),.2f,metal);
        Floor("Island entry",new Vector3(27,6,27.5f),new Vector2(6,8));
        Rail(new Vector3(24,6,23.6f),new Vector3(30,6,23.6f));

        // A contained sunken courtyard: water hides an existing walkable route.
        Floor("Court basin",new Vector3(32,2.2f,40),new Vector2(22,19));
        Box("Court west retaining wall",new Vector3(21.4f,4.1f,40),new Vector3(.8f,4,19),stone);
        Box("Court east retaining wall",new Vector3(42.6f,4.1f,40),new Vector3(.8f,4,19),stone);
        Box("Court entrance retaining wall",new Vector3(35.5f,4,31),new Vector3(14,4,.7f),stone);
        Floor("Submerged north walk",new Vector3(26,3.3f,38.5f),new Vector2(2.8f,5));
        Floor("Submerged cross walk",new Vector3(31,3.3f,41),new Vector2(12.8f,2.8f));
        Floor("Submerged east walk",new Vector3(36,3.3f,44),new Vector2(2.8f,6));
        StraightStairs(new Vector3(26,6,31.2f),new Vector3(26,3.3f,36),18);
        StraightStairs(new Vector3(36,3.3f,45),new Vector3(36,6,49.8f),18);
        Floor("Destination terrace",new Vector3(33,6,52.8f),new Vector2(20,6));
        for(int i=0;i<5;i++)
        {
            float z=33.5f+i*3.25f;
            Cylinder("Court pillar",new Vector3(22.5f,5.15f,z),.32f,5.9f,stone);
            Box("Court pillar cap",new Vector3(22.5f,8.13f,z),new Vector3(.9f,.22f,.9f),stone);
        }
        Box("Court colonnade lintel",new Vector3(22.5f,8.5f,40),new Vector3(1.05f,.65f,15),stone);
        game.waterSurface=Box("Water surface",new Vector3(32,5.72f,40),new Vector3(20.4f,.065f,17.7f),water,null,false).transform;
        game.waterMaterial=water;
        for(int i=0;i<4;i++)
        {
            Vector3 p=new Vector3(29+(i%2)*8,8,50+(i/2)*4);
            Cylinder("Pavilion column",p,.25f,4,stone);
            Box("Pavilion capital",p+Vector3.up*2,new Vector3(.8f,.25f,.8f),stone);
        }
        Box("Pavilion roof",new Vector3(33,10.3f,52),new Vector3(11,.5f,6.5f),stone);
        Box("Destination back wall",new Vector3(33,8,55),new Vector3(11,4,.4f),stone);
        Box("Warm destination aperture",new Vector3(33,8,54.75f),new Vector3(2.3f,2.7f,.06f),glow,null,false);
        LightAt("Pavilion warm light",new Vector3(33,9,52),new Color(1,.7f,.38f),5,12);
        game.goal=new Vector3(33,6,52);

        game.sites = new PhotoRealmGame.Site[6];
        game.sites[0]=Site("桥", "连接断开的桥台",new Vector3(0,0,3.4f),new Vector3(0,.6f,12),new Vector3(-2.2f,0,2.5f));
        var bridge=game.sites[0].objectRoot.transform;
        Box("Restored bridge deck",new Vector3(0,-.16f,11),new Vector3(3.6f,.32f,10),wood,bridge);
        for(int i=0;i<20;i++) Box("Deck board joint",new Vector3(0,.004f,6.25f+i*.5f),new Vector3(3.6f,.008f,.022f),dark,bridge,false);
        Rail(new Vector3(-1.72f,0,6),new Vector3(-1.72f,0,16),bridge);
        Rail(new Vector3(1.72f,0,6),new Vector3(1.72f,0,16),bridge);
        Beam("Bridge stringer",new Vector3(-1.3f,-.45f,6),new Vector3(-1.3f,-.45f,16),.24f,metal,bridge);
        Beam("Bridge stringer",new Vector3(1.3f,-.45f,6),new Vector3(1.3f,-.45f,16),.24f,metal,bridge);

        game.sites[1]=Site("门", "穿过封闭的建筑边界",new Vector3(0,0,19.4f),new Vector3(0,2,23),new Vector3(-2.3f,0,20));
        var door=game.sites[1].objectRoot.transform;
        Box("Door jamb left",new Vector3(-1.43f,2.2f,22.8f),new Vector3(.18f,4.4f,.35f),brass,door);
        Box("Door jamb right",new Vector3(1.43f,2.2f,22.8f),new Vector3(.18f,4.4f,.35f),brass,door);
        Box("Door header",new Vector3(0,4.4f,22.8f),new Vector3(3,.2f,.35f),brass,door);
        game.doorPivot=new GameObject("Door pivot").transform;game.doorPivot.SetParent(door);game.doorPivot.position=new Vector3(-1.32f,0,23);
        Box("Door leaf",new Vector3(0,2.1f,23),new Vector3(2.64f,4.2f,.16f),wood,game.doorPivot);
        Box("Door handle",new Vector3(.95f,1.4f,22.84f),new Vector3(.08f,.45f,.09f),brass,game.doorPivot,false);

        game.sites[2]=Site("射灯", "让光照出下一段路",new Vector3(-3,0,27),new Vector3(1.4f,2.7f,30.1f),new Vector3(-4.8f,0,26.2f));
        var lamp=game.sites[2].objectRoot.transform;
        Vector3 fixture=new Vector3(1.3f,3.8f,30),beamEnd=new Vector3(3,.18f,29),beamDirection=(beamEnd-fixture).normalized;
        Box("Lamp suspension",new Vector3(1.3f,4.9f,30),new Vector3(.1f,1.8f,.1f),metal,lamp);
        Box("Lamp yoke",fixture+Vector3.up*.38f,new Vector3(.86f,.1f,.14f),brass,lamp);
        var body=Cylinder("Directional spotlight housing",fixture,.35f,.72f,metal,lamp);body.transform.up=beamDirection;
        var rim=Cylinder("Brass lens rim",fixture+beamDirection*.36f,.36f,.085f,brass,lamp);rim.transform.up=beamDirection;
        var lensMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lensMaterial.color=new Color(7,4.8f,2.1f);
        var lens=Cylinder("Bright spotlight lens",fixture+beamDirection*.412f,.305f,.016f,lensMaterial,lamp);lens.transform.up=beamDirection;
        foreach(var renderer in new[]{body.GetComponent<Renderer>(),rim.GetComponent<Renderer>(),lens.GetComponent<Renderer>()})renderer.shadowCastingMode=ShadowCastingMode.Off;
        var spot=LightAt("Photo spotlight",fixture+beamDirection*.46f,new Color(1,.82f,.54f),65,14,lamp);
        spot.type=LightType.Spot;spot.spotAngle=60;spot.innerSpotAngle=32;spot.shadowBias=.03f;
        spot.transform.rotation=Quaternion.LookRotation(beamDirection);
        var bounce=LightAt("Lamp bounce",new Vector3(1.8f,2.2f,29.3f),new Color(1,.79f,.5f),5f,10,lamp);bounce.shadows=LightShadows.None;
        LightShaft(fixture+beamDirection*.44f,beamEnd,1.9f,lamp);
        game.lampDust=new Transform[28];game.lampDustRest=new Vector3[28];
        for(int i=0;i<28;i++)
        {
            float t=.18f+(i*.037f)% .72f;Vector3 p=Vector3.Lerp(fixture,beamEnd,t)+Polar(t*.72f,i*137.5f);
            var mote=Box("Illuminated dust",p,Vector3.one*(.008f+(i%3)*.003f),lensMaterial,lamp,false);
            mote.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;game.lampDust[i]=mote.transform;game.lampDustRest[i]=p;
        }
        var upperBounce=LightAt("Light carried up the stairwell",new Vector3(0,7.8f,33),new Color(.92f,.83f,.69f),3,10,lamp);upperBounce.shadows=LightShadows.None;

        game.sites[3]=Site("螺旋楼梯", "沿旋转的台阶走向天光",new Vector3(0,0,28.8f),new Vector3(0,3.4f,34),new Vector3(3,0,29));
        var stairs=game.sites[3].objectRoot.transform;
        Cylinder("Stair central mast",StairCenter+Vector3.up*3,.3f,6.5f,metal,stairs);
        for(int i=0;i<48;i++)
        {
            float a=-90+i*7.5f; float y=(i+1)*.125f;
            Wedge("Spiral tread "+i,StairCenter,1.65f,4.15f,a,a+7.65f,y,.14f,stone,stairs);
            if(i%3==0)Beam("Stair radial support",StairCenter+Polar(.3f,a+3.75f)+Vector3.up*(y-.2f),StairCenter+Polar(3.7f,a+3.75f)+Vector3.up*(y-.2f),.05f,metal,stairs);
            if(i%2==0)
            {
                Vector3 pos=StairCenter+Polar(4.06f,a+3.75f)+Vector3.up*y;
                Cylinder("Stair baluster",pos+Vector3.up*.5f,.025f,1,metal,stairs);
            }
            Vector3 p1=StairCenter+Polar(4.06f,a)+Vector3.up*(y+1);
            Vector3 p2=StairCenter+Polar(4.06f,a+7.5f)+Vector3.up*(y+1.125f);
            Beam("Spiral handrail",p1,p2,.045f,brass,stairs);
        }

        game.sites[4]=Site("挡风屏", "为连廊创造背风的通道",new Vector3(6,6,27.5f),new Vector3(15.5f,7.2f,28.9f),new Vector3(5.1f,6,26.1f));
        var screen=game.sites[4].objectRoot.transform;
        for(int i=0;i<8;i++)
        {
            float x=9+i*2;
            Box("Screen glass",new Vector3(x,7.3f,28.9f),new Vector3(1.88f,2.5f,.07f),glass,screen);
            Box("Screen mullion",new Vector3(x-1,7.3f,28.9f),new Vector3(.08f,2.6f,.12f),metal,screen);
            Box("Screen base",new Vector3(x,6.15f,28.9f),new Vector3(2,.3f,.35f),dark,screen);
        }
        Box("Screen top rail",new Vector3(16,8.6f,28.9f),new Vector3(16,.1f,.13f),metal,screen);
        game.sites[5]=Site("排水装置", "让水下的道路重新显露",new Vector3(27,6,29.1f),new Vector3(29.4f,6.8f,31),new Vector3(25.1f,6,29.1f));
        var pump=game.sites[5].objectRoot.transform;
        Box("Pump pedestal",new Vector3(29.5f,6.1f,30.5f),new Vector3(1.4f,.2f,1.8f),dark,pump);
        var pumpBody=Cylinder("Pump motor",new Vector3(29.5f,6.6f,30.5f),.38f,1,metal,pump);pumpBody.transform.rotation=Quaternion.Euler(90,0,0);
        for(int i=0;i<5;i++) {var fin=Cylinder("Motor cooling fin",new Vector3(29.5f,6.6f,30.2f+i*.12f),.42f,.04f,brass,pump);fin.transform.rotation=Quaternion.Euler(90,0,0);}
        Beam("Intake vertical pipe",new Vector3(29.5f,3,32),new Vector3(29.5f,6.6f,32),.18f,metal,pump);
        Beam("Intake elbow",new Vector3(29.5f,6.6f,32),new Vector3(29.5f,6.6f,30.8f),.18f,metal,pump);
        Beam("Drain outlet",new Vector3(29.5f,6.6f,30.1f),new Vector3(31.4f,6.6f,30.1f),.18f,metal,pump);
        Beam("Drain drop",new Vector3(31.4f,6.6f,30.1f),new Vector3(31.4f,4.8f,30.1f),.18f,metal,pump);
        game.drainStream=Beam("Visible draining water",new Vector3(31.4f,4.8f,30.1f),new Vector3(31.4f,1,30.1f),.13f,water,null,false);game.drainStream.SetActive(false);
        for(int i=0;i<6;i++) game.sites[i].objectRoot.SetActive(false);

        // Structural supports and restrained landscape detail.
        for(int i=0;i<4;i++) Box("Tower foundation buttress",new Vector3(-6+i*4,-3.5f,30),new Vector3(1.2f,6.2f,12),dark);
        for(int i=0;i<6;i++) Cylinder("Island support",new Vector3(23+i*3.4f,-.3f,42),.75f,5,stone);
        Planter(new Vector3(-4.4f,0,-2)); Planter(new Vector3(4.6f,0,18));
        Planter(new Vector3(27.5f,6,52)); Planter(new Vector3(40,6,53));
        game.windFlags=new Transform[5];
        for(int i=0;i<5;i++)
        {
            Vector3 p=new Vector3(9.5f+i*3,6,29.7f);
            Cylinder("Wind indicator mast",p+Vector3.up*1.7f,.035f,3.4f,metal);
            game.windFlags[i]=Box("Wind ribbon",p+new Vector3(0,3,-.8f),new Vector3(.3f,.025f,1.6f),wood,null,false).transform;
        }
        game.windStreaks=new Transform[34];
        Material mist=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mist.color=new Color(.6f,.7f,.72f,.24f);
        mist.SetFloat("_Surface",1);mist.SetFloat("_ZWrite",0);mist.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mist.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mist.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mist.renderQueue=3000;
        for(int i=0;i<game.windStreaks.Length;i++)
            game.windStreaks[i]=Box("Wind streak",new Vector3(8+(i*3.73f)%16,6.3f+(i*.71f)%3,24+(i*2.51f)%10),new Vector3(.012f,.018f,.45f),mist,null,false).transform;
        Details(game);
    }

    static void Surface(Material mat,string resource,Color tint)
    {
        var albedo=Resources.Load<Texture2D>("PhotoRealm/"+resource+"-albedo");
        var normal=Resources.Load<Texture2D>("PhotoRealm/"+resource+"-normal");
        if(albedo){mat.mainTexture=albedo;mat.mainTextureScale=Vector2.one*.65f;mat.color=tint;}
        if(normal){mat.SetTexture("_BumpMap",normal);mat.SetFloat("_BumpScale",.45f);mat.EnableKeyword("_NORMALMAP");}
    }
    static void LightShaft(Vector3 start,Vector3 end,float radius,Transform parent)
    {
        const int count=48;Vector3 axis=(end-start).normalized;
        Vector3 right=Vector3.Cross(axis,Vector3.right).normalized,up=Vector3.Cross(axis,right);
        var vertices=new Vector3[(count+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[count*6];
        for(int i=0;i<=count;i++)
        {
            float angle=i*Mathf.PI*2/count;Vector3 radial=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
            vertices[i*2]=start+radial*.24f;vertices[i*2+1]=end+radial*radius;
            uv[i*2]=new Vector2(i/(float)count,0);uv[i*2+1]=new Vector2(i/(float)count,1);
            if(i<count){int a=i*2,t=i*6;triangles[t]=a;triangles[t+1]=a+1;triangles[t+2]=a+2;triangles[t+3]=a+2;triangles[t+4]=a+1;triangles[t+5]=a+3;}
        }
        var mesh=new Mesh {vertices=vertices,triangles=triangles,uv=uv};mesh.RecalculateNormals();
        var g=new GameObject("Soft visible spotlight beam");g.transform.SetParent(parent);g.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=new Material(Shader.Find("PhotoRealm/LightShaft"));renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    static void Details(PhotoRealmGame game)
    {
        // Architectural joints, recessed panels, skirtings and restrained hardware.
        foreach(float y in new[]{.3f,2.0f,4.0f,5.7f})
        {
            for(int sign=-1;sign<=1;sign+=2)
                Box("Facade horizontal course",new Vector3(sign*4.8f,y,22.66f),new Vector3(6.15f,.025f,.025f),dark,null,false);
            Box("Interior rear course",new Vector3(0,y,37.535f),new Vector3(15.4f,.026f,.018f),dark,null,false);
        }
        for(int sign=-1;sign<=1;sign+=2)
        {
            Box("Entrance corner pilaster",new Vector3(sign*7.4f,3,22.55f),new Vector3(.55f,6,.4f),stone);
            Box("Entrance cornice",new Vector3(sign*4.6f,5.88f,22.35f),new Vector3(6.8f,.24f,.75f),stone);
            Box("Interior wall skirting",new Vector3(sign*7.42f,.18f,29),new Vector3(.2f,.36f,10),dark);
            Box("Facade recessed panel",new Vector3(sign*4.8f,3.2f,22.64f),new Vector3(1.7f,2.2f,.035f),dark,null,false);
            for(int i=0;i<9;i++) Box("Metal louvre",new Vector3(sign*4.8f,2.22f+i*.23f,22.59f),new Vector3(1.63f,.065f,.07f),metal,null,false);
        }
        foreach(var area in new[]{new Vector3(0,0,0),new Vector3(0,0,19.5f),new Vector3(0,0,27),new Vector3(33,6,52)})
        {
            float sx=area.z>49?18:10,sz=area.z==0?11:5;
            for(float x=-sx*.5f;x<=sx*.5f;x+=2)
                Box("Paving grout",area+new Vector3(x,.009f,0),new Vector3(.018f,.008f,sz),dark,null,false);
            for(float z=-sz*.5f;z<=sz*.5f;z+=1.5f)
                Box("Paving grout",area+new Vector3(0,.009f,z),new Vector3(sx,.008f,.018f),dark,null,false);
        }
        // Small warm fittings along the arrival steps and the destination pavilion.
        for(int i=0;i<4;i++)
        {
            var p=new Vector3(37.6f,3.6f+i*.65f,45.3f+i*1.2f);
            Box("Recessed stair lamp",p,new Vector3(.12f,.15f,.35f),glow,null,false);
            if(i%2==0)LightAt("Step guide light",p,new Color(1,.73f,.41f),.45f,2.7f);
        }
        for(int i=0;i<5;i++)
        {
            float z=33.5f+i*3.25f;
            Box("Column base moulding",new Vector3(22.5f,2.48f,z),new Vector3(.95f,.22f,.95f),stone);
            Box("Column collar",new Vector3(22.5f,2.75f,z),new Vector3(.74f,.2f,.74f),dark);
        }
        // Concrete anchor bolts on each screen support are visible at walking distance.
        for(int i=0;i<8;i++) for(int j=0;j<2;j++)
            Cylinder("Screen anchor bolt",new Vector3(8+i*2,6.34f,28.78f+j*.22f),.04f,.07f,brass,game.sites[4].objectRoot.transform);
        RockBase(new Vector3(0,-1,0),7,6,12);
        RockBase(new Vector3(0,-1,30),10,8,18);
        RockBase(new Vector3(32,.9f,41),13,10,24);
        RockBase(new Vector3(-55,-3,86),11,18,73);
        RockBase(new Vector3(78,-7,104),16,23,92);
        Box("Distant island ruin",new Vector3(-55,0,86),new Vector3(7,3,6),stone,null,false);
        for(int i=0;i<4;i++)Cylinder("Distant island column",new Vector3(74+i*3,-3,104),.7f,7,stone);
        var cloud=new Material(Shader.Find("Universal Render Pipeline/Unlit"));cloud.color=new Color(.37f,.44f,.47f);
        Box("Distant mist floor",new Vector3(0,-24,60),new Vector3(650,.1f,650),cloud,null,false);
        // Inset bronze marks identify authored viewpoints without a large UI marker.
        for(int i=0;i<game.sites.Length;i++)
        {
            var s=game.sites[i];
            for(int j=0;j<4;j++)
            {
                float x=j%2==0?-.42f:.42f,z=j/2==0?-.42f:.42f;
                Box("Viewpoint corner",s.anchor+new Vector3(x,.014f,z),new Vector3(.15f,.009f,.035f),brass,null,false);
            }
        }
    }

    static PhotoRealmGame.Site Site(string name,string hint,Vector3 anchor,Vector3 target,Vector3 table)
    {
        var s=new PhotoRealmGame.Site {name=name,hint=hint,anchor=anchor,lookTarget=target,pickup=table+Vector3.up*1.2f};
        s.objectRoot=new GameObject("PHOTO — "+name);s.objectRoot.transform.SetParent(root);
        Box("Photo table top",table+Vector3.up*.98f,new Vector3(1.15f,.12f,.8f),wood);
        for(int i=0;i<4;i++) Box("Table leg",table+new Vector3((i%2==0?-.46f:.46f),.46f,(i/2==0?-.29f:.29f)),new Vector3(.07f,.92f,.07f),metal);
        s.paper=Box("Pick up photograph — "+name,table+new Vector3(0,1.054f,0),new Vector3(.42f,.012f,.32f),white,null,false);
        Box("Photo print",table+new Vector3(0,1.063f,-.012f),new Vector3(.36f,.005f,.23f),dark,s.paper.transform,false);
        return s;
    }

    static void SetupLight(PhotoRealmGame game)
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.29f,.34f,.39f);
        RenderSettings.ambientEquatorColor=new Color(.18f,.19f,.20f);
        RenderSettings.ambientGroundColor=new Color(.085f,.08f,.072f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.48f,.56f,.61f);RenderSettings.fogDensity=.004f;
        var sun=new GameObject("Late afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;
        sun.color=new Color(1,.92f,.79f);sun.intensity=2.3f;sun.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.Euler(35,-35,0);RenderSettings.sun=sun;
        Shader skyShader=Shader.Find("Skybox/Procedural");
        if(skyShader) {var sky=new Material(skyShader);sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",1.15f);sky.SetColor("_SkyTint",new Color(.51f,.55f,.59f));sky.SetColor("_GroundColor",new Color(.29f,.32f,.34f));RenderSettings.skybox=sky;}
    }
    public static Vector3 Polar(float radius,float degrees) {float a=degrees*Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);}
    static Material Mat(string name,Color c,float metallic,float smooth,bool texture=false)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.color=c;m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
        if(texture)
        {
            var t=new Texture2D(128,128,TextureFormat.RGB24,true);t.name=name+" grain";t.wrapMode=TextureWrapMode.Repeat;
            var colors=new Color[128*128];
            for(int y=0;y<128;y++) for(int x=0;x<128;x++)
            {float n=.74f+Mathf.PerlinNoise(x*.13f,y*.13f)*.2f+Mathf.PerlinNoise(x*.73f+7,y*.73f)*.1f;colors[y*128+x]=new Color(n,n,n);}
            t.SetPixels(colors);t.Apply();m.mainTexture=t;m.mainTextureScale=new Vector2(3,3);
        }
        return m;
    }
    public static GameObject Box(string name,Vector3 pos,Vector3 size,Material mat,Transform parent=null,bool collision=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent?parent:root);g.transform.position=pos;g.transform.localScale=size;
        g.GetComponent<Renderer>().sharedMaterial=mat;
        var mesh=Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
        for(int i=0;i<uv.Length;i++)
        {
            Vector3 v=Vector3.Scale(vertices[i],size),n=normals[i];
            uv[i]=Mathf.Abs(n.y)>.5f?new Vector2(v.x,v.z):Mathf.Abs(n.x)>.5f?new Vector2(v.z,v.y):new Vector2(v.x,v.y);
        }
        mesh.uv=uv;g.GetComponent<MeshFilter>().sharedMesh=mesh;
        if(!collision) {g.GetComponent<Collider>().enabled=false;Object.Destroy(g.GetComponent<Collider>());} return g;
    }
    static GameObject Cylinder(string name,Vector3 pos,float radius,float height,Material mat,Transform parent=null)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent?parent:root);g.transform.position=pos;g.transform.localScale=new Vector3(radius*2,height*.5f,radius*2);g.GetComponent<Renderer>().sharedMaterial=mat;return g;
    }
    static GameObject Beam(string name,Vector3 a,Vector3 b,float radius,Material mat,Transform parent=null,bool collision=true)
    {
        var g=Cylinder(name,(a+b)*.5f,radius,Vector3.Distance(a,b),mat,parent);g.transform.up=(b-a).normalized;
        if(!collision) {g.GetComponent<Collider>().enabled=false;Object.Destroy(g.GetComponent<Collider>());}return g;
    }
    static void Floor(string name,Vector3 top,Vector2 size) {Box(name,top-Vector3.up*.3f,new Vector3(size.x,.6f,size.y),stone);}
    static void Rail(Vector3 a,Vector3 b,Transform parent=null)
    {
        Beam("Handrail",a+Vector3.up*1.1f,b+Vector3.up*1.1f,.04f,metal,parent);
        int count=Mathf.CeilToInt(Vector3.Distance(a,b)/1.7f);
        for(int i=0;i<=count;i++) {Vector3 p=Vector3.Lerp(a,b,i/(float)count);Cylinder("Railing upright",p+Vector3.up*.55f,.025f,1.1f,metal,parent);}
    }
    static void StraightStairs(Vector3 a,Vector3 b,int count)
    {
        float length=Vector3.Distance(new Vector3(a.x,0,a.z),new Vector3(b.x,0,b.z))/count;
        for(int i=0;i<count;i++) {var p=Vector3.Lerp(a,b,(i+.5f)/count);Box("Court step",p-Vector3.up*.14f,new Vector3(2.8f,.28f,length+.04f),stone);}
    }
    static Light LightAt(string name,Vector3 p,Color c,float intensity,float range,Transform parent=null)
    {var g=new GameObject(name);g.transform.SetParent(parent?parent:root);g.transform.position=p;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=c;l.intensity=intensity;l.range=range;l.shadows=LightShadows.Soft;return l;}
    static void Planter(Vector3 p)
    {
        Cylinder("Stone planter",p+Vector3.up*.35f,.65f,.7f,dark);
        Cylinder("Planter soil",p+Vector3.up*.71f,.59f,.03f,wood);
        var leaf=Mat("Olive leaves",new Color(.22f,.28f,.13f),0,.3f);leaf.SetFloat("_Cull",0);
        for(int i=0;i<13;i++)
        {
            Vector3 a=p+Vector3.up*.7f,b=p+Polar(.56f,i*137.5f)+Vector3.up*(1.25f+(i%3)*.2f);
            Vector3 side=Vector3.Cross((b-a).normalized,Vector3.up)*.12f,mid=Vector3.Lerp(a,b,.55f)+Vector3.up*.17f;
            var m=new Mesh();m.vertices=new[]{a,mid-side,b,mid+side};m.triangles=new[]{0,1,2,0,2,3,2,1,0,3,2,0};m.RecalculateNormals();
            var g=new GameObject("Planter leaf");g.transform.SetParent(root);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=leaf;
        }
    }
    static void RockBase(Vector3 center,float radius,float depth,int seed)
    {
        var random=new System.Random(seed);int count=18;var v=new Vector3[count*3+1];
        for(int ring=0;ring<3;ring++)for(int i=0;i<count;i++)
        {
            float r=radius*(1-ring*.22f)*(.86f+(float)random.NextDouble()*.22f);
            v[ring*count+i]=center+Polar(r,i*360f/count)+Vector3.down*(ring*depth*.4f+(float)random.NextDouble()*.6f);
        }
        v[count*3]=center+Vector3.down*depth;var tri=new List<int>();
        for(int ring=0;ring<2;ring++)for(int i=0;i<count;i++){int a=ring*count+i,b=ring*count+(i+1)%count,c=a+count,d=b+count;tri.AddRange(new[]{a,b,c,b,d,c});}
        for(int i=0;i<count;i++)tri.AddRange(new[]{2*count+i,2*count+(i+1)%count,3*count});
        var mesh=new Mesh();mesh.vertices=v;mesh.triangles=tri.ToArray();mesh.RecalculateNormals();
        var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++)uv[i]=new Vector2(v[i].x,v[i].y);mesh.uv=uv;
        var g=new GameObject("Weathered floating foundation");g.transform.SetParent(root);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=dark;
    }
    static void Wedge(string name,Vector3 c,float inner,float outer,float a,float b,float top,float thick,Material mat,Transform parent)
    {
        Vector3[] v={c+Polar(inner,a)+Vector3.up*top,c+Polar(outer,a)+Vector3.up*top,c+Polar(outer,b)+Vector3.up*top,c+Polar(inner,b)+Vector3.up*top,
            c+Polar(inner,a)+Vector3.up*(top-thick),c+Polar(outer,a)+Vector3.up*(top-thick),c+Polar(outer,b)+Vector3.up*(top-thick),c+Polar(inner,b)+Vector3.up*(top-thick)};
        // Explicit outward winding; top surface normal points up.
        int[] triangles={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
        var mesh=new Mesh {name=name};var flat=new Vector3[triangles.Length];var indices=new int[triangles.Length];var uv=new Vector2[triangles.Length];
        for(int i=0;i<triangles.Length;i++){flat[i]=v[triangles[i]];indices[i]=i;uv[i]=new Vector2(flat[i].x,flat[i].z);}
        mesh.vertices=flat;mesh.triangles=indices;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
        var g=new GameObject(name);g.transform.SetParent(parent);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;g.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
}
