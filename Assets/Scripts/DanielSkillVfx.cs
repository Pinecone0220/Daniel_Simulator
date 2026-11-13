using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime-authored skill presentation, timed from the supplied Daniel reference clips.
/// Contains no hit detection: the simulator owns skill state and damage timing.
/// </summary>
public sealed class DanielSkillVfx : MonoBehaviour
{
    enum Kind { Scissors, Mark, MarkBurst, Smoke, Trail, Ultimate, Slash, Ghost, Ping, Range, Latch, Stream, Veil, Boom, Link, DaggerCloak, DaggerCut, FireTrail }
    Kind kind;
    float age, duration, windup, markProgress, height;
    bool markReady, rangeHighlight;
    Transform linkTarget;
    Transform follow, leftBlade, rightBlade, fan, flash, arrow, timer, range;
    Vector3 startPoint;
    LineRenderer seam;
    readonly LineRenderer[] markFill = new LineRenderer[8];
    readonly Vector3[] handleArc = new Vector3[17];
    float markOpen;
    Camera markCamera;
    Vector3 exitDirection = Vector3.forward;
    readonly List<Material> ownedMaterials = new List<Material>();
    readonly List<Mesh> ownedMeshes = new List<Mesh>();
    readonly List<Transform> motes = new List<Transform>();
    readonly List<Transform> rings = new List<Transform>();
    readonly List<Vector3> moteOrigins = new List<Vector3>();
    readonly List<Vector3> moteVelocities = new List<Vector3>();
    Material dark, purple, bright, soft, cyan, extra, daggerEdge;
    Color extraColor;

    static readonly Color DaggerWind = new Color(.14f, .85f, 1f, .64f);
    static readonly Color DaggerEdge = new Color(.90f, .98f, 1f, 1f);
    static readonly Color DaggerImpact = new Color(1f, .19f, .13f, .96f);

    static readonly Color ShadowColor = new Color(.055f, .015f, .10f, .9f);
    static readonly Color VioletColor = new Color(.59f, .16f, .85f, .85f);
    static readonly Color WhiteColor = new Color(.96f, .70f, 1f, 1f);

    static DanielSkillVfx New(Kind type, Vector3 point, Vector3 direction, float life)
    {
        var go = new GameObject("Daniel VFX / " + type);
        go.transform.position = point;
        direction.y = 0;
        if (direction.sqrMagnitude > .0001f) go.transform.rotation = Quaternion.LookRotation(direction.normalized);
        var effect = go.AddComponent<DanielSkillVfx>();
        effect.kind = type;
        effect.duration = life;
        effect.dark = effect.MakeMaterial(ShadowColor);
        effect.purple = effect.MakeMaterial(VioletColor);
        effect.bright = effect.MakeMaterial(WhiteColor);
        effect.soft = effect.MakeMaterial(new Color(.45f, .10f, .65f, .18f));
        return effect;
    }

    public static DanielSkillVfx SpawnQ(Vector3 center, Vector3 direction, float windup = DanielSimulator.QWindup)
    {
        direction.y = 0;
        if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
        direction.Normalize();
        const float reach = DanielSimulator.QRadius, halfAngle = DanielSimulator.QHalfAngle, centre = DanielSimulator.QCenterHalfWidth;
        var fx = New(Kind.Scissors, center - direction * (reach * .5f) + Vector3.up * .13f, direction, windup + .3f);
        fx.windup = Mathf.Max(.15f, windup);
        fx.fan = fx.MeshPart("Violet swept area", fx.SectorMesh(reach, halfAngle * 2), fx.soft, fx.transform).transform;
        fx.leftBlade = fx.ScissorHalf("Left scissor", -1);
        fx.rightBlade = fx.ScissorHalf("Right scissor", 1);
        fx.flash = fx.Line("Closing seam", new[] {new Vector3(0,.04f,0), new Vector3(0,.04f,reach)}, .075f, fx.bright, fx.transform).transform;
        fx.flash.gameObject.SetActive(false);
        fx.leftBlade.localRotation = Quaternion.Euler(0, -halfAngle, 0);
        fx.rightBlade.localRotation = Quaternion.Euler(0, halfAngle, 0);
        // Static hit area: the whole fan deals the outer damage, the bright strip deals the centre damage.
        fx.range = new GameObject("Hit range").transform; fx.range.SetParent(fx.transform, false);
        fx.range.localPosition = Vector3.down * .08f;
        fx.MeshPart("Outer damage area", fx.SectorMesh(reach, halfAngle * 2), fx.soft, fx.range);
        var outline = new Vector3[20];
        outline[0] = new Vector3(0, .01f, 0);
        for (int i = 0; i <= 17; i++)
        {
            float a = Mathf.Lerp(-halfAngle, halfAngle, i / 17f) * Mathf.Deg2Rad;
            outline[i + 1] = new Vector3(Mathf.Sin(a) * reach, .01f, Mathf.Cos(a) * reach);
        }
        outline[19] = outline[0];
        fx.Line("Outer damage edge", outline, .045f, fx.purple, fx.range);
        float near = centre / Mathf.Tan(halfAngle * Mathf.Deg2Rad), far = Mathf.Sqrt(reach * reach - centre * centre);
        var strip = new[] { new Vector3(0, .02f, 0), new Vector3(centre, .02f, near), new Vector3(centre, .02f, far), new Vector3(0, .02f, reach), new Vector3(-centre, .02f, far), new Vector3(-centre, .02f, near) };
        var stripMesh = new Mesh(); stripMesh.vertices = strip; stripMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 };
        stripMesh.RecalculateNormals(); stripMesh.RecalculateBounds(); fx.ownedMeshes.Add(stripMesh);
        fx.extraColor = new Color(1f, .62f, .95f, .42f);
        fx.extra = fx.MakeMaterial(fx.extraColor);
        fx.MeshPart("Centre damage area", stripMesh, fx.extra, fx.range);
        fx.Line("Centre damage edge", new[] { strip[0] + Vector3.up * .01f, strip[1] + Vector3.up * .01f, strip[2] + Vector3.up * .01f, strip[3] + Vector3.up * .01f, strip[4] + Vector3.up * .01f, strip[5] + Vector3.up * .01f, strip[0] + Vector3.up * .01f }, .03f, fx.bright, fx.range);
        var shard = fx.Octahedron();
        for (int i = 0; i < 10; i++)
        {
            float z = .5f + i * .31f; int side = i % 2 == 0 ? -1 : 1;
            var mote = fx.MeshPart("Cut shard", shard, i % 3 == 0 ? fx.bright : fx.purple, fx.transform).transform;
            mote.localScale = Vector3.zero;
            fx.motes.Add(mote); fx.moteOrigins.Add(new Vector3(0, .1f, z));
            fx.moteVelocities.Add(new Vector3(side * (1.6f + (i % 3) * .5f), 1.2f + (i % 4) * .35f, .6f));
        }
        return fx;
    }

    /// <summary>
    /// W mark above the target's head (sheet 01-05). A closed shadow-violet scissor charges from the blade tips up
    /// to the hinge; on full charge a round flash clears into a crescent and the scissor opens into a bright X.
    /// </summary>
    public static DanielSkillVfx CreateMark(Transform target, float height = 2.65f)
    {
        if (!target) return null;
        var fx = New(Kind.Mark, target.position + Vector3.up * height, Vector3.forward, 0);
        fx.follow = target; fx.height = height; fx.windup = -1;
        fx.markCamera = Camera.main;
        if (!fx.markCamera) fx.markCamera = FindFirstObjectByType<Camera>();
        // Uncharged body colour; it turns white-violet once the mark is ready.
        fx.extraColor = MarkDim; fx.extra = fx.MakeMaterial(fx.extraColor);
        fx.cyan = fx.MakeMaterial(WhiteColor);
        fx.fan = fx.MeshPart("Charge glow", fx.DiscXY(.5f), fx.soft, fx.transform).transform;
        fx.fan.localPosition = new Vector3(0, -.12f, .04f);
        fx.arrow = fx.MeshPart("Crescent", fx.CrescentMesh(.66f, .56f, .24f), fx.cyan, fx.transform).transform;
        fx.arrow.localPosition = new Vector3(0, -.1f, .02f);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1 : 1;
            var pivot = new GameObject(i == 0 ? "Mark left" : "Mark right").transform;
            pivot.SetParent(fx.transform, false); pivot.localPosition = new Vector3(0, MarkHinge, 0);
            if (i == 0) fx.leftBlade = pivot; else fx.rightBlade = pivot;
            // Dark silhouette first; the charge light is drawn over it and climbs all the way to the handle tops.
            fx.Line("Blade shadow", new[] { new Vector3(0, .03f, 0), new Vector3(0, -MarkBlade - .04f, 0) }, .135f, fx.dark, pivot);
            fx.Line("Neck shadow", new[] { Vector3.zero, new Vector3(side * .1f, .17f, 0) }, .1f, fx.dark, pivot);
            fx.Circle("Handle shadow", new Vector3(side * .13f, .28f, 0), .1f, .09f, fx.dark, pivot, true);
            fx.Line("Blade body", new[] { new Vector3(0, 0, -.005f), new Vector3(0, -MarkBlade, -.005f) }, .07f, fx.extra, pivot);
            fx.Line("Neck body", new[] { new Vector3(0, 0, -.005f), new Vector3(side * .1f, .17f, -.005f) }, .04f, fx.extra, pivot);
            fx.Circle("Handle body", new Vector3(side * .13f, .28f, -.005f), .1f, .04f, fx.extra, pivot, true);
            fx.markFill[i] = fx.Line("Blade charge", new Vector3[2], .07f, fx.bright, pivot);
            fx.markFill[i].numCapVertices = 0;
            fx.markFill[4 + i] = fx.Line("Neck charge", new Vector3[2], .04f, fx.bright, pivot);
            fx.markFill[4 + i].numCapVertices = 0;
            fx.markFill[6 + i] = fx.Line("Handle charge", fx.handleArc, .04f, fx.bright, pivot);
        }
        fx.markFill[2] = fx.Line("Charge boundary", new Vector3[2], .024f, fx.purple, fx.transform);
        var ring = new Vector3[25];
        for (int i = 0; i < ring.Length; i++) { float a = Mathf.Lerp(.5f, 5.1f, i / (ring.Length - 1f)); ring[i] = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0) * .66f; }
        fx.markFill[3] = fx.Line("Ready ring", ring, .02f, fx.purple, fx.transform);
        fx.markFill[3].transform.localPosition = new Vector3(0, -.1f, .01f);
        fx.flash = fx.MeshPart("Full-charge flash", fx.DiscXY(.66f), fx.cyan, fx.transform).transform;
        fx.flash.localPosition = new Vector3(0, -.1f, -.03f);
        for (int i = 0; i < 6; i++)
        {
            var mote = fx.MeshPart("Charge mote", fx.Octahedron(), fx.purple, fx.transform).transform;
            mote.localScale = Vector3.one * (.018f + (i % 3) * .008f);
            fx.motes.Add(mote); fx.moteOrigins.Add(new Vector3(Mathf.Sin(i * 2.4f) * (.2f + (i % 2) * .12f), 0, i / 6f));
        }
        fx.Sample(0);
        return fx;
    }
    const float MarkHinge = .2f, MarkBlade = .62f;
    static readonly Color MarkDim = new Color(.27f, .13f, .40f, .95f);

    /// <param name="progress01">Elapsed buildup divided by four seconds.</param>
    public void SetMarkProgress(float progress01, bool ready)
    {
        markProgress = Mathf.Clamp01(progress01); markReady = ready;
    }

    /// <summary>
    /// W's ready mark consumed by a valid hit (sheet 06-08). A giant shadow scissor opens over the head and snaps
    /// shut; the snap fires white vertical flashes, a hit glow on the torso and a ring pulse under the feet.
    /// </summary>
    public static DanielSkillVfx MarkBurst(Vector3 position)
    {
        var fx = New(Kind.MarkBurst, position, Vector3.forward, .78f);
        fx.markCamera = Camera.main;
        if (!fx.markCamera) fx.markCamera = FindFirstObjectByType<Camera>();
        fx.fan = new GameObject("Giant scissors").transform; fx.fan.SetParent(fx.transform, false);
        fx.fan.localPosition = Vector3.up * 3.1f;
        fx.leftBlade = fx.VerticalScissorHalf("W snap left", -1); fx.leftBlade.SetParent(fx.fan, false);
        fx.rightBlade = fx.VerticalScissorHalf("W snap right", 1); fx.rightBlade.SetParent(fx.fan, false);
        fx.flash = new GameObject("Vertical flash").transform; fx.flash.SetParent(fx.transform, false);
        fx.flash.localPosition = Vector3.up * 1.2f;
        for (int side = -1; side <= 1; side += 2)
        {
            fx.Line("Violet flash", new[] { new Vector3(side * .17f, -.6f, 0), new Vector3(side * .17f, 2.5f, 0) }, .2f, fx.purple, fx.flash);
            fx.Line("White flash", new[] { new Vector3(side * .17f, -.75f, -.02f), new Vector3(side * .17f, 2.7f, -.02f) }, .08f, fx.bright, fx.flash);
        }
        fx.arrow = new GameObject("Body hit").transform; fx.arrow.SetParent(fx.transform, false);
        fx.arrow.localPosition = Vector3.up * 1.15f;
        fx.MeshPart("Hit glow", fx.DiscXY(.5f), fx.bright, fx.arrow);
        var sparks = new GameObject("Hit sparks").transform; sparks.SetParent(fx.arrow, false);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4 + .2f; var d = new Vector3(Mathf.Sin(a), Mathf.Cos(a) * .55f, 0);
            fx.Line("Spark", new[] { d * .3f, d * (.8f + (i % 2) * .35f) }, .035f, i % 2 == 0 ? fx.bright : fx.purple, sparks);
        }
        fx.timer = new GameObject("Foot pulse").transform; fx.timer.SetParent(fx.transform, false);
        fx.timer.localPosition = Vector3.up * .06f;
        fx.MeshPart("Ground glow", fx.SectorMesh(.75f, 360), fx.soft, fx.timer);
        fx.Circle("Pulse", Vector3.up * .01f, 1f, .07f, fx.purple, fx.timer, false);
        fx.range = new GameObject("Impact pulse").transform; fx.range.SetParent(fx.transform, false);
        fx.range.localPosition = Vector3.up * .08f;
        fx.Circle("Impact ring", Vector3.zero, 1f, .11f, fx.purple, fx.range, false);
        fx.Circle("Impact edge", Vector3.up * .01f, .93f, .04f, fx.bright, fx.range, false);
        var shard = fx.Octahedron();
        for (int i = 0; i < 10; i++)
        {
            float a = i * 2.39996f;
            var mote = fx.MeshPart("Shard", shard, i % 3 == 0 ? fx.bright : fx.purple, fx.transform).transform;
            fx.motes.Add(mote); fx.moteOrigins.Add(new Vector3(0, 1.15f, 0));
            fx.moteVelocities.Add(new Vector3(Mathf.Sin(a) * (1.6f + (i % 3) * .5f), 1.2f + (i % 4) * .6f, Mathf.Cos(a) * (1.6f + (i % 3) * .5f)));
        }
        fx.Sample(0);
        return fx;
    }

    public static DanielSkillVfx SmokeBurst(Vector3 position, Vector3 direction)
    {
        var fx = New(Kind.Smoke, position + Vector3.up * .07f, direction, .65f);
        fx.MeshPart("Ground ink", fx.SectorMesh(.75f, 360), fx.dark, fx.transform);
        fx.flash = new GameObject("Violet ripple").transform; fx.flash.SetParent(fx.transform, false);
        fx.Circle("Ripple", Vector3.up * .02f, .6f, .05f, fx.purple, fx.flash, false);
        for (int i = 0; i < 11; i++)
        {
            float angle = i * 2.39996f;
            var start = new Vector3(Mathf.Sin(angle) * .25f, .12f + (i%3)*.11f, Mathf.Cos(angle) * .25f);
            var mote = fx.MeshPart("Shadow wisp", fx.Octahedron(), i%3==0 ? fx.purple : fx.dark, fx.transform).transform;
            mote.localPosition = start;
            mote.localScale = Vector3.one * (.19f + (i%3)*.075f);
            fx.motes.Add(mote); fx.moteOrigins.Add(start);
            fx.moteVelocities.Add(new Vector3(Mathf.Sin(angle)*.8f,.55f+(i%3)*.2f,Mathf.Cos(angle)*.8f));
        }
        return fx;
    }

    public static DanielSkillVfx DashTrail(Vector3 from, Vector3 to)
    {
        var fx = New(Kind.Trail, from + Vector3.up * .22f, to-from, .32f);
        float length = Vector3.Distance(from,to);
        fx.Line("Shadow wake", new[] {Vector3.zero,new Vector3(0,0,length)}, .22f, fx.dark, fx.transform);
        fx.Line("Violet wake", new[] {new Vector3(0,.02f,0),new Vector3(0,.02f,length)}, .045f, fx.purple, fx.transform);
        return fx;
    }

    /// <summary>
    /// E launch: a shadow "sonic boom". A shock ring bursts at the start, a mach cone and speed streaks ride
    /// the dash, smaller rings pop along the path, and a ground shock lands with the character.
    /// </summary>
    public static DanielSkillVfx SonicBoom(Vector3 from, Vector3 to, float travel)
    {
        var fx = New(Kind.Boom, from + Vector3.up * 1f, to - from, travel + .5f);
        fx.windup = travel; fx.height = Vector3.Distance(from, to);
        float length = fx.height;
        for (int i = 0; i < 4; i++)
        {
            var ring = new GameObject("Shock ring").transform; ring.SetParent(fx.transform, false);
            ring.localPosition = new Vector3(0, 0, length * i / 3f);
            fx.Circle("Shadow rim", Vector3.zero, 1.06f, .14f, fx.dark, ring, true);
            fx.Circle("Violet ring", Vector3.zero, 1f, .08f, fx.purple, ring, true);
            fx.Circle("Bright edge", Vector3.zero, .93f, .025f, fx.bright, ring, true);
            ring.localScale = Vector3.zero; fx.rings.Add(ring);
        }
        // Bow shock: a swept-back arc just above the ground that rides ahead of the character.
        fx.arrow = new GameObject("Bow shock").transform; fx.arrow.SetParent(fx.transform, false);
        var bow = new Vector3[15];
        for (int i = 0; i < bow.Length; i++) { float x = Mathf.Lerp(-1.15f, 1.15f, i / (float)(bow.Length - 1)); bow[i] = new Vector3(x, -.84f, .75f - x * x * 1.15f); }
        fx.Line("Bow shadow", bow, .13f, fx.dark, fx.arrow);
        fx.Line("Bow edge", bow, .04f, fx.bright, fx.arrow);
        // Speed streaks around the body.
        for (int i = 0; i < 9; i++)
        {
            float angle = i * 2.39996f, radius = .35f + (i % 3) * .22f;
            var offset = new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius * .7f, (i % 4) * .22f);
            var streak = fx.Line("Speed streak", new[] { offset, offset }, 0, i % 3 == 0 ? fx.bright : fx.purple, fx.transform);
            fx.motes.Add(streak.transform); fx.moteOrigins.Add(offset);
        }
        // Arrival shock on the ground.
        fx.flash = new GameObject("Arrival shock").transform; fx.flash.SetParent(fx.transform, false);
        fx.flash.localPosition = new Vector3(0, -.93f, length);
        fx.MeshPart("Ground ink", fx.SectorMesh(.55f, 360), fx.dark, fx.flash);
        fx.Circle("Shock", Vector3.up * .02f, 1f, .1f, fx.purple, fx.flash, false);
        fx.Circle("Shock edge", Vector3.up * .03f, .9f, .03f, fx.bright, fx.flash, false);
        fx.flash.gameObject.SetActive(false);
        fx.Sample(0); // settle the first frame before it is ever drawn
        return fx;
    }

    /// <summary>Captures the current skinned pose into short-lived translucent meshes.</summary>
    public static DanielSkillVfx Afterimage(Transform model, float lifetime = .28f)
    {
        if (!model) return null;
        var fx = New(Kind.Ghost, Vector3.zero, Vector3.forward, lifetime);
        fx.extraColor = new Color(.5f, .14f, .78f, .34f);
        fx.extra = fx.MakeMaterial(fx.extraColor);
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.enabled || !skin.sharedMesh) continue;
            var mesh = new Mesh(); skin.BakeMesh(mesh); mesh.RecalculateBounds(); fx.ownedMeshes.Add(mesh);
            var part = fx.MeshPart("Pose echo", mesh, fx.extra, fx.transform);
            part.transform.position = skin.transform.position;
            part.transform.rotation = skin.transform.rotation;
            // Whether baked vertices already include the transform scale depends on the bake mode,
            // so use whichever scale makes the echo match the live renderer's size.
            float baked = mesh.bounds.size.magnitude, live = skin.bounds.size.magnitude, scaled = Mathf.Max(.0001f, skin.transform.lossyScale.x);
            float ratio = baked > .0001f ? Mathf.Max(.0001f, live / baked) : 1;
            bool applyScale = Mathf.Abs(Mathf.Log(ratio / scaled)) < Mathf.Abs(Mathf.Log(ratio));
            part.transform.localScale = applyScale ? skin.transform.lossyScale : Vector3.one;
        }
        return fx;
    }

    /// <summary>R pre-delay: a shadow pool spreads under the caster while smoke spirals down into it.</summary>
    public static DanielSkillVfx Latch(Transform caster, float windup = .5f)
    {
        if (!caster) return null;
        var fx = New(Kind.Latch, caster.position, Vector3.forward, windup + .06f);
        fx.follow = caster; fx.windup = Mathf.Max(.1f, windup);
        fx.fan = new GameObject("Spreading shadow").transform; fx.fan.SetParent(fx.transform, false);
        fx.MeshPart("Shadow pool", fx.SectorMesh(1.05f, 360), fx.dark, fx.fan).transform.localPosition = Vector3.up * .05f;
        fx.Circle("Violet rim", Vector3.up * .07f, 1.05f, .06f, fx.purple, fx.fan, false);
        var wisp = fx.Octahedron();
        for (int i = 0; i < 14; i++)
        {
            var mote = fx.MeshPart("Sinking smoke", wisp, i % 4 == 0 ? fx.purple : fx.dark, fx.transform).transform;
            fx.motes.Add(mote);
        }
        fx.Sample(0);
        return fx;
    }

    /// <summary>R entry: smoke pours along the ground from the cast point into the target's shadow.</summary>
    public static DanielSkillVfx SmokeStream(Vector3 from, Transform target)
    {
        if (!target) return null;
        var fx = New(Kind.Stream, from, Vector3.forward, .34f);
        fx.follow = target; fx.startPoint = from;
        var wisp = fx.Octahedron();
        for (int i = 0; i < 12; i++)
        {
            var mote = fx.MeshPart("Pouring smoke", wisp, i % 3 == 0 ? fx.purple : fx.dark, fx.transform).transform;
            mote.localScale = Vector3.zero; fx.motes.Add(mote);
        }
        return fx;
    }

    public static DanielSkillVfx BeginUltimate(Transform target)
    {
        if (!target) return null;
        var fx = New(Kind.Ultimate, target.position + Vector3.up*.1f, Vector3.forward, 2.6f);
        fx.follow = target;
        fx.fan = fx.MeshPart("Shadow pool", fx.SectorMesh(.85f,360), fx.dark, fx.transform).transform;
        fx.Circle("Shadow ring", Vector3.up*.02f,.70f,.055f,fx.purple,fx.transform,false);
        fx.Circle("Inner ring", Vector3.up*.03f,.36f,.035f,fx.bright,fx.transform,false);
        fx.timer = new GameObject("Time left").transform; fx.timer.SetParent(fx.transform,false);
        fx.Circle("Timer ring", Vector3.up*.04f,1.25f,.03f,fx.purple,fx.timer,false);
        fx.cyan = fx.MakeMaterial(new Color(.18f,.86f,.91f,.8f));
        fx.arrow = new GameObject("Exit direction").transform; fx.arrow.SetParent(fx.transform,false);
        fx.Line("Exit guide",new[]{new Vector3(0,.04f,.65f),new Vector3(0,.04f,2f)},.045f,fx.cyan,fx.arrow);
        fx.Line("Exit arrow",new[]{new Vector3(-.28f,.04f,1.7f),new Vector3(0,.04f,2f),new Vector3(.28f,.04f,1.7f)},.045f,fx.cyan,fx.arrow);
        for(int i=0;i<8;i++)
        {
            var mote=fx.MeshPart("Orbiting shadow",fx.Octahedron(),fx.dark,fx.transform).transform;
            mote.localScale=new Vector3(.14f,.3f,.14f); fx.motes.Add(mote);
        }
        return fx;
    }

    public void SetExitDirection(Vector3 direction)
    {
        direction.y=0; if(direction.sqrMagnitude>.001f) exitDirection=direction.normalized;
    }
    public void Stop() { if (this) Destroy(gameObject); }

    /// <summary>Clears standalone effects when the practice round is reset.</summary>
    public static void ClearAll()
    {
        foreach (var effect in FindObjectsByType<DanielSkillVfx>(FindObjectsSortMode.None))
            effect.Stop();
    }

    public static DanielSkillVfx ExitSlash(Vector3 position, Vector3 direction)
    {
        SmokeBurst(position,direction);
        var fx=New(Kind.Slash,position+Vector3.up*.75f,direction,.42f);
        fx.Arc("Exit cut",1.45f,125,.085f,fx.bright,fx.transform);
        fx.Arc("Violet edge",1.50f,145,.18f,fx.purple,fx.transform);
        fx.Line("Exit thrust",new[]{new Vector3(0,-.2f,0),new Vector3(0,-.2f,1.65f)},.06f,fx.bright,fx.transform);
        return fx;
    }

    public static DanielSkillVfx BeginDaggerCloak(Transform owner, float lifetime)
    {
        var fx = New(Kind.DaggerCloak, owner.position, owner.forward, lifetime);
        fx.follow = owner;
        fx.cyan = fx.MakeMaterial(DaggerWind);
        fx.fan = new GameObject("Cloak wind").transform;
        fx.fan.SetParent(fx.transform, false);
        for (int band = 0; band < 3; band++)
        {
            var points = new Vector3[24];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1);
                float angle = (band * 120f + t * 152f) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(.95f, .53f, t);
                points[i] = new Vector3(Mathf.Sin(angle) * radius, .13f + .92f * t, Mathf.Cos(angle) * radius);
            }
            var ribbon = fx.Line("Spiralling wind", points, .10f, fx.cyan, fx.fan);
            ribbon.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.48f, 1), new Keyframe(1, 0));
        }
        fx.Line("Low speed streak", new[] { new Vector3(-.33f, .13f, .1f), new Vector3(-.4f, .11f, -1.05f), new Vector3(-.23f, .1f, -1.85f) }, .055f, fx.cyan, fx.transform);
        fx.Line("High speed streak", new[] { new Vector3(.26f, .8f, .05f), new Vector3(.36f, .57f, -.8f), new Vector3(.18f, .35f, -1.45f) }, .07f, fx.cyan, fx.transform);
        return fx;
    }

    public static void DaggerStrike(Vector3 from, Vector3 landing, Vector3 target)
    {
        Vector3 travel = landing - from;
        travel.y = 0;
        var trail = New(Kind.DaggerCut, from + Vector3.up * .62f, travel, .23f);
        trail.cyan = trail.MakeMaterial(DaggerWind);
        float length = travel.magnitude;
        var streak = trail.Line("Dash streak", new[] { Vector3.zero, new Vector3(.10f, .07f, length * .6f), Vector3.forward * length }, .33f, trail.cyan, trail.transform);
        streak.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.75f, 1), new Keyframe(1, 0));

        Vector3 direction = target - landing;
        var fx = New(Kind.DaggerCut, landing + Vector3.up * .88f, direction, .32f);
        fx.cyan = fx.MakeMaterial(DaggerWind);
        fx.extraColor = DaggerImpact;
        fx.extra = fx.MakeMaterial(DaggerImpact);
        var edge = fx.MakeMaterial(DaggerEdge);
        fx.daggerEdge = edge;
        fx.fan = new GameObject("Sweeping blade").transform;
        fx.fan.SetParent(fx.transform, false);
        fx.Arc("Cyan blade trail", 1.32f, 136, .24f, fx.cyan, fx.fan);
        fx.Arc("White cutting edge", 1.39f, 126, .088f, edge, fx.fan);
        Vector3 hit = fx.transform.InverseTransformPoint(target + Vector3.up * 1.05f);
        fx.flash = new GameObject("Hit spark").transform;
        fx.flash.SetParent(fx.transform, false);
        fx.flash.localPosition = hit;
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3f;
            var ray = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Sin(angle + .55f) * .42f);
            fx.Line("Red impact ray", new[] { ray * .06f, ray * (.35f + .11f * (i % 3)) }, .055f, fx.extra, fx.flash);
            fx.Line("White impact core", new[] { Vector3.zero, ray * .21f }, .038f, edge, fx.flash);
        }
    }

    public static DanielSkillVfx AdrianaFireTrail(Vector3 start, Vector3 end, float width, float travel, float lifetime)
    {
        float length = Vector3.Distance(start, end);
        var fx = New(Kind.FireTrail, start, end - start, travel + lifetime);
        fx.windup = travel;
        fx.extraColor = new Color(1f, .30f, .04f, .82f);
        fx.extra = fx.MakeMaterial(fx.extraColor);
        fx.cyan = fx.MakeMaterial(new Color(1f, .72f, .16f, .92f));
        fx.fan = fx.MeshPart("Scorched ground", fx.SectorMesh(.5f, 360), fx.dark, fx.transform).transform;
        fx.fan.localScale = Vector3.zero;
        fx.fan.localPosition = Vector3.up * .04f;

        var ember = fx.Octahedron();
        int count = Mathf.Max(2, Mathf.CeilToInt(length * 7));
        for (int i = 0; i < count; i++)
        {
            float z = length * i / (count - 1);
            float x = Mathf.Sin(i * 2.4f) * width * .29f;
            var flame = fx.MeshPart("Flame", ember, i % 3 == 0 ? fx.cyan : fx.extra, fx.transform).transform;
            flame.localScale = Vector3.zero;
            fx.motes.Add(flame);
            fx.moteOrigins.Add(new Vector3(x, .23f, z));
        }
        fx.height = width;
        fx.startPoint = Vector3.forward * length;
        return fx;
    }

    public static DanielSkillVfx AttackSlash(Vector3 position, Vector3 direction, bool empowered = false)
    {
        var fx=New(Kind.Slash,position+Vector3.up*.9f,direction,empowered?.32f:.24f);
        if(!empowered)
        {
            fx.Arc("Violet edge",1.03f,118,.11f,fx.purple,fx.transform);
            fx.Arc("Dagger cut",1f,110,.05f,fx.bright,fx.transform);
            return fx;
        }
        for(int side=-1;side<=1;side+=2)
        {
            var tilt=new GameObject("Crossed cut").transform; tilt.SetParent(fx.transform,false);
            tilt.localRotation=Quaternion.Euler(0,0,side*35);
            fx.Arc("Shadow edge",1.22f,135,.24f,fx.dark,tilt);
            fx.Arc("Violet edge",1.18f,128,.13f,fx.purple,tilt);
            fx.Arc("White cut",1.15f,120,.045f,fx.bright,tilt);
        }
        return fx;
    }

    /// <summary>Right-click feedback: a ring closing in on the ordered point (green move, red attack).</summary>
    public static DanielSkillVfx ClickMarker(Vector3 point, bool attack)
    {
        var fx = New(Kind.Ping, new Vector3(point.x, .08f, point.z), Vector3.forward, .35f);
        fx.extraColor = attack ? new Color(1f, .4f, .35f, .95f) : new Color(.35f, .95f, .75f, .95f);
        fx.extra = fx.MakeMaterial(fx.extraColor);
        fx.flash = new GameObject("Closing ring").transform; fx.flash.SetParent(fx.transform, false);
        fx.Circle("Ring", Vector3.zero, .55f, .05f, fx.extra, fx.flash, false);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * .5f;
            var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            fx.Line("Tick", new[] { dir * .55f, dir * .8f }, .05f, fx.extra, fx.flash);
        }
        return fx;
    }

    /// <summary>E: ring around the player showing how far the empowered attack reaches. Lives until stopped.</summary>
    public static DanielSkillVfx CreateRange(Transform owner, float radius)
    {
        if (!owner) return null;
        var fx = New(Kind.Range, owner.position + Vector3.up * .06f, Vector3.forward, 0);
        fx.follow = owner;
        fx.MeshPart("Reach area", fx.SectorMesh(radius, 360), fx.soft, fx.transform);
        fx.Circle("Reach ring", Vector3.up * .02f, radius, .06f, fx.purple, fx.transform, false);
        fx.Circle("Reach edge", Vector3.up * .03f, radius - .09f, .02f, fx.bright, fx.transform, false);
        fx.Sample(0);
        return fx;
    }

    /// <summary>
    /// R availability: ground chevrons running from the caster toward a target damaged within the last 4 s.
    /// Dark while that target is out of R range, bright once it is inside (see SetRangeHighlight). Lives until stopped.
    /// </summary>
    public static DanielSkillVfx CreateUltimateLink(Transform owner, Transform target)
    {
        if (!owner || !target) return null;
        var fx = New(Kind.Link, owner.position + Vector3.up * .07f, target.position - owner.position, 0);
        fx.follow = owner; fx.linkTarget = target;
        fx.extraColor = LinkFar; fx.extra = fx.MakeMaterial(fx.extraColor);
        for (int i = 0; i < 12; i++)
        {
            var chevron = fx.Line("Chevron", new[] { new Vector3(-.17f, 0, -.15f), new Vector3(0, 0, .1f), new Vector3(.17f, 0, -.15f) }, .065f, fx.extra, fx.transform);
            chevron.numCapVertices = 0; chevron.numCornerVertices = 0;
            fx.motes.Add(chevron.transform);
        }
        fx.Sample(0);
        return fx;
    }
    static readonly Color LinkFar = new Color(.03f, .02f, .05f, .92f), LinkNear = new Color(1f, 1f, 1f, 1f);

    /// <summary>E stealth: a faint shadow underfoot with smoke drifting up around the hidden body. Lives until stopped.</summary>
    public static DanielSkillVfx CreateVeil(Transform owner)
    {
        if (!owner) return null;
        var fx = New(Kind.Veil, owner.position, Vector3.forward, 0);
        fx.follow = owner;
        fx.fan = fx.MeshPart("Stealth shadow", fx.SectorMesh(.7f, 360), fx.dark, fx.transform).transform;
        fx.fan.localPosition = Vector3.up * .05f;
        fx.Circle("Stealth rim", Vector3.up * .07f, .7f, .03f, fx.purple, fx.fan, false);
        var wisp = fx.Octahedron();
        for (int i = 0; i < 10; i++)
        {
            var mote = fx.MeshPart("Stealth smoke", wisp, i % 3 == 0 ? fx.purple : fx.dark, fx.transform).transform;
            mote.localScale = Vector3.zero; fx.motes.Add(mote);
        }
        fx.Sample(0);
        return fx;
    }

    /// <summary>Stealth ending: a quick violet ring and a few scattering wisps.</summary>
    public static DanielSkillVfx Reveal(Vector3 position)
    {
        var fx = New(Kind.Smoke, position + Vector3.up * .07f, Vector3.forward, .4f);
        fx.flash = new GameObject("Reveal ring").transform; fx.flash.SetParent(fx.transform, false);
        fx.Circle("Ring", Vector3.up * .02f, .55f, .06f, fx.purple, fx.flash, false);
        fx.Circle("Ring edge", Vector3.up * .03f, .5f, .02f, fx.bright, fx.flash, false);
        var wisp = fx.Octahedron();
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3;
            var start = new Vector3(Mathf.Sin(angle) * .3f, .5f + (i % 3) * .4f, Mathf.Cos(angle) * .3f);
            var mote = fx.MeshPart("Fading wisp", wisp, fx.purple, fx.transform).transform;
            mote.localPosition = start;
            fx.motes.Add(mote); fx.moteOrigins.Add(start);
            fx.moteVelocities.Add(new Vector3(Mathf.Sin(angle) * 1.4f, .5f, Mathf.Cos(angle) * 1.4f));
        }
        return fx;
    }

    /// <summary>Brightens the range ring (or the R link chevrons) while a target is inside range.</summary>
    public void SetRangeHighlight(bool targetInside) { rangeHighlight = targetInside; }

    void Update() { Sample(Time.deltaTime); }

    /// <summary>Advances effects by a supplied interval, also usable for deterministic previews.</summary>
    public void Sample(float dt)
    {
        dt = Mathf.Max(0, dt);
        age+=dt;
        if(duration>0 && age>=duration) { Destroy(gameObject); return; }
        float fade=duration>0?1-Mathf.Clamp01(age/duration):1;
        if(kind==Kind.Scissors)
        {
            // Snap open with a small overshoot, hold with rising tension, then accelerate shut exactly on the hit.
            float open=EaseOutBack(Mathf.Clamp01(age/.14f));
            float shut=Mathf.InverseLerp(windup-.17f,windup,age), closing=shut*shut*shut;
            float tension=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.14f,windup-.17f,age))*4f;
            float past=Mathf.Max(0,age-windup);
            float recoil=age>=windup?-5f*Mathf.Exp(-past*18)*Mathf.Cos(past*40):0;
            float angle=(DanielSimulator.QHalfAngle*open+tension)*(1-closing)+recoil;
            leftBlade.localRotation=Quaternion.Euler(0,-angle,0);
            rightBlade.localRotation=Quaternion.Euler(0,angle,0);
            float grow=1-Mathf.Pow(1-Mathf.Clamp01(age/.12f),3);
            float pop=Mathf.Lerp(.55f,1,grow)*(age>=windup?1+.12f*Mathf.Exp(-past*14):1);
            leftBlade.localScale=rightBlade.localScale=Vector3.one*pop;
            flash.gameObject.SetActive(age>=windup);
            if(age>=windup) { if(!seam) seam=flash.GetComponent<LineRenderer>(); if(seam) seam.widthMultiplier=Mathf.Lerp(.24f,.03f,Mathf.Clamp01(past/.22f)); }
            fan.localScale=new Vector3(Mathf.Max(.03f,angle/DanielSimulator.QHalfAngle),1,1);
            float gone=Mathf.Clamp01(past/.3f);
            fade=age<windup?Mathf.SmoothStep(0,1,age/.07f):(1-gone)*(1-gone);
            float after=age-windup;
            if(range) range.gameObject.SetActive(after<0);
            for(int i=0;i<motes.Count;i++)
            {
                if(after<0) { motes[i].localScale=Vector3.zero; continue; }
                motes[i].localPosition=moteOrigins[i]+moteVelocities[i]*after+Vector3.down*(4f*after*after);
                motes[i].localScale=new Vector3(.07f,.2f,.07f)*Mathf.Clamp01(1-after/.3f);
                motes[i].Rotate(260*dt,340*dt,0);
            }
        }
        if(kind==Kind.Mark)
        {
            if(!follow || !follow.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            transform.position=follow.position+Vector3.up*(height+.12f+Mathf.Sin(age*3)*.04f);
            if(markCamera) transform.rotation=markCamera.transform.rotation;
            // windup holds the moment the mark became ready, so the full-charge flash plays exactly once.
            if(markReady && windup<0) windup=age;
            float ready=markReady?age-windup:-1, charge=markReady?1:markProgress;
            float opening=markReady?Mathf.Lerp(3,30,EaseOutBack(Mathf.Clamp01((ready-.05f)/.2f))):3;
            // Each half carries its handle on one side and swings its blade to the other, forming the X.
            leftBlade.localRotation=Quaternion.Euler(0,0,opening);
            rightBlade.localRotation=Quaternion.Euler(0,0,-opening);
            float appear=1-Mathf.Pow(1-Mathf.Clamp01(age/.18f),3);
            transform.localScale=Vector3.one*appear*(markReady?1.25f+Mathf.Sin(age*8)*.04f:1.05f+charge*.1f);
            // Charge light climbs the whole scissor over the full charge time: blades from the tips to the hinge,
            // then the necks and handle rings, reaching the top exactly when the mark becomes ready.
            const float handleTop=.38f, ringY=.28f, ringRadius=.1f, neckTop=.17f;
            float fill=Mathf.Lerp(-MarkBlade,handleTop,charge); // height above the hinge
            Vector3 tip=new Vector3(0,-MarkBlade,-.01f), hinge=new Vector3(0,0,-.01f);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                markFill[i].enabled=charge>.001f;
                markFill[i].SetPosition(0,tip); markFill[i].SetPosition(1,Vector3.Lerp(tip,hinge,Mathf.Clamp01((fill+MarkBlade)/MarkBlade)));
                float neck=Mathf.Clamp01(fill/neckTop);
                markFill[4+i].enabled=neck>.001f;
                markFill[4+i].SetPosition(0,hinge); markFill[4+i].SetPosition(1,new Vector3(side*.1f*neck,neckTop*neck,-.01f));
                Vector3 hub=new Vector3(side*.13f,ringY,-.01f);
                float sweep=Mathf.Acos(Mathf.Clamp((ringY-fill)/ringRadius,-1,1));
                markFill[6+i].enabled=sweep>.02f;
                for(int n=0;n<handleArc.Length;n++)
                {
                    float a=Mathf.Lerp(-sweep,sweep,n/(handleArc.Length-1f));
                    handleArc[n]=hub+new Vector3(Mathf.Sin(a),-Mathf.Cos(a),0)*ringRadius;
                }
                markFill[6+i].SetPositions(handleArc);
            }
            float level=MarkHinge+fill, shimmer=.8f+.2f*Mathf.Sin(age*14);
            markFill[2].enabled=!markReady && charge>.01f && charge<.995f;
            markFill[2].SetPosition(0,new Vector3(-.26f,level,-.02f)); markFill[2].SetPosition(1,new Vector3(.26f,level,-.02f));
            markFill[2].widthMultiplier=.024f*shimmer;
            for(int i=0;i<motes.Count;i++)
            {
                Vector3 o=moteOrigins[i]; float rise=Mathf.Repeat(o.z+age*.45f,1);
                motes[i].localPosition=new Vector3(o.x,MarkHinge-MarkBlade+rise*.95f,-.02f);
                motes[i].localScale=Vector3.one*(.018f+(i%3)*.008f)*Mathf.Sin(rise*Mathf.PI);
            }
            fan.localScale=Vector3.one*(.55f+.55f*charge+(markReady?.12f*Mathf.Sin(age*8):0));
            // Full charge: a round flash blooms, then clears into a crescent that fades behind the open X.
            flash.gameObject.SetActive(markReady && ready<.12f);
            flash.localScale=Vector3.one*Mathf.Lerp(.25f,1.15f,Mathf.Clamp01(ready/.1f));
            bool crescent=markReady && ready>=.09f && ready<.6f;
            arrow.gameObject.SetActive(crescent);
            arrow.localRotation=Quaternion.Euler(0,0,-25+ready*60);
            arrow.localScale=Vector3.one*(1.05f+ready*.25f);
            Tint(cyan,WhiteColor,markReady?1-Mathf.InverseLerp(.2f,.6f,ready):0);
            markFill[3].enabled=markReady && ready>.18f;
            markFill[3].transform.localRotation=Quaternion.Euler(0,0,age*45);
            extraColor=Color.Lerp(extraColor,markReady?WhiteColor:MarkDim,1-Mathf.Exp(-22*dt));
            Tint(extra,extraColor,1);
            fade=1;
        }
        if(kind==Kind.MarkBurst)
        {
            const float snap=.17f;
            Quaternion face=markCamera?markCamera.transform.rotation:Quaternion.identity;
            float past=Mathf.Max(0,age-snap);
            // 06: the scissor swells open above the head; 07: it snaps shut, blades pointing down at the target.
            fan.rotation=face*Quaternion.Euler(0,0,180);
            fan.localScale=Vector3.one*Mathf.Lerp(.5f,1.75f,EaseOutBack(Mathf.Clamp01(age/.1f)))*(1+.08f*Mathf.Exp(-past*16)*(age>=snap?1:0));
            float closing=Mathf.InverseLerp(.1f,snap,age), opening=Mathf.Lerp(42,-3,closing*closing*closing);
            leftBlade.localRotation=Quaternion.Euler(0,0,-opening);
            rightBlade.localRotation=Quaternion.Euler(0,0,opening);
            // White vertical flashes on the snap.
            // Both sit a little toward the camera so the target's own body does not hide them.
            Vector3 toCamera=face*Vector3.back*.7f;
            flash.rotation=face; flash.position=transform.position+Vector3.up*1.2f+toCamera; flash.gameObject.SetActive(age>=snap && past<.34f);
            var beams=flash.GetComponentsInChildren<LineRenderer>(true);
            for(int i=0;i<beams.Length;i++) beams[i].widthMultiplier=i%2==0?.2f*(1-Mathf.Clamp01(past/.22f)):.08f*(1-Mathf.Clamp01(past/.34f));
            flash.localScale=new Vector3(1+past*.9f,1+past*.5f,1);
            // Hit glow and sparks on the torso.
            arrow.rotation=face; arrow.position=transform.position+Vector3.up*1.15f+toCamera; arrow.gameObject.SetActive(age>=snap && past<.36f);
            float pop=1-Mathf.Pow(1-Mathf.Clamp01(past/.1f),3);
            arrow.GetChild(0).localScale=Vector3.one*Mathf.Lerp(.3f,1.15f,pop)*(1-Mathf.Clamp01((past-.08f)/.24f));
            arrow.GetChild(1).localScale=Vector3.one*Mathf.Lerp(.5f,1.5f,pop);
            foreach(var spark in arrow.GetChild(1).GetComponentsInChildren<LineRenderer>(true)) spark.widthMultiplier=.035f*(1-Mathf.Clamp01(past/.3f));
            // Ring pulses under the feet: a soft one as the scissor appears, a bright one on the snap.
            float first=1-Mathf.Pow(1-Mathf.Clamp01(age/.36f),3);
            timer.localScale=new Vector3(Mathf.Lerp(.3f,1.3f,first),1,Mathf.Lerp(.3f,1.3f,first));
            timer.GetComponentInChildren<LineRenderer>(true).widthMultiplier=.07f*(1-Mathf.Clamp01(age/.42f));
            range.gameObject.SetActive(age>=snap);
            float second=1-Mathf.Pow(1-Mathf.Clamp01(past/.42f),3), spread=Mathf.Lerp(.4f,2.1f,second);
            range.localScale=new Vector3(spread,1,spread);
            var pulse=range.GetComponentsInChildren<LineRenderer>(true);
            pulse[0].widthMultiplier=.11f*(1-Mathf.Clamp01(past/.44f)); pulse[1].widthMultiplier=.04f*(1-Mathf.Clamp01(past/.44f));
            for(int i=0;i<motes.Count;i++)
            {
                motes[i].gameObject.SetActive(age>=snap);
                motes[i].localPosition=moteOrigins[i]+moteVelocities[i]*past+Vector3.down*(3.5f*past*past);
                motes[i].localScale=Vector3.one*(.06f+(i%3)*.02f)*(1-Mathf.Clamp01(past/.5f));
                motes[i].Rotate(210*dt,330*dt,0);
            }
            // 08: everything decays quickly after the snap.
            fade=1-Mathf.Clamp01((age-snap-.12f)/(duration-snap-.12f));
        }
        if(kind==Kind.Boom)
        {
            float length=height, k=Mathf.Clamp01(age/windup), progress=1-(1-k)*(1-k), past=Mathf.Max(0,age-windup);
            // Rings: the launch ring is the big one; the rest pop as the character passes them.
            for(int i=0;i<rings.Count;i++)
            {
                float reach=i/3f, start=windup*(1-Mathf.Sqrt(1-reach));
                float r=Mathf.Clamp01((age-start)/(i==0?.42f:.3f)), grow=1-(1-r)*(1-r)*(1-r);
                float size=age<start?0:Mathf.Lerp(.25f,i==0?1.55f:1.05f-.12f*i,grow);
                rings[i].localScale=new Vector3(size,size,1);
                var lines=rings[i].GetComponentsInChildren<LineRenderer>();
                float live=age<start?0:1; lines[0].widthMultiplier=.14f*(1-r)*live; lines[1].widthMultiplier=.08f*(1-r)*live; lines[2].widthMultiplier=.025f*(1-r*r)*live;
            }
            // Bow shock rides the character, widening as it goes, and thins out before the landing.
            arrow.localPosition=new Vector3(0,0,progress*length);
            float bow=Mathf.Clamp01(age/.05f)*(1-Mathf.Clamp01((age-windup*.75f)/(windup*.3f)));
            float flare=Mathf.Lerp(.45f,1,Mathf.Clamp01(age/(windup*.6f)));
            arrow.localScale=new Vector3(flare,1,flare);
            arrow.gameObject.SetActive(bow>.01f);
            var bowLines=arrow.GetComponentsInChildren<LineRenderer>(true);
            bowLines[0].widthMultiplier=.13f*bow; bowLines[1].widthMultiplier=.04f*bow;
            for(int i=0;i<motes.Count;i++)
            {
                var streak=motes[i].GetComponent<LineRenderer>(); Vector3 o=moteOrigins[i];
                float head=Mathf.Clamp(progress*length+o.z,0,length+.3f), tailLength=(1.1f+(i%3)*.5f)*(1-Mathf.Clamp01(past/.22f));
                streak.SetPosition(0,new Vector3(o.x,o.y,Mathf.Max(0,head-tailLength))); streak.SetPosition(1,new Vector3(o.x,o.y,head));
                streak.widthMultiplier=(.03f+(i%3)*.018f)*Mathf.Clamp01(age/.05f)*(1-Mathf.Clamp01(past/.22f));
            }
            if(age>=windup)
            {
                flash.gameObject.SetActive(true);
                float r=Mathf.Clamp01(past/.42f), spread=Mathf.Lerp(.35f,2.1f,1-(1-r)*(1-r)*(1-r));
                flash.localScale=new Vector3(spread,1,spread);
                var lines=flash.GetComponentsInChildren<LineRenderer>();
                lines[0].widthMultiplier=.1f*(1-r); lines[1].widthMultiplier=.03f*(1-r);
            }
            float tail=1-Mathf.Clamp01((age-duration+.32f)/.32f);
            Tint(dark,ShadowColor,tail); Tint(purple,VioletColor,tail); Tint(bright,WhiteColor,tail);
        }
        if(kind==Kind.Smoke)
        {
            if(flash) { float spread=1+(1-fade*fade)*1.3f; flash.localScale=new Vector3(spread,1,spread); }
            for(int i=0;i<motes.Count;i++)
            {
                motes[i].localPosition=moteOrigins[i]+moteVelocities[i]*age;
                motes[i].localScale=Vector3.one*(.15f+(i%3)*.07f+age*.55f);
                motes[i].Rotate(31*dt,47*dt,0);
            }
        }
        if(kind==Kind.Ultimate)
        {
            if(!follow) { Destroy(gameObject); return; }
            transform.position=follow.position+Vector3.up*.12f;
            arrow.rotation=Quaternion.LookRotation(exitDirection);
            float pulse=1+Mathf.Sin(age*Mathf.PI*4)*.12f;
            fan.localScale=new Vector3(pulse,1,pulse);
            float left=Mathf.Lerp(1,.56f,Mathf.Clamp01(age/2f)); timer.localScale=new Vector3(left,1,left);
            for(int i=0;i<motes.Count;i++)
            {
                float a=age*3+i*Mathf.PI*.25f;
                motes[i].localPosition=new Vector3(Mathf.Sin(a)*.67f,.16f+Mathf.Sin(a*2)*.13f,Mathf.Cos(a)*.67f);
                motes[i].localRotation=Quaternion.Euler(20,a*Mathf.Rad2Deg,35);
            }
            fade=1;
        }
        if(kind==Kind.Latch)
        {
            if(!follow) { Destroy(gameObject); return; }
            transform.position=follow.position;
            float k=Mathf.Clamp01(age/windup);
            float spread=Mathf.Lerp(.25f,1,Mathf.SmoothStep(0,1,k)); fan.localScale=new Vector3(spread,1,spread);
            for(int i=0;i<motes.Count;i++)
            {
                // Each wisp loops from body height down into the pool, faster as the delay runs out.
                float cycle=Mathf.Repeat(age*(1.6f+k*2.2f)+i*.173f,1);
                float a=i*2.4f+age*7, r=Mathf.Lerp(.55f,.12f,cycle);
                motes[i].localPosition=new Vector3(Mathf.Sin(a)*r,Mathf.Lerp(1.9f,.05f,cycle*cycle),Mathf.Cos(a)*r);
                motes[i].localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);
                float size=(.5f+k)*(1-cycle*.6f);
                motes[i].localScale=new Vector3(.13f,.34f,.13f)*size;
            }
            fade=Mathf.Clamp01(age/.08f);
        }
        if(kind==Kind.Stream)
        {
            if(!follow) { Destroy(gameObject); return; }
            Vector3 end=follow.position; Vector3 path=end-startPoint; path.y=0;
            Vector3 sideways=path.sqrMagnitude>.001f?Vector3.Cross(Vector3.up,path.normalized):Vector3.right;
            for(int i=0;i<motes.Count;i++)
            {
                float p=Mathf.Clamp01((age-i*.012f)/.17f), eased=p*p*(3-2*p);
                Vector3 point=Vector3.Lerp(startPoint,end,eased)+Vector3.up*(.12f+Mathf.Sin(p*Mathf.PI)*(.25f+(i%3)*.12f))+sideways*(Mathf.Sin(p*Mathf.PI*2+i)*.18f);
                motes[i].position=point;
                motes[i].rotation=Quaternion.LookRotation(path.sqrMagnitude>.001f?path:Vector3.forward)*Quaternion.Euler(90,0,0);
                motes[i].localScale=p<=0||p>=1?Vector3.zero:new Vector3(.16f,.42f,.16f)*(1-p*.55f);
            }
            fade=1;
        }
        if(kind==Kind.Veil)
        {
            if(!follow) { Destroy(gameObject); return; }
            transform.position=follow.position;
            float breathe=1+Mathf.Sin(age*4)*.07f; fan.localScale=new Vector3(breathe,1,breathe);
            for(int i=0;i<motes.Count;i++)
            {
                // Slow loops from the ground up past the shoulders, thinning out as they rise.
                float cycle=Mathf.Repeat(age*.9f+i*.1f,1);
                float a=i*2.4f+age*1.5f, r=.42f+Mathf.Sin(cycle*Mathf.PI)*.12f;
                motes[i].localPosition=new Vector3(Mathf.Sin(a)*r,Mathf.Lerp(.05f,2f,cycle),Mathf.Cos(a)*r);
                motes[i].localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,12);
                motes[i].localScale=new Vector3(.09f,.26f,.09f)*Mathf.Sin(cycle*Mathf.PI);
            }
            fade=Mathf.Clamp01(age/.2f)*.75f;
        }
        if(kind==Kind.Range)
        {
            if(!follow) { Destroy(gameObject); return; }
            transform.position=follow.position+Vector3.up*.06f;
            fade=Mathf.Clamp01(age/.15f)*(rangeHighlight?1:.5f+Mathf.Sin(age*5)*.08f);
        }
        if(kind==Kind.Link)
        {
            if(!follow || !linkTarget) { Destroy(gameObject); return; }
            Vector3 to=linkTarget.position-follow.position; to.y=0; float distance=to.magnitude;
            transform.position=follow.position+Vector3.up*.07f;
            if(distance>.05f) transform.rotation=Quaternion.LookRotation(to);
            // Chevrons march toward the target and stop short of both bodies.
            const float spacing=.8f; float march=Mathf.Repeat(age*.9f,spacing);
            for(int i=0;i<motes.Count;i++)
            {
                float z=.75f+i*spacing+march; bool shown=z<distance-.65f;
                motes[i].gameObject.SetActive(shown);
                if(shown) motes[i].localPosition=new Vector3(0,0,z);
            }
            extraColor=Color.Lerp(extraColor,rangeHighlight?LinkNear:LinkFar,1-Mathf.Exp(-18*dt));
            Tint(extra,extraColor,Mathf.Clamp01(age/.12f));
        }
        if (kind == Kind.DaggerCloak)
        {
            if (!follow) { Destroy(gameObject); return; }
            transform.SetPositionAndRotation(follow.position, follow.rotation);
            fan.localRotation = Quaternion.Euler(0, age * 430f, 0);
            fan.localScale = Vector3.one * Mathf.Lerp(.75f, 1.2f, Mathf.Clamp01(age / .32f));
            float envelope = Mathf.Lerp(.36f, 1f, Mathf.Sin(Mathf.Clamp01(age / .26f) * Mathf.PI));
            Tint(cyan, DaggerWind, envelope * Mathf.Clamp01((duration - age) / .2f));
        }
        if (kind == Kind.DaggerCut)
        {
            Tint(cyan, DaggerWind, fade * fade);
            if (daggerEdge) Tint(daggerEdge, DaggerEdge, fade);
            if (fan) fan.localRotation = Quaternion.Euler(0, Mathf.Lerp(-38f, 48f, 1f - fade * fade), -12f);
            if (flash)
            {
                flash.localScale = Vector3.one * Mathf.Lerp(.72f, 1.4f, 1f - fade * fade);
                if (age > .17f) flash.gameObject.SetActive(false);
            }
        }
        if (kind == Kind.FireTrail)
        {
            float length = startPoint.z * Mathf.Clamp01(age / windup);
            fan.localPosition = new Vector3(0, .04f, length * .5f);
            fan.localScale = new Vector3(height, 1f, length + height);
            fade = Mathf.Clamp01((duration - age) / .45f);
            Tint(cyan, new Color(1f, .72f, .16f, .92f), fade);
            for (int i = 0; i < motes.Count; i++)
            {
                Vector3 origin = moteOrigins[i];
                if (origin.z > length) continue;
                float wave = .5f + .5f * Mathf.Sin(age * (17f + i % 5) + i * 2.4f);
                motes[i].localPosition = origin + Vector3.up * (.26f * wave);
                motes[i].localRotation = Quaternion.Euler(8f, i * 37f, (wave - .5f) * 35f);
                motes[i].localScale = new Vector3(.19f, .26f + .36f * wave, .18f) * fade;
            }
        }
        if(kind==Kind.Ping) flash.localScale=Vector3.one*Mathf.Lerp(.25f,1,fade);
        if(kind==Kind.Slash) transform.localScale=Vector3.one*(.8f+.3f*(1-fade));
        Tint(dark,ShadowColor,fade); Tint(purple,VioletColor,fade); Tint(bright,WhiteColor,fade);
        Tint(soft,new Color(.45f,.1f,.65f,.18f),fade);
        if(extra) Tint(extra,extraColor,fade);
    }

    static float EaseOutBack(float k)
    {
        float m=k-1;
        return 1+2.7f*m*m*m+1.7f*m*m;
    }

    Material MakeMaterial(Color color)
    {
        Shader shader=Shader.Find("Universal Render Pipeline/Unlit");
        if(!shader) shader=Shader.Find("Sprites/Default");
        var material=new Material(shader);
        material.SetFloat("_Surface",1); material.SetFloat("_Blend",0);
        material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha); material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite",0); material.SetInt("_Cull",(int)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
        if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Color")) material.SetColor("_Color",color);
        ownedMaterials.Add(material); return material;
    }
    static void Tint(Material material,Color color,float fade)
    {
        color.a*=Mathf.Clamp01(fade);
        if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Color")) material.SetColor("_Color",color);
    }
    GameObject MeshPart(string name,Mesh mesh,Material material,Transform parent)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        if(mesh.subMeshCount>1)
        {
            var materials=new Material[mesh.subMeshCount];
            for(int i=0;i<materials.Length;i++) materials[i]=material;
            renderer.sharedMaterials=materials;
        }
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        return go;
    }
    LineRenderer Line(string name,Vector3[] points,float width,Material material,Transform parent)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false); var line=go.AddComponent<LineRenderer>();
        line.useWorldSpace=false; line.positionCount=points.Length; line.SetPositions(points);
        line.widthMultiplier=width; line.sharedMaterial=material; line.numCapVertices=3; line.numCornerVertices=3;
        line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false; return line;
    }
    void Circle(string name,Vector3 center,float radius,float width,Material material,Transform parent,bool vertical)
    {
        var points=new Vector3[33]; for(int i=0;i<points.Length;i++) {float a=i*Mathf.PI*2/(points.Length-1);points[i]=center+(vertical?new Vector3(Mathf.Sin(a),Mathf.Cos(a),0):new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)))*radius;}
        Line(name,points,width,material,parent);
    }
    void Arc(string name,float radius,float angle,float width,Material material,Transform parent)
    {
        var points=new Vector3[25]; for(int i=0;i<points.Length;i++) {float a=Mathf.Lerp(-angle*.5f,angle*.5f,i/(float)(points.Length-1))*Mathf.Deg2Rad;points[i]=new Vector3(Mathf.Sin(a)*radius,Mathf.Sin(a)*.13f,Mathf.Cos(a)*radius);}
        Line(name,points,width,material,parent);
    }
    Transform ScissorHalf(string name,int side)
    {
        var pivot=new GameObject(name).transform; pivot.SetParent(transform,false);
        var mesh=new Mesh(); mesh.vertices=new[]{new Vector3(-.10f,0,-.10f),new Vector3(.15f,0,.25f),new Vector3(0,0,DanielSimulator.QRadius),new Vector3(-.24f,0,.55f)};
        mesh.triangles=new[]{0,2,1,0,3,2}; mesh.RecalculateNormals(); ownedMeshes.Add(mesh);
        MeshPart("Blade",mesh,dark,pivot);
        Line("Blade edge",new[]{new Vector3(.15f,.025f,.25f),new Vector3(0,.025f,DanielSimulator.QRadius)},.035f,purple,pivot);
        Circle("Finger loop",new Vector3(side*.17f,0,-.48f),.27f,.10f,dark,pivot,false);
        Line("Handle neck",new[]{new Vector3(side*.15f,0,-.32f),Vector3.zero},.10f,dark,pivot);
        return pivot;
    }
    Transform VerticalScissorHalf(string name,int side)
    {
        var pivot=new GameObject(name).transform; pivot.SetParent(transform,false);
        var mesh=new Mesh();
        mesh.vertices=new[]{new Vector3(-.075f,-.1f,0),new Vector3(.11f,.15f,0),new Vector3(0,1.05f,0),new Vector3(-.14f,.25f,0)};
        mesh.triangles=new[]{0,1,2,0,2,3}; mesh.RecalculateNormals(); ownedMeshes.Add(mesh);
        MeshPart("Shadow blade",mesh,dark,pivot);
        Line("Violet blade edge",new[]{new Vector3(.11f,.15f,-.02f),new Vector3(0,1.05f,-.02f)},.045f,purple,pivot);
        Circle("Scissor handle",new Vector3(side*.10f,-.36f,0),.19f,.075f,purple,pivot,true);
        Line("Handle neck",new[]{new Vector3(side*.1f,-.2f,0),Vector3.zero},.075f,dark,pivot);
        return pivot;
    }
    Mesh SectorMesh(float radius,float angle)
    {
        const int steps=40; var verts=new Vector3[steps+2]; var triangles=new int[steps*3];
        for(int i=0;i<=steps;i++) {float a=Mathf.Lerp(-angle*.5f,angle*.5f,i/(float)steps)*Mathf.Deg2Rad;verts[i+1]=new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius);}
        for(int i=0;i<steps;i++) {triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
        var mesh=new Mesh();mesh.vertices=verts;mesh.triangles=triangles;mesh.RecalculateNormals();ownedMeshes.Add(mesh);return mesh;
    }
    /// <summary>Filled disc in the XY plane, for camera-facing glows.</summary>
    Mesh DiscXY(float radius)
    {
        const int steps=36; var verts=new Vector3[steps+2]; var triangles=new int[steps*3];
        for(int i=0;i<=steps;i++) {float a=i*Mathf.PI*2/steps;verts[i+1]=new Vector3(Mathf.Sin(a)*radius,Mathf.Cos(a)*radius,0);}
        for(int i=0;i<steps;i++) {triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
        var mesh=new Mesh();mesh.vertices=verts;mesh.triangles=triangles;mesh.RecalculateNormals();ownedMeshes.Add(mesh);return mesh;
    }
    /// <summary>Crescent in the XY plane: a disc with a smaller disc, shifted toward +x, cut out of it.</summary>
    Mesh CrescentMesh(float radius,float cutRadius,float cutOffset)
    {
        const int steps=28;
        float x=(radius*radius-cutRadius*cutRadius+cutOffset*cutOffset)/(2*cutOffset), y=Mathf.Sqrt(Mathf.Max(0,radius*radius-x*x));
        float outer=Mathf.Atan2(y,x), inner=Mathf.Atan2(y,x-cutOffset);
        var verts=new Vector3[(steps+1)*2]; var triangles=new int[steps*6];
        for(int i=0;i<=steps;i++)
        {
            float k=i/(float)steps, a=Mathf.Lerp(outer,Mathf.PI*2-outer,k), b=Mathf.Lerp(inner,Mathf.PI*2-inner,k);
            verts[i*2]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;
            verts[i*2+1]=new Vector3(cutOffset+Mathf.Cos(b)*cutRadius,Mathf.Sin(b)*cutRadius,0);
        }
        for(int i=0;i<steps;i++) {int v=i*2,t=i*6;triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;}
        var mesh=new Mesh();mesh.vertices=verts;mesh.triangles=triangles;mesh.RecalculateNormals();ownedMeshes.Add(mesh);return mesh;
    }
    Mesh Octahedron()
    {
        var mesh=new Mesh();mesh.vertices=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
        mesh.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};mesh.RecalculateNormals();ownedMeshes.Add(mesh);return mesh;
    }
    void OnDestroy()
    {
        foreach(var material in ownedMaterials) if(material) Destroy(material);
        foreach(var mesh in ownedMeshes) if(mesh) Destroy(mesh);
    }
}

