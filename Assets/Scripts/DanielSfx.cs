using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Original sound effects synthesised at runtime: no audio assets are needed.
/// The palette follows the character: cold scissor steel (inharmonic rings, sharp snips)
/// over shadow (filtered noise, low swells). Clips are built on first use and cached.
/// </summary>
public static class DanielSfx
{
    const int Rate = 44100;
    public static float MasterVolume = .7f;
    static GameObject host;
    static AudioSource[] sources;
    static int next;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static System.Random random = new System.Random(1);

    public static void Play(string name, float volume = 1f)
    {
        AudioClip clip = Clip(name);
        if (!clip) return;
        EnsureHost();
        var source = sources[next]; next = (next + 1) % sources.Length;
        source.Stop();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * MasterVolume);
        // Slight pitch variation keeps repeated hits from sounding identical; the chime stays in tune.
        source.pitch = name == "WReady" ? 1 : Random.Range(.96f, 1.04f);
        source.Play();
    }

    static void EnsureHost()
    {
        if (host) return;
        host = new GameObject("Daniel SFX");
        Object.DontDestroyOnLoad(host);
        // The simulator disables the scene cameras, which usually carry the only listener.
        if (Object.FindFirstObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
        sources = new AudioSource[8];
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = host.AddComponent<AudioSource>();
            sources[i].playOnAwake = false; sources[i].spatialBlend = 0;
        }
    }

    static AudioClip Clip(string name)
    {
        AudioClip clip;
        if (clips.TryGetValue(name, out clip) && clip) return clip;
        int seed = 17; foreach (char c in name) seed = seed * 31 + c;
        random = new System.Random(seed);
        float[] data = Build(name);
        if (data == null) return null;
        clip = AudioClip.Create("SFX " + name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        clips[name] = clip;
        return clip;
    }

    static float[] Build(string name)
    {
        float[] b;
        switch (name)
        {
            case "QOpen": // blades springing open: rising steel scrape with a thin ring
                b = New(.24f);
                Noise(b, 0, .16f, 1f, 2200, 6500, .35f, .005f, 1.5f);
                Ring(b, .02f, .35f, new[] { 3100f, 4700f, 6200f }, .18f);
                return Finish(b, .42f);
            case "QSnip": // blades meeting: click, cutting hiss, long steel ring, low thud
                b = New(.44f);
                Noise(b, 0, .012f, 1.2f, 5000, 6500, 1.2f, .0005f, 1f, 2);
                Noise(b, 0, .09f, .9f, 6000, 1800, .5f, .002f, 2f);
                Ring(b, .004f, .6f, new[] { 2350f, 3920f, 5310f, 6870f }, .38f);
                Tone(b, 0, .14f, .9f, 95, 48, 2f);
                return Finish(b, .8f);
            case "Impact": // something was cut
                b = New(.2f);
                Tone(b, 0, .16f, 1f, 140, 55, 2.5f);
                Noise(b, 0, .07f, .5f, 900, 300, .9f, .001f, 2f, 0);
                return Finish(b, .55f);
            case "WCast": // mark placed: hushed two-note hum with a faint steel glint
                b = New(.4f);
                Tone(b, 0, .34f, .6f, 196, 147, 1.5f, .02f, 9, .02f);
                Tone(b, 0, .3f, .3f, 294, 220, 1.5f, .02f);
                Noise(b, 0, .3f, .35f, 1200, 500, .6f, .03f, 1.5f);
                Ring(b, .03f, .25f, new[] { 2637f, 3951f }, .2f);
                return Finish(b, .48f);
            case "WReady": // mark complete: small cold chime, minor third
                b = New(.7f);
                Tone(b, 0, .5f, .7f, 587.3f, 587.3f, 3f, .004f);
                Tone(b, 0, .3f, .2f, 1621f, 1621f, 3f, .004f);
                Tone(b, .09f, .55f, .7f, 698.5f, 698.5f, 3f, .004f);
                Tone(b, .09f, .3f, .2f, 1928f, 1928f, 3f, .004f);
                return Finish(b, .42f);
            case "WBurst": // mark snaps shut: two crossing cuts, ring, thud
                b = New(.55f);
                Noise(b, 0, .08f, 1f, 6500, 1500, .45f, .002f, 1.5f);
                Noise(b, .07f, .1f, 1f, 6000, 1200, .45f, .002f, 1.5f);
                Ring(b, .07f, .5f, new[] { 2100f, 3350f, 4980f }, .4f);
                Tone(b, .07f, .18f, 1f, 120, 45, 2f);
                return Finish(b, .85f);
            case "Dash": // shadow step: airy rush
                b = New(.34f);
                Noise(b, 0, .3f, 1f, 350, 1900, .7f, .05f, 1.2f);
                Noise(b, .05f, .25f, .5f, 2500, 700, .5f, .04f, 1.5f);
                return Finish(b, .55f);
            case "StealthIn": // fading from sight: air closing down into a low murmur
                b = New(.52f);
                Noise(b, 0, .45f, .8f, 2400, 180, .6f, .02f, 1.3f);
                Tone(b, 0, .45f, .5f, 130, 65, 1.2f, .05f, 6, .03f);
                return Finish(b, .42f);
            case "StealthOut": // reappearing: short breath opening up
                b = New(.3f);
                Noise(b, 0, .26f, .8f, 300, 2600, .6f, .01f, 2f);
                Tone(b, 0, .2f, .3f, 220, 330, 2f);
                return Finish(b, .32f);
            case "Attack": // basic cut: quick hiss with a steel tick
                b = New(.2f);
                Noise(b, 0, .09f, 1f, 5500, 2000, .5f, .002f, 1.8f);
                Ring(b, .01f, .3f, new[] { 3300f, 5100f }, .12f);
                Tone(b, .01f, .08f, .5f, 160, 80, 2f);
                return Finish(b, .5f);
            case "Ambush": // empowered strike from behind: rush in, heavy cut, ring
                b = New(.55f);
                Noise(b, 0, .12f, .7f, 400, 3000, .6f, .02f, 1f);
                Noise(b, .1f, .13f, 1.1f, 6500, 1300, .4f, .002f, 1.5f);
                Ring(b, .1f, .55f, new[] { 1980f, 3120f, 4650f, 6100f }, .38f);
                Tone(b, .1f, .22f, 1.1f, 110, 40, 2f);
                return Finish(b, .9f);
            case "RCharge": // half-second pre-delay: shadow gathering, rising
                b = New(.56f);
                Tone(b, 0, .52f, .9f, 55, 98, 1.6f, .003f, 7, .04f, true);
                Tone(b, 0, .52f, .2f, 220, 330, 2f, .003f, 0, 0, true);
                Noise(b, 0, .52f, .6f, 200, 1500, .6f, .003f, 1.8f, 1, true);
                return Finish(b, .6f);
            case "REnter": // pouring into the target's shadow: air sucked down, sub thump
                b = New(.46f);
                Noise(b, 0, .3f, 1f, 3500, 160, .5f, .01f, .8f);
                Tone(b, .22f, .22f, 1f, 85, 38, 2f, .005f);
                Noise(b, .22f, .1f, .4f, 500, 200, .9f, .002f, 2f, 0);
                return Finish(b, .75f);
            case "RTick": // damage from inside: muffled stab
                b = New(.18f);
                Tone(b, 0, .14f, 1f, 130, 60, 2.5f);
                Noise(b, 0, .05f, .6f, 2500, 900, .6f, .001f, 2f);
                return Finish(b, .5f);
            case "RExit": // bursting out: rush, long cut, widest ring, deep thud
                b = New(.75f);
                Noise(b, 0, .14f, .8f, 250, 2800, .6f, .03f, 1f);
                Noise(b, .1f, .2f, 1.2f, 6800, 900, .4f, .002f, 1.4f);
                Ring(b, .1f, .6f, new[] { 1760f, 2790f, 4180f, 5900f, 6900f }, .55f);
                Tone(b, .1f, .3f, 1.2f, 100, 34, 1.8f);
                return Finish(b, .95f);
            case "Hurt": // player struck
                b = New(.25f);
                Tone(b, 0, .2f, 1f, 180, 70, 2f);
                Noise(b, 0, .1f, .7f, 700, 250, .8f, .001f, 2f, 0);
                return Finish(b, .6f);
            case "Click": // move / attack order
                b = New(.06f);
                Tone(b, 0, .05f, 1f, 1400, 900, 3f, .001f);
                return Finish(b, .14f);
        }
        return null;
    }

    static float[] New(float seconds) { return new float[Mathf.CeilToInt(seconds * Rate)]; }
    static float Rand() { return (float)(random.NextDouble() * 2 - 1); }

    static float Envelope(float t, float seconds, float attack, float shape, bool rise)
    {
        if (rise) return Mathf.Pow(t, shape) * Mathf.Min(1, (1 - t) * 20);
        return Mathf.Min(1, seconds / Mathf.Max(.0001f, attack)) * Mathf.Pow(1 - t, shape);
    }

    /// <summary>Filtered noise whose centre frequency glides from f0 to f1. mode: 0 low-pass, 1 band-pass, 2 high-pass.</summary>
    static void Noise(float[] b, float start, float dur, float amp, float f0, float f1, float damping, float attack = .004f, float shape = 2f, int mode = 1, bool rise = false)
    {
        int s = (int)(start * Rate), n = Mathf.Max(1, (int)(dur * Rate));
        float low = 0, band = 0;
        for (int i = 0; i < n && s + i < b.Length; i++)
        {
            float t = i / (float)n;
            float fc = Mathf.Min(7000f, f0 * Mathf.Pow(f1 / f0, t));
            float f = 2 * Mathf.Sin(Mathf.PI * fc / Rate);
            low += f * band;
            float high = Rand() - low - damping * band;
            band += f * high;
            float y = mode == 0 ? low : mode == 1 ? band : high;
            b[s + i] += y * Envelope(t, i / (float)Rate, attack, shape, rise) * amp;
        }
    }

    /// <summary>Sine gliding from f0 to f1, with optional vibrato.</summary>
    static void Tone(float[] b, float start, float dur, float amp, float f0, float f1, float shape = 2f, float attack = .003f, float vibratoHz = 0, float vibratoDepth = 0, bool rise = false)
    {
        int s = (int)(start * Rate), n = Mathf.Max(1, (int)(dur * Rate));
        float phase = 0;
        for (int i = 0; i < n && s + i < b.Length; i++)
        {
            float t = i / (float)n, seconds = i / (float)Rate;
            float freq = f0 * Mathf.Pow(f1 / f0, t) * (1 + vibratoDepth * Mathf.Sin(2 * Mathf.PI * vibratoHz * seconds));
            phase += 2 * Mathf.PI * freq / Rate;
            b[s + i] += Mathf.Sin(phase) * Envelope(t, seconds, attack, shape, rise) * amp;
        }
    }

    /// <summary>Struck steel: a few inharmonic partials, higher ones quieter and shorter.</summary>
    static void Ring(float[] b, float start, float amp, float[] partials, float decay)
    {
        for (int k = 0; k < partials.Length; k++)
            Tone(b, start, decay * (1 - .12f * k), amp / (k + 1), partials[k], partials[k] * .996f, 3f, .001f);
    }

    static float[] Finish(float[] b, float level)
    {
        float peak = .0001f;
        for (int i = 0; i < b.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(b[i]));
        float gain = level / peak;
        int tail = Mathf.Min(b.Length, Rate / 200);
        for (int i = 0; i < b.Length; i++)
        {
            float edge = Mathf.Min(1, (b.Length - 1 - i) / (float)tail);
            b[i] *= gain * edge;
        }
        return b;
    }
}
