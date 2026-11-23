using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Original character design layered onto the Y Bot skeleton at runtime: a masked "shadow tailor".
/// Hooded cowl with a pale mask and violet eye slits, asymmetric capelet, long split coat with
/// violet lining, thread spools on the belt and two oversized needles crossed on the back.
/// Every piece is a low-poly mesh generated here and parented to a bone, so the existing
/// hand-authored animation keeps driving it. Add it before DanielCharacterAnimation so the
/// stealth and hide logic picks the pieces up with the body.
/// </summary>
public sealed class DanielOutfit : MonoBehaviour
{
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    readonly List<Mesh> owned = new List<Mesh>();
    readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<int> tris = new List<int>();
    Vector3 outwardFrom;
    float u; // one metre on a 1.8 m figure, in model-local units

    public static void Apply(Transform model)
    {
        if (!model || model.GetComponent<DanielOutfit>()) return;
        model.gameObject.AddComponent<DanielOutfit>().Build();
    }

    /// <summary>
    /// Sets the intended look on a material of the modelled character, matched by the material's name.
    /// Colours are fixed here so they do not depend on how the FBX importer converted them.
    /// </summary>
    public static void Paint(Material material)
    {
        if (!material) return;
        string n = material.name;
        Color color; float metallic = 0, smoothness = .3f; Color emission = Color.black;
        if (n.Contains("Outfit_Coat")) { color = new Color(.085f, .085f, .11f); smoothness = .25f; }
        else if (n.Contains("Outfit_Lining")) { color = new Color(.40f, .15f, .60f); smoothness = .35f; }
        else if (n.Contains("Outfit_Trim")) { color = new Color(.62f, .28f, .90f); smoothness = .5f; emission = new Color(.30f, .10f, .50f); }
        else if (n.Contains("Outfit_Leather")) { color = new Color(.14f, .11f, .10f); smoothness = .35f; }
        else if (n.Contains("Outfit_Steel")) { color = new Color(.72f, .74f, .78f); metallic = .85f; smoothness = .65f; }
        else if (n.Contains("Outfit_Mask")) { color = new Color(.86f, .84f, .80f); smoothness = .45f; }
        else if (n.Contains("Outfit_Eyes")) { color = new Color(.80f, .45f, 1f); smoothness = .6f; emission = new Color(1.6f, .7f, 2.2f); }
        else if (n.Contains("Outfit_Thread")) { color = new Color(.90f, .88f, .84f); }
        else if (n.Contains("Joints")) { color = new Color(.30f, .14f, .46f); smoothness = .55f; }
        else if (n.Contains("Body")) { color = new Color(.11f, .11f, .14f); }
        else return;
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (emission != Color.black)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
    }

    void Build()
    {
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            string key = t.name;
            int colon = key.LastIndexOf(':');
            if (colon >= 0) key = key.Substring(colon + 1);
            if (key.StartsWith("mixamorig")) key = key.Substring("mixamorig".Length).TrimStart('1', '_');
            if (!bones.ContainsKey(key)) bones.Add(key, t);
        }
        Recolor();
        string[] needed = { "Hips", "Spine", "Spine2", "Head", "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };
        foreach (string name in needed) if (!bones.ContainsKey(name)) return;

        Vector3 hips = P("Hips"), chest = P("Spine2"), head = P("Head");
        Vector3 neck = bones.ContainsKey("Neck") ? P("Neck") : Vector3.Lerp(chest, head, .75f);
        u = Mathf.Max(.0001f, (head.y - hips.y) / .6f);
        float left = Mathf.Sign(P("LeftArm").x - P("RightArm").x); // -1 when the rig's left is at -x

        Material coat = Mat("coat", new Color(.085f, .085f, .11f), 0, .25f);
        Material lining = Mat("lining", new Color(.40f, .15f, .60f), 0, .35f);
        Material trim = Mat("trim", new Color(.62f, .28f, .90f), 0, .5f, new Color(.30f, .10f, .50f));
        Material leather = Mat("leather", new Color(.14f, .11f, .10f), 0, .35f);
        Material steel = Mat("steel", new Color(.72f, .74f, .78f), .85f, .65f);
        Material mask = Mat("mask", new Color(.86f, .84f, .80f), 0, .45f);
        Material eyes = Mat("eyes", new Color(.80f, .45f, 1f), 0, .6f, new Color(1.6f, .7f, 2.2f));
        Material thread = Mat("thread", new Color(.90f, .88f, .84f), 0, .3f);

        // ---- coat body -------------------------------------------------------------------
        Begin(new Vector3(hips.x, (hips.y + chest.y) * .5f, hips.z));
        Loft(Ring(hips, hips.y + .02f * u, .185f, .130f, 0, 360, 10), Ring(chest, chest.y, .200f, .140f, 0, 360, 10));
        Commit("outfit_coat_waist", coat, "Spine");

        Begin(new Vector3(chest.x, (chest.y + neck.y) * .5f, chest.z));
        Loft(Ring(chest, chest.y - .01f * u, .205f, .145f, 0, 360, 10), Ring(chest, neck.y - .06f * u, .190f, .130f, 0, 360, 10), Ring(neck, neck.y - .01f * u, .100f, .090f, 0, 360, 10));
        Commit("outfit_coat_chest", coat, "Spine2");

        // ---- asymmetric capelet: hangs lower over the left shoulder ----------------------
        float droop = -left * .28f;
        Begin(new Vector3(chest.x, neck.y - .12f * u, chest.z));
        Loft(Ring(chest, neck.y - .24f * u, .315f, .205f, 0, 360, 12, droop, left * .03f),
             Ring(chest, neck.y - .08f * u, .270f, .170f, 0, 360, 12, droop * .5f, left * .02f),
             Ring(neck, neck.y - .015f * u, .110f, .100f, 0, 360, 12));
        Commit("outfit_capelet", coat, "Spine2");
        Begin(new Vector3(chest.x, neck.y - .25f * u, chest.z));
        Loft(Ring(chest, neck.y - .262f * u, .318f, .208f, 0, 360, 12, droop, left * .03f), Ring(chest, neck.y - .238f * u, .318f, .208f, 0, 360, 12, droop, left * .03f));
        Commit("outfit_capelet_hem", trim, "Spine2");

        // ---- high collar, open at the throat ----------------------------------------------
        string neckBone = bones.ContainsKey("Neck") ? "Neck" : "Spine2";
        Begin(neck + Vector3.up * .03f * u);
        Loft(Ring(neck, neck.y - .03f * u, .088f, .088f, 38, 322, 10), Ring(neck, neck.y + .085f * u, .122f, .118f, 38, 322, 10));
        Commit("outfit_collar", coat, neckBone);
        Begin(neck + Vector3.up * .03f * u);
        Loft(Ring(neck, neck.y + .085f * u, .124f, .120f, 38, 322, 10), Ring(neck, neck.y + .100f * u, .127f, .122f, 38, 322, 10));
        Commit("outfit_collar_edge", trim, neckBone);

        // ---- hood with a swept-back tip ---------------------------------------------------
        Vector3 skull = head + Vector3.up * .10f * u;
        Vector3 back = skull + Vector3.back * .025f * u;
        Begin(skull);
        Loft(Ring(skull, skull.y - .135f * u, .118f, .122f, 58, 302, 12),
             Ring(skull, skull.y - .020f * u, .138f, .142f, 52, 308, 12),
             Ring(skull, skull.y + .080f * u, .128f, .138f, 28, 332, 12),
             Ring(back, skull.y + .150f * u, .082f, .100f, 0, 360, 12),
             Ring(skull + Vector3.back * .085f * u, skull.y + .185f * u, .004f, .004f, 0, 360, 12));
        Commit("outfit_hood", coat, "Head");
        Begin(skull);
        Loft(Ring(skull, skull.y - .137f * u, .121f, .125f, 58, 302, 12), Ring(skull, skull.y - .118f * u, .123f, .127f, 58, 302, 12));
        Commit("outfit_hood_hem", trim, "Head");

        // ---- pale mask with two slanted eye slits -----------------------------------------
        float faceZ = skull.z + .108f * u;
        Vector3 nose = new Vector3(skull.x, skull.y - .025f * u, faceZ + .022f * u);
        Vector3[] rim = {
            new Vector3(skull.x - .066f * u, skull.y + .060f * u, faceZ - .012f * u), new Vector3(skull.x + .066f * u, skull.y + .060f * u, faceZ - .012f * u),
            new Vector3(skull.x + .078f * u, skull.y - .020f * u, faceZ - .020f * u), new Vector3(skull.x, skull.y - .125f * u, faceZ - .004f * u),
            new Vector3(skull.x - .078f * u, skull.y - .020f * u, faceZ - .020f * u)
        };
        Begin(skull);
        for (int i = 0; i < rim.Length; i++) Tri(nose, rim[i], rim[(i + 1) % rim.Length]);
        Commit("outfit_mask", mask, "Head");
        Begin(skull);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 inner = new Vector3(skull.x + side * .014f * u, skull.y + .000f * u, faceZ + .018f * u);
            Vector3 outer = new Vector3(skull.x + side * .056f * u, skull.y + .020f * u, faceZ + .002f * u);
            Vector3 thick = Vector3.up * .013f * u;
            Quad(inner - thick * .4f, outer - thick, outer + thick * .5f, inner + thick * .6f);
        }
        Commit("outfit_mask_eyes", eyes, "Head");

        // ---- belt, buckle and thread spools -------------------------------------------------
        Begin(hips);
        Loft(Ring(hips, hips.y + .000f * u, .200f, .145f, 0, 360, 10), Ring(hips, hips.y + .050f * u, .200f, .145f, 0, 360, 10));
        Commit("outfit_belt", leather, "Hips");
        Begin(hips);
        Box(new Vector3(hips.x, hips.y + .025f * u, hips.z + .150f * u), new Vector3(.055f, .050f, .016f) * u);
        Commit("outfit_buckle", steel, "Hips");
        for (int i = 0; i < 2; i++)
        {
            float a = (-left * (96 + i * 26)) * Mathf.Deg2Rad;
            Vector3 at = new Vector3(hips.x + Mathf.Sin(a) * .215f * u, hips.y - .035f * u, hips.z + Mathf.Cos(a) * .160f * u);
            Begin(at);
            Tube(at - Vector3.up * .032f * u, at + Vector3.up * .032f * u, .026f * u, .026f * u, 8, true);
            Commit("outfit_spool_" + i, i == 0 ? thread : lining, "Hips");
            Begin(at);
            Tube(at - Vector3.up * .040f * u, at - Vector3.up * .032f * u, .034f * u, .034f * u, 8, true);
            Tube(at + Vector3.up * .032f * u, at + Vector3.up * .040f * u, .034f * u, .034f * u, 8, true);
            Commit("outfit_spool_caps_" + i, steel, "Hips");
        }

        // ---- split coat tails: long at the back, short at the sides, open in front ---------
        Tail("outfit_tail_back_long", hips, left > 0 ? 110 : 180, left > 0 ? 180 : 250, .64f, coat, lining);
        Tail("outfit_tail_back_short", hips, left > 0 ? 180 : 110, left > 0 ? 250 : 180, .50f, coat, lining);
        Tail("outfit_tail_side_long", hips, left > 0 ? 60 : 250, left > 0 ? 110 : 300, .42f, coat, lining);
        Tail("outfit_tail_side_short", hips, left > 0 ? 250 : 60, left > 0 ? 300 : 110, .33f, coat, lining);

        // ---- sleeves, bracers and boots ----------------------------------------------------
        foreach (string side in new[] { "Left", "Right" })
        {
            Vector3 shoulder = P(side + "Arm"), elbow = P(side + "ForeArm"), wrist = P(side + "Hand");
            Begin((shoulder + elbow) * .5f);
            Tube(Vector3.Lerp(shoulder, elbow, .08f), Vector3.Lerp(shoulder, elbow, .98f), .066f * u, .054f * u, 8, false);
            Commit("outfit_sleeve_" + side, coat, side + "Arm");
            Begin((elbow + wrist) * .5f);
            Tube(Vector3.Lerp(elbow, wrist, .22f), Vector3.Lerp(elbow, wrist, .96f), .056f * u, .044f * u, 8, false);
            Commit("outfit_bracer_" + side, leather, side + "ForeArm");
            Begin((elbow + wrist) * .5f);
            Tube(Vector3.Lerp(elbow, wrist, .22f), Vector3.Lerp(elbow, wrist, .30f), .060f * u, .059f * u, 8, false);
            Tube(Vector3.Lerp(elbow, wrist, .84f), Vector3.Lerp(elbow, wrist, .90f), .050f * u, .049f * u, 8, false);
            Commit("outfit_bracer_bands_" + side, trim, side + "ForeArm");

            Vector3 knee = P(side + "Leg"), ankle = P(side + "Foot");
            Begin((knee + ankle) * .5f);
            Tube(Vector3.Lerp(knee, ankle, .18f), Vector3.Lerp(knee, ankle, .97f), .074f * u, .058f * u, 8, false);
            Commit("outfit_boot_" + side, leather, side + "Leg");
            Begin((knee + ankle) * .5f);
            Tube(Vector3.Lerp(knee, ankle, .14f), Vector3.Lerp(knee, ankle, .22f), .082f * u, .080f * u, 8, false);
            Commit("outfit_boot_cuff_" + side, coat, side + "Leg");
        }

        // ---- two oversized needles crossed on the back, joined by a loop of thread --------
        Vector3 spine = new Vector3(chest.x, chest.y + .06f * u, chest.z - .185f * u);
        Vector3[] eyeHoles = new Vector3[2];
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 dir = (Vector3.up + Vector3.right * side * .62f).normalized;
            Vector3 tip = spine - dir * .40f * u, eye = spine + dir * .40f * u;
            eyeHoles[side < 0 ? 0 : 1] = eye;
            Begin(spine);
            Tube(tip, spine - dir * .30f * u, .002f * u, .013f * u, 4, false);
            Tube(spine - dir * .30f * u, eye, .013f * u, .010f * u, 4, false);
            Tube(eye, eye + dir * .05f * u, .020f * u, .006f * u, 4, true);
            Commit("outfit_needle_" + (side < 0 ? "a" : "b"), steel, "Spine2");
        }
        Begin(spine);
        Vector3 sag = (eyeHoles[0] + eyeHoles[1]) * .5f + Vector3.down * .16f * u + Vector3.back * .01f * u;
        Tube(eyeHoles[0], sag, .0045f * u, .0045f * u, 4, false);
        Tube(sag, eyeHoles[1], .0045f * u, .0045f * u, 4, false);
        Commit("outfit_needle_thread", trim, "Spine2");
    }

    /// <summary>Dark body with dim violet joints so the outfit reads as one figure.</summary>
    void Recolor()
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            var set = renderer.materials;
            for (int i = 0; i < set.Length; i++)
            {
                if (!set[i]) continue;
                Color current = set[i].HasProperty("_BaseColor") ? set[i].GetColor("_BaseColor") : set[i].color;
                bool joint = renderer.name.IndexOf("Joint", System.StringComparison.OrdinalIgnoreCase) >= 0 || (renderer.name.IndexOf("Surface", System.StringComparison.OrdinalIgnoreCase) < 0 && current.grayscale < .35f);
                Color next = joint ? new Color(.30f, .14f, .46f) : new Color(.11f, .11f, .14f);
                if (set[i].HasProperty("_BaseColor")) set[i].SetColor("_BaseColor", next);
                set[i].color = next;
                if (set[i].HasProperty("_Smoothness")) set[i].SetFloat("_Smoothness", joint ? .55f : .3f);
            }
            renderer.materials = set;
        }
    }

    Vector3 P(string bone) { return transform.InverseTransformPoint(bones[bone].position); }

    static Material Mat(string key, Color color, float metallic, float smoothness, Color? emission = null)
    {
        Material material;
        if (materials.TryGetValue(key, out material) && material) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        material = new Material(shader) { name = "Outfit " + key, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        materials[key] = material;
        return material;
    }

    // ---- mesh building ---------------------------------------------------------------------
    void Begin(Vector3 inside) { verts.Clear(); tris.Clear(); outwardFrom = inside; }

    void Commit(string name, Material material, string bone)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        owned.Add(mesh);
        var part = new GameObject(name);
        part.transform.SetParent(transform, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
        part.transform.SetParent(bones[bone], true);
    }

    /// <summary>Flat-shaded triangle, wound so its normal points away from the piece's interior.</summary>
    void Tri(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(normal, (a + b + c) / 3f - outwardFrom) < 0) { Vector3 swap = b; b = c; c = swap; }
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }
    void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

    /// <summary>
    /// Elliptical arc in the horizontal plane. Angles are degrees clockwise from forward; radii are
    /// in metres on the reference figure. slope tilts the ring sideways, shift moves it sideways.
    /// </summary>
    Vector3[] Ring(Vector3 centre, float y, float rx, float rz, float from, float to, int segments, float slope = 0, float shift = 0)
    {
        var ring = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)segments) * Mathf.Deg2Rad;
            float x = Mathf.Sin(a) * rx * u + shift * u;
            ring[i] = new Vector3(centre.x + x, y + x * slope, centre.z + Mathf.Cos(a) * rz * u);
        }
        return ring;
    }

    void Loft(params Vector3[][] rings)
    {
        for (int r = 0; r + 1 < rings.Length; r++)
            for (int i = 0; i + 1 < rings[r].Length; i++)
                Quad(rings[r][i], rings[r][i + 1], rings[r + 1][i + 1], rings[r + 1][i]);
    }

    void Tube(Vector3 a, Vector3 b, float ra, float rb, int sides, bool capped)
    {
        Vector3 axis = (b - a).normalized;
        Vector3 reference = Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.forward;
        Vector3 x = Vector3.Cross(reference, axis).normalized, z = Vector3.Cross(axis, x);
        Vector3 keep = outwardFrom; outwardFrom = (a + b) * .5f;
        for (int i = 0; i < sides; i++)
        {
            float t0 = i * Mathf.PI * 2 / sides, t1 = (i + 1) * Mathf.PI * 2 / sides;
            Vector3 d0 = x * Mathf.Cos(t0) + z * Mathf.Sin(t0), d1 = x * Mathf.Cos(t1) + z * Mathf.Sin(t1);
            Quad(a + d0 * ra, a + d1 * ra, b + d1 * rb, b + d0 * rb);
            if (capped) { Tri(a, a + d0 * ra, a + d1 * ra); Tri(b, b + d0 * rb, b + d1 * rb); }
        }
        outwardFrom = keep;
    }

    void Box(Vector3 centre, Vector3 size)
    {
        Vector3 h = size * .5f;
        Vector3 keep = outwardFrom; outwardFrom = centre;
        Vector3[] c = new Vector3[8];
        for (int i = 0; i < 8; i++) c[i] = centre + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
        Quad(c[0], c[1], c[3], c[2]); Quad(c[4], c[5], c[7], c[6]);
        Quad(c[0], c[1], c[5], c[4]); Quad(c[2], c[3], c[7], c[6]);
        Quad(c[0], c[2], c[6], c[4]); Quad(c[1], c[3], c[7], c[5]);
        outwardFrom = keep;
    }

    /// <summary>One hanging coat panel with a pointed hem, plus a violet lining just inside it.</summary>
    void Tail(string name, Vector3 hips, float from, float to, float length, Material outer, Material inner)
    {
        for (int layer = 0; layer < 2; layer++)
        {
            float inset = layer == 0 ? 1f : .955f;
            const int segments = 4;
            Vector3[] top = Ring(hips, hips.y + .005f * u, .195f * inset, .140f * inset, from, to, segments);
            Vector3[] hem = Ring(hips, hips.y - length * u, .255f * inset, .195f * inset, from, to, segments);
            // The hem dips to a point in the middle of the panel.
            for (int i = 0; i <= segments; i++) hem[i].y -= (1 - Mathf.Abs(i - segments * .5f) / (segments * .5f)) * .07f * u;
            Begin(new Vector3(hips.x, hips.y - length * .5f * u, hips.z));
            Loft(hem, top);
            Commit(name + (layer == 0 ? "" : "_lining"), layer == 0 ? outer : inner, "Hips");
        }
    }

    void OnDestroy()
    {
        foreach (var mesh in owned) if (mesh) Destroy(mesh);
    }
}
