using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Self-contained playable prototype. All arena objects are generated at runtime.
public class DanielSimulator : MonoBehaviour
{
    enum Mode { Menu, Puzzle1, Puzzle2, Puzzle3, Free }
    class Enemy {
        public GameObject body, warning, markVisual;
        public DanielSkillVfx markSight;
        public Renderer[] renderers;
        public float maxHp = 160, markStoreRatio, markSkillDamage;
        public readonly List<GameObject> outline = new List<GameObject>(); public bool outlined;
        public Vector3 direction;
        public float hp = 160, timer, windup = -1, slow, slowFactor=1, markSlow, mark, storedDamage, recentDamage, visionDebuff, silence;
        public DanielSkillVfx ultimateLink;
        public bool dead, boss;
    }
    class Shot { public GameObject body; public Vector3 velocity; public float life = 4; }
    Mode mode = Mode.Menu;
    readonly List<Enemy> enemies = new List<Enemy>();
    readonly List<Shot> shots = new List<Shot>();
    GameObject arena, player, shadow;
    DanielCharacterAnimation characterAnimation;
    DanielSkillVfx ultimateVfx, rangeVfx, veilVfx;
    bool quietReveal;
    float castFacingTime;
    Transform leftLeg, rightLeg, leftArm, rightArm;
    readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    Camera cam;
    Vector3 camFocus; float camDistance=17, camShownDistance=24; bool camLocked=true;
    Vector3 destination;
    float afterimageTimer, ultimateWindup, health, stealth, shadowBuff, invulnerable, elapsed, attackTimer, ultimateTime, qDelay, qBuffTime, dashTime;
    int nextUltimateTick;
    int qBuffAttacks; Vector3 qPoint, qAim, dashFrom, dashTo, latchFrom; GameObject qEffect;
    [SerializeField, Range(1, 5)] int startingQLevel = 1;
    [SerializeField, Range(1, 5)] int startingWLevel = 1, startingELevel = 1;
    [SerializeField, Range(1, 3)] int startingRLevel = 1;
    [SerializeField, Min(0)] float startingAttackPower = 20f;
    int qLevel, wLevel, eLevel, rLevel, qCastLevel;
    float attackPower, qCastAttackPower, qAttackSpeedBonus;
    float shadowStrikeDamage, shadowCooldown, ultimateTickDamage, ultimateExitDamage;
    float BonusAttackPower => Mathf.Max(0, attackPower - startingAttackPower);
    // The stored-damage stat is a percentage: +0.08 percentage points per bonus attack power.
    float MarkStoreRatio => WStore[wLevel - 1] + .0008f * BonusAttackPower;
    float MarkSkillDamage => WDamage[wLevel - 1] + .6f * attackPower;
    float ShadowStrikeDamage => EDamage[eLevel - 1] + .5f * BonusAttackPower;
    float UltimateTickDamage => RTickDamage[rLevel - 1] + .3f * attackPower;
    float UltimateExitDamage => RExitDamage[rLevel - 1] + .9f * attackPower;
    Enemy attackTarget, ultimateTarget;
    float[] cooldown = new float[4];
    int hits, dodged, kills, stealthUses;
    bool ended, won;
    Font font;
    GUIStyle label, small, heading, button, centered;
    Texture2D white, vignette;
    float stealthTint;
    const float DashTravel=.28f, UltimateRange=3f;
    internal const float QCastRange = 7f, QRadius = 3.5f, QHalfAngle = 32.5f;
    internal const float QWindup = .53f, QCenterHalfWidth = .3f;
    const float BasicAttackInterval = .8f;
    static readonly float[] QDamage = { 30, 40, 50, 60, 70 };
    static readonly float[] QCenterDamage = { 40, 60, 80, 100, 120 };
    static readonly float[] QAttackSpeedBonus = { .8f, 1.1f, 1.4f, 1.7f, 2f };
    static readonly float[] QCooldown = { 10, 9, 8, 7, 6 };
    const float WRange = 7f, WChargeTime = 4f, WReadyTime = 2.5f, WCooldown = 7f;
    const float MarkSightRadius = 2f, NormalSightRadius = 13f;
    static readonly float[] WStore = { .04f, .06f, .08f, .10f, .12f };
    static readonly float[] WDamage = { 20, 40, 60, 80, 100 };
    const float ShadowRange = 3f, ShadowDistance = 3f, ShadowDuration = 3f;
    static readonly float[] EDamage = { 20, 40, 60, 80, 100 };
    static readonly float[] EStealth = { 1, 1.2f, 1.4f, 1.6f, 1.8f };
    static readonly float[] ECooldown = { 11, 10, 9, 8, 7 };
    static readonly float[] RTickDamage = { 10, 30, 50 };
    static readonly float[] RExitDamage = { 40, 105, 170 };
    static readonly float[] RCooldown = { 70, 60, 50 };
    static readonly float QFanSlope = Mathf.Tan(QHalfAngle * Mathf.Deg2Rad);
    float AttackSpeedMultiplier => qBuffAttacks > 0 ? 1f + qAttackSpeedBonus : 1f;
    const float UltimateWindupDuration = .5f, UltimateHoldDuration = 2f;
    // Attack transients in R_Full.wav, measured from the start of the cast.
    static readonly float[] UltimateTickTimes = { .885f, 1.225f, 1.585f, 1.930f };
    MaterialPropertyBlock markerColor;
    Material outlineMaterial;
    Color ink = new Color(.82f,.87f,.91f), muted = new Color(.55f,.63f,.68f), mint = new Color(.45f,.9f,.76f), red = new Color(1f,.38f,.38f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (FindFirstObjectByType<DanielSimulator>() == null) new GameObject("Daniel Simulator").AddComponent<DanielSimulator>(); }
    DanielSkillAudio skillAudio;

    void Start() {
        skillAudio = gameObject.AddComponent<DanielSkillAudio>();
        font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
        white = Texture2D.whiteTexture; markerColor=new MaterialPropertyBlock();
        // Soft-edged frame used to tint the screen while stealthed.
        vignette=new Texture2D(128,128,TextureFormat.RGBA32,false); vignette.wrapMode=TextureWrapMode.Clamp;
        for(int y=0;y<128;y++) for(int x=0;x<128;x++) {
            float dx=(x+.5f)/64f-1, dy=(y+.5f)/64f-1; float d=Mathf.Sqrt(dx*dx+dy*dy)/1.25f;
            vignette.SetPixel(x,y,new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.42f,1f,d))));
        }
        vignette.Apply();
        foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
        var cameraObject = new GameObject("Simulator Camera"); cam = cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
        cam.orthographic = false; cam.fieldOfView = 40; cam.nearClipPlane = .3f; cam.farClipPlane = 120; cam.backgroundColor = new Color(.035f,.055f,.067f); cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, 22, -15); cam.transform.rotation = Quaternion.Euler(55, 0, 0);
        var sun = new GameObject("Arena Light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(55,-35,0);
        RenderSettings.ambientLight = new Color(.65f,.69f,.72f);
        BuildArena(); ResetGame(Mode.Menu);
    }
    Material Mat(Color color) { Material m; if (materials.TryGetValue(color, out m)) return m; var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard"); m = new Material(shader); m.color = color; materials[color]=m; return m; }
    GameObject Shape(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, Transform parent) {
        GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent); go.transform.localPosition = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().material = Mat(color);
        var col = go.GetComponent<Collider>(); if (col != null) Destroy(col); return go;
    }
    // A faceted octahedron gives trees, hair and rocks deliberately flat faces.
    GameObject Facet(string name, Vector3 pos, Vector3 scale, Color color, Transform parent) {
        GameObject go=new GameObject(name); go.transform.SetParent(parent); go.transform.localPosition=pos; go.transform.localScale=scale;
        Vector3 top=new Vector3(0,.7f,0), bottom=new Vector3(0,-.6f,0);
        Vector3[] ring={new Vector3(-.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(.5f,0,-.5f)};
        Vector3[] vertices=new Vector3[24]; int[] triangles=new int[24];
        for(int i=0;i<4;i++) { int k=i*6; vertices[k]=top; vertices[k+1]=ring[i]; vertices[k+2]=ring[(i+1)%4]; vertices[k+3]=bottom; vertices[k+4]=ring[(i+1)%4]; vertices[k+5]=ring[i]; for(int j=0;j<6;j++) triangles[k+j]=k+j; }
        Mesh mesh=new Mesh(); mesh.vertices=vertices; mesh.triangles=triangles; mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().mesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=Mat(color); return go;
    }
    GameObject Part(string name, Vector3 pos, Vector3 scale, Color color, Transform parent, Vector3 rotation) {
        var part=Shape(PrimitiveType.Cube,name,pos,scale,color,parent); part.transform.localRotation=Quaternion.Euler(rotation); return part;
    }
    void BuildArena() {
        arena = new GameObject("Training arena"); var t=arena.transform;
        Color grass=new Color(.25f,.36f,.29f), darkGrass=new Color(.20f,.30f,.25f), stone=new Color(.42f,.45f,.41f), wall=new Color(.30f,.34f,.31f);
        Shape(PrimitiveType.Cube,"Island base",new Vector3(0,-.55f,0),new Vector3(29,1,23),new Color(.23f,.27f,.23f),t);
        Shape(PrimitiveType.Cube,"Grass",new Vector3(0,-.08f,0),new Vector3(28,.13f,22),grass,t);
        // One textured ground surface preserves clear silhouettes during combat.
        var ground=Shape(PrimitiveType.Plane,"Textured paving",new Vector3(0,.005f,0),new Vector3(2.4f,1,1.8f),Color.white,t);
        var groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Texture2D paving=Resources.Load<Texture2D>("arena_stone_v1");
        if(paving!=null) { paving.wrapMode=TextureWrapMode.Repeat; groundMaterial.mainTexture=paving; groundMaterial.mainTextureScale=new Vector2(3,2.25f); }
        ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
        // Street markings give the humanoid model a clear urban training backdrop.
        Color asphalt=new Color(.21f,.235f,.25f); Color roadLine=new Color(.68f,.58f,.32f);
        Shape(PrimitiveType.Cube,"Road",new Vector3(0,.035f,0),new Vector3(24,.055f,7.4f),asphalt,t);
        for(int i=-5;i<=5;i++) Shape(PrimitiveType.Cube,"Road center stripe",new Vector3(i*2.15f,.071f,0),new Vector3(1.15f,.015f,.07f),roadLine,t);
        for(int side=-1;side<=1;side+=2) Shape(PrimitiveType.Cube,"Road edge stripe",new Vector3(0,.071f,side*3.45f),new Vector3(24,.015f,.055f),new Color(.62f,.64f,.59f),t);
        // Inset bands and edge stones frame the play area like a small MOBA practice map.
        for(int i=-7;i<=7;i++) {
            Shape(PrimitiveType.Cube,"North curb",new Vector3(i*1.65f,.1f,9.4f),new Vector3(1.55f,.24f,.48f),wall,t);
            Shape(PrimitiveType.Cube,"South curb",new Vector3(i*1.65f,.1f,-9.4f),new Vector3(1.55f,.24f,.48f),wall,t);
        }
        for(int i=-5;i<=5;i++) {
            Shape(PrimitiveType.Cube,"West curb",new Vector3(-12.3f,.1f,i*1.65f),new Vector3(.48f,.24f,1.55f),wall,t);
            Shape(PrimitiveType.Cube,"East curb",new Vector3(12.3f,.1f,i*1.65f),new Vector3(.48f,.24f,1.55f),wall,t);
        }
        for(int i=0;i<8;i++) {
            float x=-11.5f+i*3.3f; float z=(i%2==0?10.1f:-10.1f);
            Shape(PrimitiveType.Cube,"Tree trunk",new Vector3(x,.65f,z),new Vector3(.3f,1.3f,.3f),new Color(.35f,.28f,.22f),t);
            Facet("Low poly tree",new Vector3(x,1.65f,z),new Vector3(1.45f,1.55f,1.35f),i%2==0?darkGrass:new Color(.29f,.42f,.30f),t);
        }
        for(int i=0;i<6;i++) {
            float z=-7.5f+i*3; float x=i%2==0?-13.1f:13.1f;
            Facet("Rock",new Vector3(x,.28f,z),new Vector3(.9f,.65f,.7f),stone,t);
        }
        for(int i=0;i<4;i++) {
            float x=i<2?-11.6f:11.6f, z=i%2==0?-8.5f:8.5f;
            Shape(PrimitiveType.Cube,"Stone pillar",new Vector3(x,.55f,z),new Vector3(.65f,1.1f,.65f),stone,t);
            Shape(PrimitiveType.Cube,"Pillar cap",new Vector3(x,1.15f,z),new Vector3(.85f,.16f,.85f),wall,t);
        }
    }
    void BuildFighter(Transform root, bool isPlayer) {
        Color coat=isPlayer?new Color(.12f,.14f,.19f):new Color(.48f,.23f,.20f);
        Color trim=isPlayer?new Color(.57f,.15f,.19f):new Color(.72f,.50f,.31f);
        Color pants=new Color(.11f,.13f,.15f), skin=new Color(.84f,.68f,.55f), hair=isPlayer?new Color(.16f,.14f,.16f):new Color(.35f,.24f,.18f);
        leftLeg=Part("Left leg",new Vector3(-.21f,.48f,0),new Vector3(.28f,.75f,.29f),pants,root,Vector3.zero).transform;
        rightLeg=Part("Right leg",new Vector3(.21f,.48f,0),new Vector3(.28f,.75f,.29f),pants,root,Vector3.zero).transform;
        Part("Left boot",new Vector3(-.21f,.12f,.10f),new Vector3(.32f,.23f,.48f),new Color(.08f,.09f,.10f),root,Vector3.zero);
        Part("Right boot",new Vector3(.21f,.12f,.10f),new Vector3(.32f,.23f,.48f),new Color(.08f,.09f,.10f),root,Vector3.zero);
        Part("Coat body",new Vector3(0,1.16f,0),new Vector3(.82f,.98f,.48f),coat,root,Vector3.zero);
        Part("Coat tail L",new Vector3(-.22f,.69f,-.15f),new Vector3(.42f,.68f,.28f),coat,root,new Vector3(-8,0,-8));
        Part("Coat tail R",new Vector3(.22f,.69f,-.15f),new Vector3(.42f,.68f,.28f),coat,root,new Vector3(-8,0,8));
        Part("Shirt",new Vector3(0,1.26f,.252f),new Vector3(.22f,.65f,.04f),new Color(.76f,.75f,.66f),root,Vector3.zero);
        Part("Collar",new Vector3(0,1.60f,.03f),new Vector3(.86f,.16f,.61f),trim,root,Vector3.zero);
        Part("Head",new Vector3(0,1.90f,0),new Vector3(.52f,.57f,.50f),skin,root,Vector3.zero);
        Facet("Angular hair",new Vector3(0,2.20f,-.04f),new Vector3(.75f,.60f,.72f),hair,root);
        Part("Fringe",new Vector3(-.10f,2.10f,.255f),new Vector3(.38f,.20f,.08f),hair,root,new Vector3(0,0,12));
        leftArm=Part("Left arm",new Vector3(-.56f,1.17f,0),new Vector3(.25f,.76f,.29f),coat,root,new Vector3(0,0,-12)).transform;
        rightArm=Part("Right arm",new Vector3(.56f,1.17f,0),new Vector3(.25f,.76f,.29f),coat,root,new Vector3(0,0,12)).transform;
        if(isPlayer) {
            Part("Dagger hilt",new Vector3(.62f,.78f,.25f),new Vector3(.12f,.12f,.42f),new Color(.24f,.20f,.18f),root,Vector3.zero);
            Part("Dagger blade",new Vector3(.62f,.78f,.60f),new Vector3(.13f,.09f,.52f),new Color(.75f,.82f,.80f),root,Vector3.zero);
        } else {
            Part("Target shoulder plate",new Vector3(0,1.56f,0),new Vector3(1.05f,.16f,.53f),trim,root,Vector3.zero);
        }
    }
    // Separate meshes and pivots make the character a true rotating 3D object.
    GameObject Rounded(string name, Vector3 position, Vector3 size, Color color, Transform parent) {
        return Shape(PrimitiveType.Sphere,name,position,size,color,parent);
    }
    Transform Limb(string name, Vector3 pivot, Vector3 size, Color color, Transform root) {
        var joint=new GameObject(name); joint.transform.SetParent(root); joint.transform.localPosition=pivot;
        Rounded(name+" mesh",new Vector3(0,-size.y*.5f,0),size,color,joint.transform); return joint.transform;
    }
    void BuildDaniel(Transform root) {
        Color black=new Color(.09f,.085f,.12f), dark=new Color(.055f,.055f,.075f), hair=new Color(.20f,.12f,.29f), hairLight=new Color(.28f,.18f,.37f), skin=new Color(.82f,.67f,.62f), metal=new Color(.65f,.67f,.70f);
        leftLeg=Limb("Left hip",new Vector3(-.18f,.96f,0),new Vector3(.31f,.86f,.32f),black,root);
        rightLeg=Limb("Right hip",new Vector3(.18f,.96f,0),new Vector3(.31f,.86f,.32f),black,root);
        Rounded("Left boot",new Vector3(0,-.75f,.07f),new Vector3(.30f,.28f,.46f),dark,leftLeg);
        Rounded("Right boot",new Vector3(0,-.75f,.07f),new Vector3(.30f,.28f,.46f),dark,rightLeg);
        Rounded("Jacket torso",new Vector3(0,1.35f,0),new Vector3(.82f,1.03f,.48f),black,root);
        Rounded("Waist",new Vector3(0,1.0f,0),new Vector3(.64f,.48f,.43f),black,root);
        Part("Long coat left",new Vector3(-.22f,.80f,-.20f),new Vector3(.32f,.72f,.12f),black,root,new Vector3(-8,0,-7));
        Part("Long coat right",new Vector3(.22f,.80f,-.20f),new Vector3(.32f,.72f,.12f),black,root,new Vector3(-8,0,7));
        leftArm=Limb("Left shoulder",new Vector3(-.47f,1.68f,0),new Vector3(.25f,.78f,.27f),black,root);
        rightArm=Limb("Right shoulder",new Vector3(.47f,1.68f,0),new Vector3(.25f,.78f,.27f),black,root);
        Rounded("Left glove",new Vector3(0,-.72f,0),new Vector3(.22f,.22f,.23f),dark,leftArm);
        Rounded("Right glove",new Vector3(0,-.72f,0),new Vector3(.22f,.22f,.23f),dark,rightArm);
        Part("Dagger handle",new Vector3(0,-.76f,.15f),new Vector3(.10f,.10f,.26f),metal,rightArm,Vector3.zero);
        Part("Dagger blade",new Vector3(0,-.76f,.40f),new Vector3(.10f,.06f,.38f),metal,rightArm,Vector3.zero);
        Rounded("Neck",new Vector3(0,1.91f,0),new Vector3(.30f,.25f,.30f),skin,root);
        Rounded("Head",new Vector3(0,2.12f,.02f),new Vector3(.48f,.57f,.43f),skin,root);
        Rounded("Hair crown",new Vector3(0,2.30f,-.05f),new Vector3(.54f,.39f,.52f),hair,root);
        Rounded("Long hair back",new Vector3(0,1.77f,-.21f),new Vector3(.52f,1.04f,.23f),hair,root);
        Rounded("Hair strand left",new Vector3(-.23f,1.99f,.08f),new Vector3(.16f,.71f,.19f),hairLight,root);
        Rounded("Hair strand right",new Vector3(.23f,1.99f,.08f),new Vector3(.16f,.71f,.19f),hair,root);
        Part("High collar",new Vector3(0,1.82f,.02f),new Vector3(.52f,.18f,.43f),dark,root,Vector3.zero);
        for(int i=0;i<3;i++) {
            float y=1.25f+i*.16f;
            Part("Jacket strap",new Vector3(0,y,.243f),new Vector3(.48f,.045f,.025f),dark,root,Vector3.zero);
            Part("Silver buckle",new Vector3(.09f,y,.265f),new Vector3(.12f,.075f,.03f),metal,root,Vector3.zero);
        }
    }
    void ResetGame(Mode next) {
        skillAudio.StopAll();
        if (next != Mode.Free || mode != Mode.Free)
        {
            qLevel = Mathf.Clamp(startingQLevel, 1, QDamage.Length);
            wLevel = Mathf.Clamp(startingWLevel, 1, WDamage.Length);
            eLevel = Mathf.Clamp(startingELevel, 1, EDamage.Length);
            rLevel = Mathf.Clamp(startingRLevel, 1, RTickDamage.Length);
            attackPower = Mathf.Max(0, startingAttackPower);
        }
        if(characterAnimation) characterAnimation.Footstep -= skillAudio.PlayFootstep;
        if(ultimateVfx) ultimateVfx.Stop(); ultimateVfx=null; characterAnimation=null; castFacingTime=0;
        foreach(var effect in FindObjectsByType<DanielSkillVfx>(FindObjectsSortMode.None)) Destroy(effect.gameObject);
        mode=next; ended=false; won=false; health=100; stealth=0; shadowBuff=0; invulnerable=0; elapsed=0; hits=0; dodged=0; kills=0; stealthUses=0; attackTimer=0; ultimateTime=0; ultimateWindup=0; stealthTint=0; qDelay=0; qBuffTime=0; qBuffAttacks=0; dashTime=0; nextUltimateTick=0; if(qEffect) Destroy(qEffect); attackTarget=null; ultimateTarget=null;
        for(int i=0;i<4;i++) cooldown[i]=0;
        foreach(var e in enemies) { if(e.body) Destroy(e.body); if(e.warning) Destroy(e.warning); } enemies.Clear();
        foreach(var s in shots) if(s.body) Destroy(s.body); shots.Clear();
        if(player) Destroy(player); if(shadow) Destroy(shadow);
        destination = next==Mode.Menu ? new Vector3(4,0,0) : new Vector3(-5,0,-3); if(next!=Mode.Menu) camFocus=destination;
        player = new GameObject("Daniel 3D"); player.transform.position = destination;
        GameObject modelAsset=Resources.Load<GameObject>("Characters/Y Bot");
        if(modelAsset!=null) {
            GameObject model=Instantiate(modelAsset,player.transform); model.name="Y Bot (Daniel)";
            model.transform.localPosition=Vector3.zero; model.transform.localRotation=Quaternion.identity; model.transform.localScale=Vector3.one;
            var renderers=model.GetComponentsInChildren<Renderer>();
            foreach(var renderer in renderers) {
                Material[] source=renderer.sharedMaterials; Material[] converted=new Material[source.Length];
                for(int i=0;i<source.Length;i++) {
                    if(source[i]==null) { converted[i]=Mat(Color.gray); continue; }
                    var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if(source[i].HasProperty("_Color")) material.color=source[i].color;
                    if(source[i].HasProperty("_MainTex")) material.mainTexture=source[i].mainTexture;
                    converted[i]=material;
                }
                renderer.materials=converted;
            }
            if(renderers.Length>0) {
                Bounds bounds=renderers[0].bounds; for(int i=1;i<renderers.Length;i++) bounds.Encapsulate(renderers[i].bounds);
                if(bounds.size.y>.001f) {
                    float scale=2.25f/bounds.size.y; model.transform.localScale=Vector3.one*scale;
                    Vector3 relativeCenter=bounds.center-player.transform.position;
                    model.transform.localPosition=new Vector3(-relativeCenter.x*scale,-(bounds.min.y-player.transform.position.y)*scale,-relativeCenter.z*scale);
                }
            }
            characterAnimation=model.AddComponent<DanielCharacterAnimation>();
            characterAnimation.Footstep += skillAudio.PlayFootstep;
            leftLeg=rightLeg=leftArm=rightArm=null;
        } else { Debug.LogError("Y Bot FBX could not be loaded from Resources/Characters/Y Bot."); BuildDaniel(player.transform); }
        shadow=Shape(PrimitiveType.Cylinder,"Player marker",destination+Vector3.up*.035f,new Vector3(.78f,.025f,.78f),new Color(.29f,.9f,.68f),arena.transform);
        if(next!=Mode.Menu) { AddEnemy(new Vector3(4,0,2),0); if(next==Mode.Puzzle3 || next==Mode.Free) AddEnemy(new Vector3(7,0,-3),1.1f); }
    }
    void AddEnemy(Vector3 position, float offset, bool boss = false) {
        Enemy e = new Enemy { boss = boss, timer = 1.4f + offset };
        e.body = new GameObject(boss ? "Training boss" : "Training target");
        e.body.transform.position = position;
        if (boss) { e.hp = e.maxHp = 1000; }
        Transform savedLL=leftLeg, savedRL=rightLeg, savedLA=leftArm, savedRA=rightArm;
        BuildFighter(e.body.transform, false); leftLeg=savedLL; rightLeg=savedRL; leftArm=savedLA; rightArm=savedRA;
        BuildOutline(e);
        e.renderers = e.body.GetComponentsInChildren<Renderer>(true);
        e.warning=Shape(PrimitiveType.Cube,"Attack telegraph",Vector3.zero,new Vector3(.65f,.035f,1),new Color(.9f,.29f,.23f),arena.transform); e.warning.SetActive(false); enemies.Add(e);
    }
    bool CanSeeEnemy(Enemy target)
    {
        Vector3 position = target.body.transform.position;
        if ((position - player.transform.position).sqrMagnitude <= NormalSightRadius * NormalSightRadius)
            return true;
        foreach (var marked in enemies)
            if (!marked.dead && marked.mark > 0 &&
                (position - marked.body.transform.position).sqrMagnitude <= MarkSightRadius * MarkSightRadius)
                return true;
        return false;
    }
    // Hover outline: every body part gets a slightly larger shell drawn with front faces culled.
    void BuildOutline(Enemy e) {
        if(outlineMaterial==null) {
            var shader=Shader.Find("Universal Render Pipeline/Unlit"); if(shader==null) shader=Shader.Find("Unlit/Color");
            outlineMaterial=new Material(shader); outlineMaterial.SetColor("_BaseColor",new Color(1f,.86f,.35f)); outlineMaterial.color=new Color(1f,.86f,.35f);
            outlineMaterial.SetFloat("_Cull",(float)UnityEngine.Rendering.CullMode.Front);
        }
        const float thickness=.11f;
        foreach(var filter in e.body.GetComponentsInChildren<MeshFilter>()) {
            if(filter.sharedMesh==null) continue;
            var shell=new GameObject("Hover outline"); shell.transform.SetParent(filter.transform,false);
            Vector3 size=Vector3.Scale(filter.sharedMesh.bounds.size,filter.transform.lossyScale);
            shell.transform.localScale=new Vector3(1+thickness/Mathf.Max(.05f,Mathf.Abs(size.x)),1+thickness/Mathf.Max(.05f,Mathf.Abs(size.y)),1+thickness/Mathf.Max(.05f,Mathf.Abs(size.z)));
            shell.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            var renderer=shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial=outlineMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            shell.SetActive(false); e.outline.Add(shell);
        }
    }
    void UpdateHoverOutline() {
        Enemy hovered=EnemyAtCursor();
        foreach(var e in enemies) {
            bool on=e==hovered && !e.dead; if(on==e.outlined) continue; e.outlined=on;
            foreach(var shell in e.outline) if(shell) shell.SetActive(on);
        }
    }
    Vector3 MouseWorld() { if(Mouse.current==null) return player.transform.position+Vector3.forward; Ray ray=cam.ScreenPointToRay(Mouse.current.position.ReadValue()); Plane p=new Plane(Vector3.up,Vector3.zero); float distance; return p.Raycast(ray,out distance)?ray.GetPoint(distance):player.transform.position; }
    void Update() {
        UpdateCamera(Time.deltaTime);
        if(mode==Mode.Menu || ended) { if(characterAnimation) characterAnimation.SetMoving(0); return; }
        UpdateHoverOutline();
        float dt=Time.deltaTime; elapsed+=dt;
        foreach (var e in enemies) if (!e.dead) AdvanceMark(e, dt);
        stealthTint=Mathf.MoveTowards(stealthTint,stealth>0?1:0,dt*(stealth>0?6:4)); castFacingTime=Mathf.Max(0,castFacingTime-dt);
        stealth=Mathf.Max(0,stealth-dt); if(shadowBuff>0) { shadowBuff-=dt; if(shadowBuff<=0) { shadowBuff=0; cooldown[2]=shadowCooldown; } }
        invulnerable=Mathf.Max(0,invulnerable-dt); qBuffTime=Mathf.Max(0,qBuffTime-dt); if(qBuffTime<=0) qBuffAttacks=0;
        if(qDelay>0) { qDelay-=dt; if(qDelay<=0) ResolveQ(); }
        if(dashTime>0) { afterimageTimer-=dt; if(afterimageTimer<=0 && characterAnimation) { DanielSkillVfx.Afterimage(characterAnimation.transform,.3f); afterimageTimer=.045f; } dashTime=Mathf.Max(0,dashTime-dt); { float dashK=1-dashTime/DashTravel; player.transform.position=Vector3.Lerp(dashFrom,dashTo,1-(1-dashK)*(1-dashK)); } destination=player.transform.position; }
        for(int i=0;i<4;i++) cooldown[i]=Mathf.Max(0,cooldown[i]-dt);
        if(Mouse.current!=null && Mouse.current.rightButton.wasPressedThisFrame) {
            Enemy clicked=EnemyAtCursor();
            if(clicked!=null) { attackTarget=clicked; DanielSkillVfx.ClickMarker(clicked.body.transform.position,true); Show("표적을 추격하고 기본 공격합니다."); }
            else { attackTarget=null; destination=MouseWorld(); destination.x=Mathf.Clamp(destination.x,-11.8f,11.8f); destination.z=Mathf.Clamp(destination.z,-8.8f,8.8f); destination.y=0; DanielSkillVfx.ClickMarker(destination,false); }
        } else if(Mouse.current!=null && Mouse.current.rightButton.isPressed && attackTarget==null) {
            // Holding the button keeps steering toward the cursor.
            destination=MouseWorld(); destination.x=Mathf.Clamp(destination.x,-11.8f,11.8f); destination.z=Mathf.Clamp(destination.z,-8.8f,8.8f); destination.y=0;
        }
        attackTimer = Mathf.Max(0, attackTimer - dt * AttackSpeedMultiplier);
        if(ultimateWindup>0 && ultimateTarget!=null) {
            // Pre-delay: rooted where the skill was cast and untargetable, then pours into the target's shadow as smoke.
            ultimateWindup-=dt;
            Vector3 targetPos=ultimateTarget.body.transform.position; Vector3 toward=targetPos-latchFrom; toward.y=0;
            player.transform.position=latchFrom; if(toward.sqrMagnitude>.01f) player.transform.rotation=Quaternion.LookRotation(toward);
            destination=latchFrom; castFacingTime=.1f;
            if(ultimateWindup<=0) {
                float overshoot = -ultimateWindup;
                ultimateWindup = 0;
                ultimateTime = UltimateHoldDuration;
                nextUltimateTick = 0;
                if (!ultimateTarget.dead)
                {
                    ultimateTarget.silence = .5f;
                    ultimateTarget.windup = -1;
                    ultimateTarget.warning.SetActive(false);
                }
                skillAudio.Play(DanielSkillAudio.Cue.R_Enter);
                if(characterAnimation) characterAnimation.SetHidden(true);
                DanielSkillVfx.SmokeBurst(latchFrom,toward); DanielSkillVfx.DashTrail(latchFrom,targetPos); DanielSkillVfx.SmokeStream(latchFrom,ultimateTarget.body.transform);
                player.transform.position=targetPos; destination=targetPos;
                ultimateVfx=DanielSkillVfx.BeginUltimate(ultimateTarget.body.transform);
                AdvanceUltimate(overshoot);
            }
        } else if(ultimateTime>0) {
            if(ultimateVfx && ultimateTarget!=null) ultimateVfx.SetExitDirection(MouseWorld()-ultimateTarget.body.transform.position);
            AdvanceUltimate(dt);
        }
        if(attackTarget!=null && !attackTarget.dead && CanSeeEnemy(attackTarget) && ultimateTime<=0 && ultimateWindup<=0 && dashTime<=0) {
            Vector3 d=attackTarget.body.transform.position-player.transform.position; d.y=0; float reach=shadowBuff>0?ShadowRange:1.45f;
            if(d.magnitude>reach) destination=attackTarget.body.transform.position-d.normalized*(reach-.1f);
            else { destination=player.transform.position; if(attackTimer<=0) BasicAttack(attackTarget); }
        } else if(attackTarget!=null && (attackTarget.dead || !CanSeeEnemy(attackTarget))) { attackTarget=null; destination=player.transform.position; }
        if(Keyboard.current!=null) {
            Vector3 move=Vector3.zero;
            if(Keyboard.current.upArrowKey.isPressed) move+=Vector3.forward; if(Keyboard.current.downArrowKey.isPressed) move+=Vector3.back;
            if(Keyboard.current.leftArrowKey.isPressed) move+=Vector3.left; if(Keyboard.current.rightArrowKey.isPressed) move+=Vector3.right;
            if(move!=Vector3.zero) { attackTarget=null; destination=player.transform.position+move.normalized*2; }
            if(Keyboard.current.sKey.wasPressedThisFrame) { attackTarget=null; destination=player.transform.position; }
            if(Keyboard.current.aKey.wasPressedThisFrame) {
                Enemy nearest=EnemyAtCursor(); Vector3 cursor=MouseWorld(); float best=6f;
                if(nearest==null) foreach(var e in enemies) { if(e.dead || !CanSeeEnemy(e)) continue; float d=Vector3.Distance(e.body.transform.position,cursor); if(d<best) { best=d; nearest=e; } }
                if(nearest!=null) { attackTarget=nearest; DanielSkillVfx.ClickMarker(nearest.body.transform.position,true); } else Show("A: 커서 근처에 공격할 적이 없습니다.");
            }
            if(Keyboard.current.qKey.wasPressedThisFrame) Cast(0); if(Keyboard.current.wKey.wasPressedThisFrame) Cast(1); if(Keyboard.current.eKey.wasPressedThisFrame) Cast(2); if(Keyboard.current.rKey.wasPressedThisFrame) Cast(3);
            if(Keyboard.current.escapeKey.wasPressedThisFrame) { ResetGame(Mode.Menu); return; }
        }
        // Stealth presentation: smoke veil while hidden, a reveal ring when it ends on its own or is broken by a skill.
        if(stealth>0 && dashTime<=0 && !veilVfx) { veilVfx=DanielSkillVfx.CreateVeil(player.transform); quietReveal=false; }
        else if(stealth<=0 && veilVfx) { veilVfx.Stop(); veilVfx=null; if(!quietReveal && ultimateWindup<=0 && ultimateTime<=0) DanielSkillVfx.Reveal(player.transform.position); }
        // After the dash lands, show how far the empowered attack reaches until it is used or expires.
        bool showRange=shadowBuff>0 && dashTime<=0 && ultimateTime<=0 && ultimateWindup<=0;
        if(showRange && !rangeVfx) rangeVfx=DanielSkillVfx.CreateRange(player.transform,ShadowRange);
        else if(!showRange && rangeVfx) { rangeVfx.Stop(); rangeVfx=null; }
        if(rangeVfx) { bool inside=false; foreach(var e in enemies) if(!e.dead && CanSeeEnemy(e) && Vector3.Distance(e.body.transform.position,player.transform.position)<=ShadowRange) inside=true; rangeVfx.SetRangeHighlight(inside); }
        // R window: chevrons point at every target damaged in the last 4 s; dark out of range, bright inside it.
        foreach(var e in enemies) {
            bool link=!e.dead && !e.boss && CanSeeEnemy(e) && e.recentDamage>0 && cooldown[3]<=0 && ultimateTime<=0 && ultimateWindup<=0;
            if(link && !e.ultimateLink) e.ultimateLink=DanielSkillVfx.CreateUltimateLink(player.transform,e.body.transform);
            else if(!link && e.ultimateLink) { e.ultimateLink.Stop(); e.ultimateLink=null; }
            if(e.ultimateLink) e.ultimateLink.SetRangeHighlight(Vector3.Distance(e.body.transform.position,player.transform.position)<=UltimateRange);
        }
        Vector3 delta=(ultimateTime>0 || ultimateWindup>0 || dashTime>0)?Vector3.zero:destination-player.transform.position; delta.y=0;
        if(delta.magnitude>.08f) { player.transform.position+=delta.normalized*Mathf.Min(delta.magnitude, 4.4f*dt); if(castFacingTime<=0) player.transform.rotation=Quaternion.RotateTowards(player.transform.rotation,Quaternion.LookRotation(delta),1080*dt); }
        if(characterAnimation) { characterAnimation.SetMoving(delta.magnitude>.08f && dashTime<=0 && ultimateTime<=0?4.4f:0); characterAnimation.SetStealth(stealth>0 || ultimateWindup>0); }
        float swing=delta.magnitude>.1f?Mathf.Sin(Time.time*11)*25:0;
        if(leftLeg) leftLeg.localRotation=Quaternion.Euler(swing,0,0); if(rightLeg) rightLeg.localRotation=Quaternion.Euler(-swing,0,0);
        if(leftArm) leftArm.localRotation=Quaternion.Euler(-swing*.7f,0,-10); if(rightArm) rightArm.localRotation=Quaternion.Euler(swing*.7f,0,10);
        var markerRenderer=shadow.GetComponent<Renderer>(); markerRenderer.GetPropertyBlock(markerColor); markerColor.SetColor("_BaseColor",stealth>0?new Color(.67f,.32f,.88f):new Color(.29f,.9f,.68f)); markerRenderer.SetPropertyBlock(markerColor);
        Vector3 pp=player.transform.position; pp.x=Mathf.Clamp(pp.x,-11.8f,11.8f); pp.z=Mathf.Clamp(pp.z,-8.8f,8.8f); player.transform.position=pp; shadow.transform.position=pp+Vector3.up*.035f;
        foreach(var e in enemies) UpdateEnemy(e,dt);
        foreach (var e in enemies)
        {
            if (e.dead) continue;
            bool visible = CanSeeEnemy(e);
            foreach (var renderer in e.renderers) renderer.forceRenderingOff = !visible;
            if (e.warning) e.warning.GetComponent<Renderer>().forceRenderingOff = !visible;
        }
        for(int i=shots.Count-1;i>=0;i--) {
            Shot s=shots[i]; s.body.transform.position+=s.velocity*dt; s.life-=dt;
            if(Vector3.Distance(s.body.transform.position,player.transform.position+Vector3.up*.55f)<.75f && invulnerable<=0 && ultimateTime<=0 && ultimateWindup<=0) { health-=25; hits++; invulnerable=.6f; s.life=0; Show("피격! 진입하기 전에 투사체를 피하세요."); }
            if(s.life<=0 || Mathf.Abs(s.body.transform.position.x)>13 || Mathf.Abs(s.body.transform.position.z)>10) { if(s.life>0) dodged++; Destroy(s.body); shots.RemoveAt(i); }
        }
        if(health<=0) Finish(false);
        else if(ultimateTime<=0 && ultimateWindup<=0 && mode==Mode.Puzzle1 && kills>=1) Finish(hits<=2);
        else if(ultimateTime<=0 && ultimateWindup<=0 && mode==Mode.Puzzle2 && kills>=1) Finish(dodged>=2);
        else if(ultimateTime<=0 && ultimateWindup<=0 && mode==Mode.Puzzle3 && kills>=2) Finish(stealthUses>=1 && hits<=3);
        else if(mode==Mode.Free && kills>=enemies.Count && shots.Count==0) { AddEnemy(new Vector3(Random.Range(3f,9f),0,Random.Range(-6f,6f)),0); }
    }
    // Quarter-view chase camera: locked to the player by default, Y frees it for edge panning.
    void UpdateCamera(float dt) {
        bool playing=mode!=Mode.Menu && !ended;
        if(playing && Keyboard.current!=null && Keyboard.current.yKey.wasPressedThisFrame) { camLocked=!camLocked; Show(camLocked?"시점 고정":"시점 자유: 화면 가장자리로 이동, Space로 복귀"); }
        if(playing && Mouse.current!=null) { float wheel=Mouse.current.scroll.ReadValue().y; if(Mathf.Abs(wheel)>.01f) camDistance=Mathf.Clamp(camDistance-Mathf.Sign(wheel)*1.5f,11,22); }
        Vector3 target=camFocus; float distance=camDistance; bool follow=camLocked || !playing || (Keyboard.current!=null && Keyboard.current.spaceKey.isPressed);
        if(mode==Mode.Menu) { target=new Vector3(1,0,0); distance=24; }
        else if(follow) { if(player) target=player.transform.position; }
        else if(Application.isFocused && Mouse.current!=null) {
            Vector2 p=Mouse.current.position.ReadValue(); Vector3 pan=Vector3.zero; const float edge=12;
            if(p.x<=edge) pan.x-=1; if(p.x>=Screen.width-edge) pan.x+=1; if(p.y<=edge) pan.z-=1; if(p.y>=Screen.height-edge) pan.z+=1;
            if(pan!=Vector3.zero) target=camFocus+pan.normalized*16*dt;
        }
        target.x=Mathf.Clamp(target.x,-12,12); target.z=Mathf.Clamp(target.z,-9,9); target.y=0;
        camFocus=follow?Vector3.Lerp(camFocus,target,1-Mathf.Exp(-12*dt)):target;
        camShownDistance=Mathf.Lerp(camShownDistance,distance,1-Mathf.Exp(-10*dt));
        cam.transform.rotation=Quaternion.Euler(55,0,0);
        cam.transform.position=camFocus+Vector3.forward*.6f-cam.transform.forward*camShownDistance;
    }
    void AdvanceMark(Enemy e, float dt)
    {
        e.visionDebuff = Mathf.Max(0, e.visionDebuff - dt);
        if (e.mark <= 0) return;
        bool charging = e.mark > WReadyTime;
        e.mark -= dt;
        if (e.mark <= 0)
        {
            ClearMark(e);
            return;
        }
        bool ready = e.mark <= WReadyTime;
        if (charging && ready) skillAudio.Play(DanielSkillAudio.Cue.W_Ready);
        if (e.markVisual && e.markVisual.TryGetComponent<DanielSkillVfx>(out var effect))
            effect.SetMarkProgress(Mathf.Clamp01((WChargeTime + WReadyTime - e.mark) / WChargeTime), ready);
    }

    void ClearMark(Enemy e)
    {
        e.mark = e.storedDamage = e.visionDebuff = 0;
        if (e.markVisual) Destroy(e.markVisual);
        if (e.markSight) e.markSight.Stop();
        e.markVisual = null;
        e.markSight = null;
        skillAudio.CancelCharge();
        cooldown[1] = WCooldown;
    }

    void UpdateEnemy(Enemy e,float dt) {
        if(e.dead) return;
        Vector3 face = player.transform.position - e.body.transform.position;
        face.y = 0;
        bool seesPlayer = e.visionDebuff <= 0 || face.sqrMagnitude <= MarkSightRadius * MarkSightRadius;
        if (seesPlayer && face.sqrMagnitude > .01f) e.body.transform.rotation = Quaternion.LookRotation(face);
        e.silence = Mathf.Max(0, e.silence - dt);
        e.slow = Mathf.Max(0, e.slow - dt);
        e.markSlow = Mathf.Max(0, e.markSlow - dt);
        if (e.slow <= 0) e.slowFactor = 1;
        e.timer -= dt;
        e.recentDamage = Mathf.Max(0, e.recentDamage - dt);
        if(e.windup<0 && seesPlayer && stealth<=0 && ultimateTime<=0 && ultimateWindup<=0) {
            Vector3 away=e.body.transform.position-player.transform.position; away.y=0;
            if(away.magnitude<4.5f && away.sqrMagnitude>.01f) {
                float movementFactor = e.markSlow > 0 ? .5f : e.slowFactor;
                Vector3 next=e.body.transform.position+away.normalized*1.3f*movementFactor*dt;
                next.x=Mathf.Clamp(next.x,-11.5f,11.5f); next.z=Mathf.Clamp(next.z,-8.5f,8.5f); e.body.transform.position=next;
            }
        }
        if(e.silence>0) return;
        if(e.windup>=0) {
            e.windup-=dt;
            if(e.windup<=0) { e.warning.SetActive(false); var s=new Shot(); s.body=Shape(PrimitiveType.Sphere,"Projectile",e.body.transform.position+Vector3.up*.55f,new Vector3(.55f,.55f,.55f),new Color(1,.35f,.20f),arena.transform); s.velocity=e.direction*8; shots.Add(s); e.windup=-1; e.timer=e.slow>0?2.8f:1.8f; }
        } else if(e.timer<=0 && ultimateWindup<=0 && seesPlayer) {
            Vector3 aim=player.transform.position-e.body.transform.position; aim.y=0; e.direction=aim.sqrMagnitude>.01f?aim.normalized:Vector3.forward; e.windup=.85f;
            e.warning.transform.position=e.body.transform.position+e.direction*6+Vector3.up*.025f; e.warning.transform.rotation=Quaternion.LookRotation(e.direction); e.warning.transform.localScale=new Vector3(.75f,.025f,12); e.warning.SetActive(true);
        }
    }
    Enemy EnemyAtCursor() {
        if(Mouse.current==null) return null; Vector2 point=Mouse.current.position.ReadValue(); Enemy best=null; float distance=55f*Screen.height/720f;
        foreach(var e in enemies) { if(e.dead || !CanSeeEnemy(e)) continue; Vector3 screen=cam.WorldToScreenPoint(e.body.transform.position+Vector3.up); float d=Vector2.Distance(point,new Vector2(screen.x,screen.y)); if(screen.z>0 && d<distance) { distance=d; best=e; } }
        return best;
    }
    void Hurt(Enemy e, float amount, float extraSkillDamage = 0f)
    {
        if (e == null || e.dead) return;
        e.visionDebuff = 0;
        float damage = amount + extraSkillDamage;
        if (e.mark > WReadyTime)
            e.storedDamage += damage * e.markStoreRatio;
        else if (e.mark > 0)
        {
            // The arena has no defense stat. Both the stored true damage and skill damage go directly to HP.
            float trueDamage = e.storedDamage;
            float skillDamage = e.markSkillDamage;
            ClearMark(e);
            damage += trueDamage + skillDamage;
            DanielSkillVfx.MarkBurst(e.body.transform.position);
            skillAudio.Play(DanielSkillAudio.Cue.W_Burst);
            e.markSlow = 1f;
        }
        if (damage > 0) e.recentDamage = 4;
        e.hp -= damage;
        if (e.hp <= 0)
        {
            if (e.mark > 0) ClearMark(e);
            e.dead = true;
            e.warning.SetActive(false);
            e.body.SetActive(false);
            kills++;
        }
    }
    void BasicAttack(Enemy e) {
        Vector3 d=e.body.transform.position-player.transform.position; d.y=0;
        if(d.sqrMagnitude>.01f) player.transform.rotation=Quaternion.LookRotation(d);
        if(shadowBuff>0) {
            DanielSkillVfx.SmokeBurst(player.transform.position,player.transform.forward);
            Vector3 side=d.sqrMagnitude>.01f?d.normalized:Vector3.forward;
            player.transform.position=e.body.transform.position+side*1.1f; destination=player.transform.position;
            player.transform.rotation=Quaternion.LookRotation(-side);
            if(characterAnimation) characterAnimation.PlayAttack(true, AttackSpeedMultiplier);
            DanielSkillVfx.SmokeBurst(player.transform.position,-side); DanielSkillVfx.AttackSlash(e.body.transform.position,-side,true); castFacingTime=.3f;
            skillAudio.Play(DanielSkillAudio.Cue.E_Strike);
            Hurt(e, attackPower, extraSkillDamage: shadowStrikeDamage);
            stealth = 0; quietReveal = true; shadowBuff = 0; cooldown[2] = shadowCooldown;
        } else {
            if(characterAnimation) characterAnimation.PlayAttack(false, AttackSpeedMultiplier);
            DanielSkillVfx.AttackSlash(e.body.transform.position,player.transform.forward);
            skillAudio.PlayBasicAttack();
            castFacingTime=.25f;
            Hurt(e,attackPower);
        }
        attackTimer = BasicAttackInterval;
        if (qBuffAttacks > 0) qBuffAttacks--;
    }
    void ResolveQ() {
        skillAudio.Play(DanielSkillAudio.Cue.Q_Snap);
        qEffect = null;
        int rank = Mathf.Clamp(qCastLevel, 1, QDamage.Length) - 1;
        Vector3 apex = qPoint - qAim * (QRadius * .5f);
        Vector3 right = Vector3.Cross(Vector3.up, qAim);
        bool hit = false;
        foreach (var e in enemies)
        {
            if (e.dead) continue;
            Vector3 offset = e.body.transform.position - apex;
            offset.y = 0;
            float forward = Vector3.Dot(offset, qAim);
            float lateral = Mathf.Abs(Vector3.Dot(offset, right));
            if (forward < 0 || offset.sqrMagnitude > QRadius * QRadius || lateral > forward * QFanSlope) continue;
            bool center = lateral <= QCenterHalfWidth;
            Hurt(e, center ? QCenterDamage[rank] + 1.4f * qCastAttackPower
                           : QDamage[rank] + 1.1f * qCastAttackPower);
            if (center) { e.slow = 1.5f; e.slowFactor = .65f; }
            hit = true;
        }
        if(hit) { qBuffAttacks=3; qBuffTime=3; qAttackSpeedBonus=QAttackSpeedBonus[rank]; Show("그림자 가위 적중: 다음 기본 공격 3회의 속도가 증가합니다."); }
        else Show("그림자 가위가 빗나갔습니다.");
    }
    void AdvanceUltimate(float deltaTime)
    {
        ultimateTime -= deltaTime;
        if (ultimateTarget != null)
        {
            player.transform.position = ultimateTarget.body.transform.position;
            float castElapsed = UltimateWindupDuration + UltimateHoldDuration - ultimateTime;
            // Apply every due tick even if one frame crosses more than one timestamp.
            while (nextUltimateTick < UltimateTickTimes.Length && castElapsed >= UltimateTickTimes[nextUltimateTick])
            {
                nextUltimateTick++;
                if (ultimateTarget.dead) continue;
                skillAudio.Play(DanielSkillAudio.Cue.R_Tick);
                Hurt(ultimateTarget, ultimateTickDamage);
            }
        }
        if (ultimateTime <= 0 || ultimateTarget == null) ExitUltimate();
    }

    void ExitUltimate() {
        if(ultimateTarget!=null) {
            Vector3 exit=MouseWorld()-ultimateTarget.body.transform.position; exit.y=0; if(exit.sqrMagnitude<.01f) exit=Vector3.forward; exit.Normalize();
            DanielSkillVfx.ExitSlash(ultimateTarget.body.transform.position,exit);
            skillAudio.Play(DanielSkillAudio.Cue.R_Exit);
            player.transform.rotation=Quaternion.LookRotation(exit); castFacingTime=.35f;
            player.transform.position=ultimateTarget.body.transform.position+exit*1.5f; destination=player.transform.position;
            Hurt(ultimateTarget, ultimateExitDamage);
        }
        if(ultimateVfx) ultimateVfx.Stop(); ultimateVfx=null;
        if(characterAnimation) { characterAnimation.SetHidden(false); characterAnimation.PlayUltimateExit(); }
        ultimateTarget=null; ultimateTime=0; Show("걸작 종료!");
    }
    void Cast(int index) {
        if(ultimateWindup>0) return;
        if(ultimateTime>0) { if(index==3) ExitUltimate(); return; }
        if(index==1) { foreach(var e in enemies) if(e.mark>0) { Show("영감 표식이 사라진 뒤 다시 사용할 수 있습니다."); return; } }
        if(index==2 && (shadowBuff>0 || dashTime>0)) { Show("그림자 이동이 아직 지속 중입니다."); return; }
        if(cooldown[index]>0) { Show("아직 스킬을 사용할 수 없습니다."); return; }
        Vector3 mouse=MouseWorld(); Vector3 aim=mouse-player.transform.position; aim.y=0; if(aim.sqrMagnitude<.01f) aim=player.transform.forward; aim.Normalize(); player.transform.rotation=Quaternion.LookRotation(aim);
        if(index==0) {
            stealth = 0;
            qCastLevel = qLevel;
            qCastAttackPower = attackPower;
            cooldown[0] = QCooldown[qCastLevel - 1];
            qPoint = player.transform.position + Vector3.ClampMagnitude(mouse - player.transform.position, QCastRange);
            qPoint.y = 0;
            qAim = aim;
            qDelay = QWindup;
            castFacingTime=.32f;
            skillAudio.Play(DanielSkillAudio.Cue.Q_Cast);
            qEffect=DanielSkillVfx.SpawnQ(qPoint,qAim,QWindup).gameObject; Show("그림자 가위: 잠시 후 지정 위치를 공격합니다."); return;
        }
        if(index==1) {
            Enemy target=EnemyAtCursor();
            if(target==null || Vector3.Distance(target.body.transform.position,player.transform.position)>WRange) { Show("W: 사거리 안의 적에게 커서를 올리세요."); return; }
            ApplyMark(target);
            return;
        }
        if(index==2) {
            attackTarget=null; dashFrom=player.transform.position; dashTo=dashFrom+aim*ShadowDistance; dashTo.x=Mathf.Clamp(dashTo.x,-11.8f,11.8f); dashTo.z=Mathf.Clamp(dashTo.z,-8.8f,8.8f);
            if(characterAnimation) characterAnimation.PlayDash(); castFacingTime=DashTravel+.1f;
            DanielSkillVfx.SmokeBurst(dashFrom,aim); DanielSkillVfx.DashTrail(dashFrom,dashTo); DanielSkillVfx.SonicBoom(dashFrom,dashTo,DashTravel);
            skillAudio.Play(DanielSkillAudio.Cue.E_Dash);
            shadowStrikeDamage = ShadowStrikeDamage;
            shadowCooldown = ECooldown[eLevel - 1];
            dashTime=DashTravel; afterimageTimer=0; shadowBuff=ShadowDuration; stealth=EStealth[eLevel - 1]; stealthUses++; Show("그림자 이동: 다음 기본 공격으로 적의 반대편에 이동합니다."); return;
        }
        Enemy targetR=EnemyAtCursor();
        if(targetR==null || targetR.boss || targetR.recentDamage<=0 || Vector3.Distance(targetR.body.transform.position,player.transform.position)>UltimateRange) { Show("R: 최근 4초 안에 피해를 준 가까운 적에게 사용하세요."); return; }
        ultimateTickDamage = UltimateTickDamage;
        ultimateExitDamage = UltimateExitDamage;
        cooldown[3]=RCooldown[rLevel - 1]; attackTarget=null; stealth=0; ultimateTarget=targetR; ultimateWindup=UltimateWindupDuration; latchFrom=player.transform.position;
        if(characterAnimation) characterAnimation.PlayUltimateLatch();
        DanielSkillVfx.Latch(player.transform,UltimateWindupDuration);
        skillAudio.Play(DanielSkillAudio.Cue.R_Latch);
        Show("걸작: 0.5초 뒤 표적의 그림자 속으로 들어갑니다. 최대 2초 유지, R 재사용으로 커서 방향에 나옵니다.");
    }
    void ApplyMark(Enemy target)
    {
        stealth = 0;
        target.mark = WChargeTime + WReadyTime;
        target.storedDamage = 0;
        target.visionDebuff = 2;
        target.markStoreRatio = MarkStoreRatio;
        target.markSkillDamage = MarkSkillDamage;
        castFacingTime = .25f;
        skillAudio.Play(DanielSkillAudio.Cue.W_Charge);
        target.markVisual = DanielSkillVfx.CreateMark(target.body.transform, 2.65f).gameObject;
        target.markSight = DanielSkillVfx.CreateRange(target.body.transform, MarkSightRadius);
    }
    void Show(string message) { } // on-screen notices are disabled
    void Finish(bool success) { skillAudio.CancelCharge(); if(characterAnimation) { characterAnimation.SetMoving(0); characterAnimation.SetStealth(false); characterAnimation.SetHidden(false); } ended=true; won=success; foreach(var e in enemies) if(e.warning) e.warning.SetActive(false); }
    void InitStyles() {
        if(label!=null) return;
        label=new GUIStyle(GUI.skin.label){font=font,fontSize=16,normal={textColor=ink},wordWrap=true};
        small=new GUIStyle(label){fontSize=13,normal={textColor=muted}};
        heading=new GUIStyle(label){fontSize=29,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
        centered=new GUIStyle(label){alignment=TextAnchor.MiddleCenter};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=15,fontStyle=FontStyle.Bold,wordWrap=true};
    }
    void Box(Rect r,Color c) { GUI.color=c; GUI.DrawTexture(r,white); GUI.color=Color.white; }
    bool Button(Rect r,string text) { Box(r,new Color(.12f,.25f,.25f,.97f)); return GUI.Button(r,text,button); }
    void OnGUI() {
        InitStyles(); float sx=Screen.width/1280f, sy=Screen.height/720f; GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(sx,sy,1));
        Box(new Rect(0,0,1280,64),new Color(.025f,.04f,.05f,.96f)); GUI.Label(new Rect(28,12,500,35),"DANIEL  /  TRAINING GROUND",heading); GUI.Label(new Rect(930,23,320,25),"팬 메이드 전투 연습 프로토타입",small);
        if(mode==Mode.Menu) {
            Box(new Rect(32,94,440,545),new Color(.025f,.045f,.055f,.94f));
            GUI.Label(new Rect(58,116,380,42),"다니엘 시뮬레이터",heading); GUI.Label(new Rect(60,164,360,54),"진입할 틈을 찾고, 스킬을 피하고,\n가까이 붙어 마무리하세요.",label);
            if(Button(new Rect(60,240,380,65),"01   진입의 순간     /     2회 이하 피격으로 처치")) ResetGame(Mode.Puzzle1);
            if(Button(new Rect(60,316,380,65),"02   먼저 피하기     /     투사체 2회 회피 후 처치")) ResetGame(Mode.Puzzle2);
            if(Button(new Rect(60,392,380,65),"03   위험한 암살     /     은신을 사용해 두 적 처치")) ResetGame(Mode.Puzzle3);
            if(Button(new Rect(60,468,380,65),"∞   자유 연습     /     계속 등장하는 표적")) ResetGame(Mode.Free);
            GUI.Label(new Rect(60,558,365,65),"우클릭 이동 · 마우스 방향 조준\n우클릭 적 공격 · Q 가위 · W 표식 · E 은신 · R 궁극기",small);
            GUI.Label(new Rect(840,600,400,50),"관찰  →  회피  →  진입  →  마무리",label); return;
        }
        if(stealthTint>.01f && !ended) {
            // Stealth: cool violet wash with darkened edges, drawn under the HUD.
            GUI.color=new Color(.30f,.14f,.58f,.11f*stealthTint); GUI.DrawTexture(new Rect(0,64,1280,656),white);
            GUI.color=new Color(.13f,.04f,.28f,.8f*stealthTint); GUI.DrawTexture(new Rect(0,64,1280,656),vignette);
            GUI.color=Color.white;
        }
        string title=mode==Mode.Puzzle1?"01  진입의 순간":mode==Mode.Puzzle2?"02  먼저 피하기":mode==Mode.Puzzle3?"03  위험한 암살":"∞  자유 연습";
        string goal=mode==Mode.Puzzle1?"2회 이하 피격으로 표적 처치":mode==Mode.Puzzle2?"투사체 2회 회피 후 표적 처치":mode==Mode.Puzzle3?"은신 사용 후 3회 이하 피격으로 두 표적 처치":"표적을 처치하며 자유롭게 연습하세요";
        Box(new Rect(25,84,370,116),new Color(.025f,.045f,.055f,.91f)); GUI.Label(new Rect(43,96,340,30),title,label); GUI.Label(new Rect(43,126,330,24),goal,small); GUI.Label(new Rect(43,155,335,30),"피격 " + hits + "   ·   회피 " + dodged + "   ·   처치 " + kills, label);
        Box(new Rect(25,211,230,62),new Color(.025f,.045f,.055f,.91f)); GUI.Label(new Rect(43,219,200,24),"체력  " + Mathf.Max(0,health).ToString("0") + " / 100",small); Box(new Rect(43,248,190,8),new Color(.18f,.22f,.23f)); Box(new Rect(43,248,190*Mathf.Max(0,health)/100,8),mint);
        if(shadowBuff>0) GUI.Label(new Rect(270,220,280,30),"그림자 평타 " + shadowBuff.ToString("0.0") + "초" + (stealth>0?" · 은신":""),label);
        string[] keys={"Q","W","E","R"}; string[] names={"그림자 가위","영감 표식","그림자 이동","걸작"};
        for(int i=0;i<4;i++) { float x=396+i*125; Box(new Rect(x,642,115,57),new Color(.025f,.045f,.055f,.94f)); GUI.Label(new Rect(x+9,647,100,23),keys[i]+"  "+names[i],small); GUI.Label(new Rect(x+9,670,100,23),(i==2 && shadowBuff>0)?"강화 중":(i==1 && enemies.Exists(e=>e.mark>0))?"표식 유지":cooldown[i]>0?cooldown[i].ToString("0.0")+"초":"준비",label); }
        GUI.Label(new Rect(26,658,362,44),"우클릭 이동·공격(누르고 있으면 계속 이동) · A 공격 · S 정지\nY 시점 고정/해제 · Space 내 캐릭터 · 휠 확대/축소 · ESC 메뉴",small);
        if(Button(new Rect(1095,85,155,38),"다시 시작")) ResetGame(mode);
        if(Button(new Rect(1095,130,155,38),"메뉴로")) ResetGame(Mode.Menu);
        if (mode == Mode.Free && !ended) DrawPracticeSettings();
        foreach(var e in enemies) if(!e.dead && CanSeeEnemy(e)) { Vector3 screen=cam.WorldToScreenPoint(e.body.transform.position+Vector3.up*2.5f); if(screen.z<=0) continue; float x=screen.x/sx, y=(Screen.height-screen.y)/sy; Box(new Rect(x-36,y,72,6),new Color(.15f,.1f,.1f)); Box(new Rect(x-36,y,72*Mathf.Clamp01(e.hp/e.maxHp),6),red); if(e.boss) GUI.Label(new Rect(x-35,y-42,110,22),"연습 보스",small); if(e.mark>0) GUI.Label(new Rect(x-35,y-23,100,22),e.mark>WReadyTime?"영감 "+(e.mark-WReadyTime).ToString("0.0"):"영감 완성",small); }
        if(ended) { Box(new Rect(0,64,1280,656),new Color(0,0,0,.68f)); Box(new Rect(400,220,480,260),new Color(.04f,.075f,.08f,.98f)); GUI.Label(new Rect(430,246,420,45),won?"훈련 성공":"훈련 종료",heading); GUI.Label(new Rect(430,300,400,50),won?"좋은 진입이었습니다.":"목표 조건을 확인하고 다시 도전하세요.",label); GUI.Label(new Rect(430,348,400,30),"피격 " + hits + "   ·   회피 " + dodged + "   ·   처치 " + kills + "   ·   " + elapsed.ToString("0.0") + "초",small); if(Button(new Rect(430,399,185,48),"다시 도전")) ResetGame(mode); if(Button(new Rect(630,399,185,48),"메뉴로")) ResetGame(Mode.Menu); }
    }

    void DrawPracticeSettings()
    {
        Box(new Rect(960, 182, 290, 337), new Color(.025f, .045f, .055f, .91f));
        qLevel = DrawPracticeValue("Q 레벨", qLevel, 191, 1, 1, QDamage.Length);
        wLevel = DrawPracticeValue("W 레벨", wLevel, 219, 1, 1, WDamage.Length);
        eLevel = DrawPracticeValue("E 레벨", eLevel, 247, 1, 1, EDamage.Length);
        rLevel = DrawPracticeValue("R 레벨", rLevel, 275, 1, 1, RTickDamage.Length);
        attackPower = DrawPracticeValue("공격력", attackPower, 303, 5, 0, float.MaxValue);
        GUI.Label(new Rect(973, 337, 260, 23), $"추가 공격력 {BonusAttackPower:0.#} · Q 공속 +{QAttackSpeedBonus[qLevel - 1]:P0}", small);

        int qRank = qLevel - 1;
        GUI.Label(new Rect(973, 359, 265, 22),
            $"Q 피해 {QDamage[qRank] + 1.1f * attackPower:0.#} / 중앙 {QCenterDamage[qRank] + 1.4f * attackPower:0.#} · {QCooldown[qRank]:0}초", small);
        GUI.Label(new Rect(973, 381, 265, 22),
            $"W 축적 {MarkStoreRatio:P1} · 폭발 {MarkSkillDamage:0.#} + 축적", small);
        GUI.Label(new Rect(973, 403, 265, 22),
            $"E 추가 {ShadowStrikeDamage:0.#} · 은신 {EStealth[eLevel - 1]:0.#}초 · {ECooldown[eLevel - 1]:0}초", small);
        GUI.Label(new Rect(973, 425, 265, 22),
            $"R 틱 {UltimateTickDamage:0.#} · 마지막 {UltimateExitDamage:0.#} · {RCooldown[rLevel - 1]:0}초", small);
        if (!enemies.Exists(e => e.boss && !e.dead) &&
            Button(new Rect(973, 452, 263, 33), "연습 보스 추가"))
            AddEnemy(new Vector3(3, 0, -5), 1f, boss: true);
    }

    int DrawPracticeValue(string name, int value, float y, int step, int min, int max)
        => (int)DrawPracticeValue(name, (float)value, y, step, min, max);

    float DrawPracticeValue(string name, float value, float y, float step, float min, float max)
    {
        GUI.Label(new Rect(973, y, 170, 26), $"{name} {value:0.#}", label);
        if (Button(new Rect(1165, y, 32, 25), "−")) value = Mathf.Max(min, value - step);
        if (Button(new Rect(1204, y, 32, 25), "+")) value = Mathf.Min(max, value + step);
        return value;
    }
}
