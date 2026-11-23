using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Hand-authored skeletal motion based on the supplied Daniel reference videos.
/// Targets the imported Y Bot skeleton directly, without requiring a Humanoid Avatar.
/// All motion stays in the skeleton: the simulator owns translation and heading.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class DanielCharacterAnimation : MonoBehaviour
{
    struct Rest { public Transform bone; public Vector3 position; public Quaternion rotation; }
    readonly List<Rest> rest = new List<Rest>();
    readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    Transform hips, spine, chest, head;
    Transform lu, ll, lh, ru, rl, rh, lt, lc, lf, rt, rc, rf;
    Vector3 leftFootHome, rightFootHome;
    Renderer[] renderers;
    Material[][] normalMaterials, ghostMaterials;
    readonly List<Material> ownedMaterials = new List<Material>();
    readonly List<Mesh> ownedMeshes = new List<Mesh>();
    float speed, gait, idleTime, age, actionDuration, stealthAge, unit = 1, moveBlend, cadence = RunCadence;
    float strideSpeed = RunSpeed;
    // Forward run loop from the 6-pose sheet: one full cycle (two steps) every 0.73 s at full speed.
    const float RunPeriod = .73f, RunCadence = 2 * Mathf.PI / RunPeriod, RunSpeed = 4.4f, StanceShare = .39f;
    string action = "Idle";
    bool initialized, stealth;
    GameObject weapon;
    public string CurrentAction { get { return action; } }
    public bool HasValidRig { get { return hips && lu && ru && lt && rt; } }
    public event Action<bool> Footstep;

    void Awake() { Initialize(); }
    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        foreach (var animator in GetComponentsInChildren<Animator>()) animator.enabled = false;
        foreach (var animation in GetComponentsInChildren<Animation>()) animation.enabled = false;
        foreach (var bone in GetComponentsInChildren<Transform>())
        {
            if (bone == transform) continue;
            string key = bone.name.Replace("mixamorig1:", "").Replace("mixamorig:", "").Replace("mixamorig", "");
            if (!bones.ContainsKey(key)) bones.Add(key, bone);
            rest.Add(new Rest { bone = bone, position = bone.localPosition, rotation = bone.localRotation });
        }
        hips = Bone("Hips"); spine = Bone("Spine"); chest = Bone("Spine2"); head = Bone("Head");
        lu = Bone("LeftArm"); ll = Bone("LeftForeArm"); lh = Bone("LeftHand");
        ru = Bone("RightArm"); rl = Bone("RightForeArm"); rh = Bone("RightHand");
        lt = Bone("LeftUpLeg"); lc = Bone("LeftLeg"); lf = Bone("LeftFoot");
        rt = Bone("RightUpLeg"); rc = Bone("RightLeg"); rf = Bone("RightFoot");
        if (lf) leftFootHome = transform.InverseTransformPoint(lf.position);
        if (rf) rightFootHome = transform.InverseTransformPoint(rf.position);
        // Key poses are authored for a 0.9 m leg; rescale them to whatever units this rig imports in.
        if (lt && lf) unit = Mathf.Max(.0001f, transform.InverseTransformVector(lf.position - lt.position).magnitude / .9f);
        BuildScissors();
        renderers = GetComponentsInChildren<Renderer>();
        normalMaterials = new Material[renderers.Length][];
        ghostMaterials = new Material[renderers.Length][];
        for (int r = 0; r < renderers.Length; r++)
        {
            normalMaterials[r] = renderers[r].sharedMaterials;
            ghostMaterials[r] = new Material[normalMaterials[r].Length];
            for (int i = 0; i < normalMaterials[r].Length; i++)
            {
                Material source = normalMaterials[r][i];
                var ghost = new Material(source ? source.shader : Shader.Find("Universal Render Pipeline/Lit"));
                if (source) ghost.CopyPropertiesFromMaterial(source);
                ghost.SetFloat("_Surface", 1); ghost.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                ghost.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); ghost.SetFloat("_ZWrite", 0);
                ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); ghost.renderQueue = (int)RenderQueue.Transparent;
                ghost.SetOverrideTag("RenderType", "Transparent");
                if (ghost.IsKeywordEnabled("_ALPHATEST_ON")) ghost.SetFloat("_Cutoff", .05f);
                ghost.SetColor("_BaseColor", new Color(.35f, .19f, .65f, .26f));
                ghostMaterials[r][i] = ghost; ownedMaterials.Add(ghost);
            }
        }
        Evaluate("Idle", 0, 0);
    }
    Transform Bone(string name)
    {
        Transform bone; if (bones.TryGetValue(name, out bone)) return bone;
        foreach (var pair in bones) if (pair.Key.EndsWith(name, StringComparison.Ordinal)) return pair.Value;
        return null;
    }
    public void SetMoving(float metersPerSecond) { speed = Mathf.Max(0, metersPerSecond); }
    public void SetMoving(bool moving) { SetMoving(moving ? 4.4f : 0); }
    void Play(string name, float duration) { action = name; age = 0; actionDuration = duration; }
    void Play(string name) { Play(name, Clips[name].duration); }
    public void PlayDash(float travelDuration) => Play("Dash", travelDuration);
    public void LandDash()
    {
        if (action == "Dash") Play("DashLand");
    }
    public void PlayAttack(bool empowered = false, float speedMultiplier = 1f)
    {
        float duration = empowered ? Clips["Empowered"].duration : .38f;
        Play(empowered ? "Empowered" : "Attack", duration / speedMultiplier);
    }
    public void PlayUltimateEnter() { Play("Enter", .2f); }
    /// <summary>R folds into the shadow on the same frame the simulator hides the model.</summary>
    public void PlayUltimateLatch(float windupDuration) => Play("Latch", windupDuration);
    public void PlayUltimateExit(float duration = .3f) { SetHidden(false); Play("Exit", duration); }
    public void CancelUltimateExit() { if (action == "Exit") Play("Idle", 0); }

    MaterialPropertyBlock untargetableTint;
    bool untargetableTintActive;
    public void SetUntargetableTint(bool value)
    {
        if (untargetableTintActive == value) return;
        Initialize();
        if (untargetableTint == null) untargetableTint = new MaterialPropertyBlock();
        untargetableTintActive = value;
        for (int r = 0; r < renderers.Length; r++)
        {
            if (!renderers[r]) continue;
            var materials = renderers[r].sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                if (!value) { renderers[r].SetPropertyBlock(null, m); continue; }
                var material = materials[m];
                if (!material || !material.HasProperty("_BaseColor")) continue;
                Color color = material.GetColor("_BaseColor");
                untargetableTint.SetColor("_BaseColor", new Color(color.r * .38f, color.g * .38f, color.b * .48f, color.a));
                renderers[r].SetPropertyBlock(untargetableTint, m);
            }
        }
    }
    public void SetStealth(bool value)
    {
        Initialize(); if (stealth == value) return; stealth = value; stealthAge = 0;
        bool restoreTint = untargetableTintActive;
        if (restoreTint) SetUntargetableTint(false);
        for (int r = 0; r < renderers.Length; r++) if (renderers[r])
        {
            renderers[r].sharedMaterials = value ? ghostMaterials[r] : normalMaterials[r];
            renderers[r].shadowCastingMode = value ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
        if (restoreTint) SetUntargetableTint(true);
    }
    public void SetHidden(bool value)
    {
        Initialize();
        foreach (var renderer in renderers) if (renderer) renderer.enabled = !value;
    }
    void LateUpdate() { Sample(Time.deltaTime); }
    public void Sample(float deltaTime)
    {
        Initialize();
        float dt = Mathf.Max(0, deltaTime);
        bool moving = speed > .05f;
        if (moving)
        {
            strideSpeed = speed;
            cadence = RunCadence * Mathf.Clamp(speed / RunSpeed, .6f, 1.5f);
        }
        idleTime += dt;
        int previousContact = Mathf.FloorToInt(gait / Mathf.PI);
        gait += dt * (moving || moveBlend > .01f ? cadence : 1.6f);
        float targetBlend = moving && action != "Dash" && action != "Latch" ? 1 : 0;
        moveBlend = Mathf.MoveTowards(moveBlend, targetBlend, dt * (targetBlend > moveBlend ? 6f : 8f));
        age += dt;
        if (stealth)
        {
            // Dissolve into the translucent look, then shimmer while hidden.
            stealthAge += dt;
            float alpha = Mathf.Lerp(.85f, .2f, Mathf.Clamp01(stealthAge / .25f)) + Mathf.Sin(stealthAge * 6) * .05f;
            var tint = new Color(.35f, .19f, .65f, alpha);
            foreach (var set in ghostMaterials) foreach (var material in set) if (material) material.SetColor("_BaseColor", tint);
        }
        if (action == "Enter" && age >= .12f) SetHidden(true);
        if (age >= actionDuration && action != "Dash" && action != "Latch") action = "Idle";
        Evaluate(action, actionDuration > 0 ? age / actionDuration : 0, gait);
        int contact = Mathf.FloorToInt(gait / Mathf.PI);
        bool inFootstepPose = action != "Dash" && action != "Latch" &&
            (action != "DashLand" || age >= actionDuration * .75f);
        if (moving && inFootstepPose && moveBlend >= .5f && contact != previousContact)
            Footstep?.Invoke((contact & 1) == 0);
    }
    /// <summary>Deterministic sampling for render QA or an offline animation baker.</summary>
    public void PreviewPose(string state, float normalizedTime)
    {
        Initialize();
        float previous = speed, previousBlend = moveBlend, previousCadence = cadence;
        float previousIdle = idleTime, previousStrideSpeed = strideSpeed;
        speed = state == "Walk" ? RunSpeed : 0; moveBlend = state == "Walk" ? 1 : 0; cadence = RunCadence;
        idleTime = normalizedTime * 3.4f;
        strideSpeed = RunSpeed;
        Evaluate(state == "Walk" ? "Idle" : state, Mathf.Clamp01(normalizedTime), normalizedTime * Mathf.PI * 2);
        speed = previous; moveBlend = previousBlend; cadence = previousCadence;
        idleTime = previousIdle; strideSpeed = previousStrideSpeed;
    }
    void Evaluate(string state, float time, float phase)
    {
        foreach (var pose in rest) { pose.bone.localPosition = pose.position; pose.bone.localRotation = pose.rotation; }
        if (!HasValidRig) return;
        float walk = moveBlend;
        if (state == "Dash" || state == "Latch")
            walk *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .25f, time));
        if (state == "DashLand") walk *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 1, time));
        float stride = Mathf.Sin(phase);
        float breath = Mathf.Sin(idleTime * 2 * Mathf.PI / 3.4f);
        float idleWeight = 1 - walk;
        // cycle 0 = left foot contact (sheet 01), .5 = right foot contact (sheet 04).
        float cycle = Mathf.Repeat(phase / (2 * Mathf.PI), 1), armSwing = -Mathf.Cos(cycle * 2 * Mathf.PI);
        // Locomotion silhouette: every skill clip starts from and returns to this pose.
        // Running: torso pitched forward, right elbow folded low with the forearm out front,
        // left elbow folded and pumping a short arc opposite the left leg.
        Pose p = new Pose
        {
            lean = Mathf.Lerp(5 + breath * 1.3f, 27, walk),
            twist = 4 * Mathf.Cos(cycle * 2 * Mathf.PI) * walk + breath * 1.5f * idleWeight,
            roll = breath * .9f * idleWeight,
            root = V(breath * .012f, -.018f, 0),
            lU = Vector3.Lerp(IdleLU, new Vector3(-.22f, -.85f, -.2f + .22f * armSwing), walk),
            lL = Vector3.Lerp(IdleLL, new Vector3(.25f, -.3f + .1f * armSwing, .9f), walk),
            rU = Vector3.Lerp(IdleRU, new Vector3(.2f, -.9f, -.28f - .05f * armSwing), walk),
            rL = Vector3.Lerp(IdleRL, new Vector3(-.06f, -.1f, .99f), walk)
        };
        Clip clip;
        if (Clips.TryGetValue(state, out clip)) p = clip.Sample(Mathf.Clamp01(time) * clip.duration, p);
        else if (state == "Enter")
        {
            float envelope = Mathf.Sin(Mathf.Clamp01(time) * Mathf.PI);
            p.lean = 36; p.root.y = -.12f * envelope;
            p.lU = new Vector3(-.3f, -.25f, -.9f); p.lL = new Vector3(-.1f, .2f, -.9f);
            p.rU = new Vector3(.2f, -.35f, -.9f); p.rL = new Vector3(.1f, -.1f, -.9f);
        }
        else if (state == "Attack")
        {
            float envelope = Mathf.Sin(Mathf.Clamp01(time) * Mathf.PI);
            float swing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .72f, time));
            // Poses return gently to the locomotion silhouette rather than snapping back.
            float fade = Mathf.SmoothStep(1, 0, Mathf.InverseLerp(.78f, 1, time));
            p.rU = Vector3.Lerp(p.rU, Vector3.Lerp(new Vector3(.7f, .3f, -.25f), new Vector3(-.6f, -.45f, .65f), swing), fade);
            p.rL = Vector3.Lerp(p.rL, Vector3.Lerp(new Vector3(.2f, .65f, -.7f), new Vector3(-.8f, -.1f, .7f), swing), fade);
            p.twist = Mathf.Lerp(-28, 35, swing) * envelope;
            p.lean += 8 * envelope;
            p.root.y = -.04f * envelope;
        }
        // While walking the gait owns the legs, so a cast keeps playing on the upper body.
        float planted = 1 - walk;
        Vector3 root = new Vector3(p.root.x * planted, p.root.y * (1 - walk * .6f), p.root.z * planted) * unit;
        // Hips sink onto each contact and rise through the push-off.
        root.y += (.012f * breath * idleWeight - (.027f + .022f * Mathf.Cos(cycle * 4 * Mathf.PI)) * walk) * unit;
        // The run carries the whole trunk forward from the hips, matching the sheet's waist angle.
        float toePitch = p.pitch * planted, pitch = toePitch + 16 * walk;
        hips.position += transform.TransformVector(root);
        RotateInModel(hips, new Vector3(pitch, p.twist * .3f, 0));
        RotateInModel(spine, new Vector3(p.lean * .55f, p.twist * .3f, p.roll * .5f + stride * 2 * walk));
        RotateInModel(chest, new Vector3(p.lean * .45f, p.twist * .4f, p.roll * .5f));
        // Chin up while running so the gaze stays level along the heading.
        RotateInModel(head, new Vector3(-p.lean * .55f - toePitch * .6f - 26 * walk, -p.twist * .4f, 0));
        Aim(lu, ll, p.lU); Aim(ll, lh, p.lL);
        Aim(ru, rl, p.rU); Aim(rl, rh, p.rL);
        Vector3 leftStep, rightStep; float leftToe, rightToe;
        GaitFoot(cycle, out leftStep, out leftToe); GaitFoot(Mathf.Repeat(cycle + .5f, 1), out rightStep, out rightToe);
        PlaceLeg(lt, lc, lf, leftFootHome, p.lF * (planted * unit) + leftStep * walk, toePitch + leftToe * walk);
        PlaceLeg(rt, rc, rf, rightFootHome, p.rF * (planted * unit) + rightStep * walk, toePitch + rightToe * walk);
        if (weapon && rh)
        {
            weapon.transform.position = rh.position;
            Vector3 axis = (rh.position - rl.position).normalized;
            // While running the scissor tips are held up and forward.
            weapon.transform.rotation = Quaternion.LookRotation((axis + transform.forward * .55f + transform.up * (1.1f * walk)).normalized, transform.up);
        }
    }
    /// <summary>
    /// One foot of the run loop in model space, u = 0 at its contact. During stance the foot travels straight back
    /// at exactly the character's ground speed, so the planted foot does not slide; it then folds up behind,
    /// is carried under the body and reaches forward for the next contact.
    /// </summary>
    void GaitFoot(float u, out Vector3 offset, out float toePitch)
    {
        float period = 2 * Mathf.PI / Mathf.Max(.01f, cadence);
        float reach = strideSpeed * StanceShare * period * .5f / Mathf.Max(.0001f, transform.lossyScale.z);
        reach = Mathf.Min(reach > 0 ? reach : .42f * unit, .46f * unit);
        if (u < StanceShare)
        {
            float k = u / StanceShare;
            offset = new Vector3(0, 0, Mathf.Lerp(reach, -reach, k));
            // Heel strike, flat through support, heel peels up for the push-off.
            toePitch = k < .2f ? Mathf.Lerp(-12, 0, k / .2f) : Mathf.Lerp(0, 28, Mathf.InverseLerp(.65f, 1, k));
        }
        else
        {
            float v = (u - StanceShare) / (1 - StanceShare);
            float carry = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, 1, v));
            offset = new Vector3(0, .235f * unit * Mathf.Sin(Mathf.PI * Mathf.Pow(v, .8f)), Mathf.Lerp(-reach, reach, carry));
            toePitch = v < .25f ? Mathf.Lerp(28, 44, v / .25f) : Mathf.Lerp(44, -12, Mathf.InverseLerp(.25f, 1, v));
        }
    }

    // ---- Key poses traced from the 48 mannequin reference frames (Q/W/E/R, 12 each) ----
    // Model space: +x right, +y up, +z forward. Arm vectors are bone directions; root/feet are metres on a 0.9 m leg.
    struct Pose
    {
        public float lean, twist, roll, pitch;   // spine bend, yaw, side bend, whole-body pitch (degrees)
        public Vector3 root, lU, lL, rU, rL, lF, rF; // hip offset, arm directions, foot offsets
        public static Pose Blend(Pose a, Pose b, float t)
        {
            return new Pose
            {
                lean = Mathf.Lerp(a.lean, b.lean, t), twist = Mathf.Lerp(a.twist, b.twist, t),
                roll = Mathf.Lerp(a.roll, b.roll, t), pitch = Mathf.Lerp(a.pitch, b.pitch, t),
                root = Vector3.Lerp(a.root, b.root, t), lF = Vector3.Lerp(a.lF, b.lF, t), rF = Vector3.Lerp(a.rF, b.rF, t),
                lU = Vector3.Slerp(a.lU.normalized, b.lU.normalized, t), lL = Vector3.Slerp(a.lL.normalized, b.lL.normalized, t),
                rU = Vector3.Slerp(a.rU.normalized, b.rU.normalized, t), rL = Vector3.Slerp(a.rL.normalized, b.rL.normalized, t)
            };
        }
    }
    struct Key { public float time; public Pose pose; public bool rest; }
    sealed class Clip
    {
        public float duration; public Key[] keys;
        public Pose Sample(float seconds, Pose rest)
        {
            Key a = keys[0], b = keys[keys.Length - 1];
            for (int i = 0; i < keys.Length - 1; i++)
                if (seconds < keys[i + 1].time) { a = keys[i]; b = keys[i + 1]; break; } else a = keys[i + 1];
            float t = b.time > a.time ? Mathf.Clamp01((seconds - a.time) / (b.time - a.time)) : 1;
            t = Mathf.Lerp(t, t * t * (3 - 2 * t), .85f);
            return Pose.Blend(a.rest ? rest : a.pose, b.rest ? rest : b.pose, t);
        }
    }
    static readonly Vector3 IdleLU = new Vector3(-.18f, -.78f, .28f), IdleLL = new Vector3(.02f, -.15f, .9f);
    static readonly Vector3 IdleRU = new Vector3(.13f, -.98f, -.10f), IdleRL = new Vector3(.04f, -.70f, .48f);
    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    static Key Base(float time) { return new Key { time = time, rest = true }; }
    static Key K(float time, float lean, float twist, float roll, float pitch, Vector3 root,
        Vector3 lU, Vector3 lL, Vector3 rU, Vector3 rL, Vector3 lF, Vector3 rF)
    {
        return new Key { time = time, pose = new Pose { lean = lean, twist = twist, roll = roll, pitch = pitch, root = root, lU = lU, lL = lL, rU = rU, rL = rL, lF = lF, rF = rF } };
    }
    static Clip Make(params Key[] keys) { return new Clip { keys = keys, duration = keys[keys.Length - 1].time }; }
    static readonly Vector3 O = Vector3.zero;
    // Wide-stance "ready" silhouette shared by frame 01/12 of every sheet.
    static readonly Vector3 HangLU = V(-.35f, -.9f, .1f), HangLL = V(-.15f, -.95f, .25f), HangRU = V(.35f, -.9f, .1f), HangRL = V(.15f, -.95f, .25f);
    static readonly Vector3 WideL = V(-.08f, 0, 0), WideR = V(.08f, 0, 0);
    static Key DashContact(float time) => K(time, 34, -9, -5, 15,
        V(-.025f, -.16f, .075f), V(-.42f, -.72f, .4f), V(.08f, -.52f, .85f),
        V(.24f, -.68f, -.65f), V(.1f, -.48f, .86f), V(-.025f, 0, .31f), V(.045f, 0, -.24f));
    static readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>
    {
        // Q and W have no cast animation: the character keeps its current idle/walk pose.
        // Travel ends on contact. The simulator starts the absorption and recovery at that point.
        { "Dash", Make(
            Base(0),
            K(.08f, 31, -12, -3, 24, V(0, -.15f, .09f), V(-.3f, -.55f, .7f), V(.08f, -.32f, .94f), V(.25f, -.4f, -.85f), V(.12f, -.25f, .96f), V(0, .05f, .13f), V(.025f, .12f, -.27f)),
            K(.17f, 33, -7, -3, 32, V(0, -.18f, .13f), V(-.35f, -.65f, .48f), V(.08f, -.40f, .90f), V(.24f, -.25f, -.94f), V(.1f, -.18f, .98f), V(0, .12f, .23f), V(.04f, .23f, -.34f)),
            DashContact(.28f)) },
        { "DashLand", Make(
            DashContact(0),
            K(.085f, 27, -8, -4, 10, V(-.035f, -.23f, .10f), V(-.33f, -.72f, .49f), V(.10f, -.58f, .80f), V(.22f, -.88f, -.23f), V(.02f, -.35f, .94f), V(-.025f, 0, .31f), V(.045f, 0, -.24f)),
            K(.21f, 14, -4, -2, 4, V(-.015f, -.08f, .08f), IdleLU, IdleLL, IdleRU, IdleRL, V(-.025f, 0, .25f), V(.035f, .07f, -.08f)),
            Base(.36f)) },
        // E sheet 08-12: the empowered attack reappears crouched beside the target and cuts sideways.
        { "Empowered", Make(
            K(0, 40, -30, 0, 0, V(0, -.5f, 0), V(-.35f, -.7f, .5f), V(.1f, -.9f, .3f), V(-.6f, -.2f, .6f), V(-.8f, .1f, .3f), WideL, WideR),       // E08
            K(.16f, 30, 35, 0, 0, V(0, -.4f, .1f), V(-.4f, -.7f, -.2f), V(.2f, -.6f, .5f), V(.85f, -.05f, .4f), V(.95f, 0, .1f), V(-.1f, 0, .25f), V(.35f, 0, -.25f)), // E09
            K(.38f, 35, 42, 0, 0, V(0, -.45f, .1f), V(-.4f, -.7f, -.1f), V(.2f, -.7f, .5f), V(.8f, -.3f, .1f), V(.9f, -.3f, -.2f), V(-.1f, 0, .25f), V(.35f, 0, -.25f)), // E10
            K(.60f, 20, 10, 0, 0, V(0, -.2f, 0), HangLU, HangLL, HangRU, HangRL, V(-.1f, 0, .1f), V(.15f, 0, -.1f)),                              // E11
            Base(.85f)) },
        // Coil into a short forward pull, then tuck into the shadow at the end of the windup.
        { "Latch", Make(
            Base(0),
            K(.11f, 14, -24, -5, 5, V(.025f, -.07f, -.035f), V(-.35f, -.64f, .59f), V(.15f, -.35f, .94f), V(.48f, -.47f, -.74f), V(.08f, .38f, .92f), WideL, WideR),
            K(.24f, 29, 15, 3, 14, V(0, -.12f, .09f), V(-.53f, -.52f, -.55f), V(-.16f, -.50f, .84f), V(.15f, -.44f, .90f), V(-.26f, -.19f, .94f), V(-.045f, 0, .13f), WideR),
            K(.37f, 36, 8, 2, 15, V(0, -.23f, .09f), V(-.27f, -.57f, .73f), V(.32f, -.26f, .91f), V(.27f, -.61f, .73f), V(-.38f, -.20f, .90f), V(-.045f, 0, .13f), WideR),
            K(.50f, 48, 2, 0, 16, V(0, -.36f, .12f), V(-.22f, -.74f, .64f), V(.42f, -.12f, .90f), V(.23f, -.75f, .63f), V(-.40f, -.13f, .90f), V(-.045f, 0, .13f), WideR)) },
        // R sheet 09-12: burst out low, cut sideways, sink into the wide finishing cut, then stand.
        { "Exit", Make(
            K(0, 25, -30, 0, 0, V(0, -.35f, 0), V(-.4f, -.7f, .3f), V(-.1f, -.6f, .7f), V(-.5f, .2f, .7f), V(-.8f, .3f, .3f), V(-.15f, 0, .05f), V(.15f, 0, -.05f)), // R09
            K(.10f, 40, 30, 0, 0, V(0, -.5f, .05f), V(-.6f, -.1f, -.5f), V(-.8f, 0, -.4f), V(.6f, -.35f, .6f), V(.85f, -.3f, .3f), V(-.1f, 0, .2f), V(.2f, 0, -.3f)), // R10
            K(.22f, 50, 45, 0, 0, V(0, -.6f, .05f), V(-.8f, .1f, .3f), V(-.95f, .1f, .2f), V(.85f, -.2f, -.1f), V(.95f, -.15f, -.3f), V(-.2f, 0, .25f), V(.25f, 0, -.35f)), // R11
            K(.34f, 48, 42, 0, 0, V(0, -.58f, .05f), V(-.8f, .05f, .3f), V(-.95f, .05f, .2f), V(.85f, -.25f, -.15f), V(.95f, -.2f, -.3f), V(-.2f, 0, .25f), V(.25f, 0, -.35f)),
            K(.48f, 20, 10, 0, 0, V(0, -.2f, 0), HangLU, HangLL, HangRU, HangRL, V(-.1f, 0, .1f), V(.12f, 0, -.1f)),                              // R12
            Base(.62f)) },
    };
    void RotateInModel(Transform bone, Vector3 degrees)
    {
        if (!bone) return;
        bone.rotation = transform.rotation * Quaternion.Euler(degrees) * Quaternion.Inverse(transform.rotation) * bone.rotation;
    }
    void Aim(Transform bone, Transform child, Vector3 direction)
    {
        if (!bone || !child) return;
        bone.rotation = Quaternion.FromToRotation(child.position - bone.position, transform.TransformDirection(direction.normalized)) * bone.rotation;
    }
    void PlaceLeg(Transform thigh, Transform knee, Transform foot, Vector3 home, Vector3 offset, float pitch)
    {
        if (!thigh || !knee || !foot) return;
        Vector3 target = home + offset;
        // Small spread maintains the reference's balanced idle stance.
        target.x += Mathf.Sign(home.x) * .025f;
        Vector3 destination = transform.TransformPoint(target);
        float upper = Vector3.Distance(thigh.position, knee.position), lower = Vector3.Distance(knee.position, foot.position);
        Vector3 vector = destination - thigh.position;
        float distance = Mathf.Clamp(vector.magnitude, .001f, upper + lower - .001f);
        Vector3 axis = vector.normalized;
        Vector3 pole = Vector3.ProjectOnPlane(transform.forward, axis).normalized;
        float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
        float height = Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
        Vector3 bend = thigh.position + axis * along + pole * height;
        thigh.rotation = Quaternion.FromToRotation(knee.position - thigh.position, bend - thigh.position) * thigh.rotation;
        knee.rotation = Quaternion.FromToRotation(foot.position - knee.position, destination - knee.position) * knee.rotation;
        // Cancel ankle twist from leg swing using the imported rest direction.
        Transform toe = foot.childCount > 0 ? foot.GetChild(0) : null;
        if (toe) foot.rotation = Quaternion.FromToRotation(toe.position - foot.position, Quaternion.AngleAxis(pitch, transform.right) * (transform.forward + transform.up * -.12f)) * foot.rotation;
    }
    void BuildScissors()
    {
        if (!rh) return;
        weapon = new GameObject("Daniel held scissors"); weapon.transform.SetParent(transform);
        Material steel = new Material(Shader.Find("Universal Render Pipeline/Lit")); steel.color = new Color(.48f,.49f,.58f); steel.SetFloat("_Metallic", .65f); steel.SetFloat("_Smoothness", .65f); ownedMaterials.Add(steel);
        Material grip = new Material(steel); grip.color = new Color(.12f,.09f,.18f); grip.SetFloat("_Metallic", .25f); ownedMaterials.Add(grip);
        var bladeMesh = new Mesh { name = "Tapered scissor blade" };
        bladeMesh.vertices = new[] {
            new Vector3(-.016f,-.006f,0), new Vector3(.016f,-.006f,0), new Vector3(.012f,-.006f,.27f), new Vector3(0,-.006f,.35f),
            new Vector3(-.016f,.006f,0), new Vector3(.016f,.006f,0), new Vector3(.012f,.006f,.27f), new Vector3(0,.006f,.35f)
        };
        bladeMesh.triangles = new[] { 0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0 };
        bladeMesh.RecalculateNormals(); bladeMesh.RecalculateBounds(); ownedMeshes.Add(bladeMesh);
        var ringMesh = MakeHandleRing(); ownedMeshes.Add(ringMesh);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1 : 1;
            var blade = MeshPart("Scissor blade", bladeMesh, steel);
            blade.transform.localPosition = new Vector3(side * .008f, side * .004f, 0);
            blade.transform.localRotation = Quaternion.Euler(0, side * 8, 0);
            var handle = MeshPart("Scissor handle", ringMesh, grip);
            handle.transform.localPosition = new Vector3(side * .04f, 0, -.058f);
            handle.transform.localRotation = Quaternion.Euler(0, -side * 12, 0);
        }
        weapon.transform.localScale = Vector3.one;
    }
    GameObject MeshPart(string name, Mesh mesh, Material material)
    {
        var part = new GameObject(name); part.transform.SetParent(weapon.transform, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material; return part;
    }
    Mesh MakeHandleRing()
    {
        const int segments = 20, sides = 6;
        var vertices = new Vector3[segments * sides]; var triangles = new int[segments * sides * 6];
        for (int i = 0; i < segments; i++) for (int j = 0; j < sides; j++)
        {
            float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / sides;
            float rim = .007f * Mathf.Cos(b);
            vertices[i * sides + j] = new Vector3(Mathf.Cos(a) * (.031f + rim), Mathf.Sin(b) * .007f, Mathf.Sin(a) * (.045f + rim));
            int k = (i * sides + j) * 6, p = i * sides + j, q = ((i + 1) % segments) * sides + j;
            int pn = i * sides + (j + 1) % sides, qn = ((i + 1) % segments) * sides + (j + 1) % sides;
            triangles[k] = p; triangles[k + 1] = q; triangles[k + 2] = pn;
            triangles[k + 3] = pn; triangles[k + 4] = q; triangles[k + 5] = qn;
        }
        var mesh = new Mesh { name = "Scissor handle ring", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    void OnDestroy()
    {
        foreach (var material in ownedMaterials) if (material) { if(Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        foreach (var mesh in ownedMeshes) if (mesh) { if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
    }
}
