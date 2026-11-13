using UnityEngine;

[DisallowMultipleComponent]
public sealed class DanielSkillAudio : MonoBehaviour
{
    public enum Cue
    {
        Q_Cast, Q_Snap, W_Charge, W_Ready, W_Burst, E_Dash, E_Strike,
        Attack_1, Attack_2, Step_1, Step_2, D_Cloak, D_Strike, Adriana_Dash
    }

    // R_Full.wav begins at 1.94 s in the reference recording.
    public const float UltimateEnterOffset = .50f, UltimateExitOffset = 2.26f;

    AudioClip[] clips;
    AudioSource oneShots;
    AudioSource charge;
    AudioSource ultimate;
    const float SkillVolumeScale = .65f;
    bool alternateAttack;

    void Awake()
    {
        var cues = (Cue[])System.Enum.GetValues(typeof(Cue));
        clips = new AudioClip[cues.Length];
        foreach (Cue cue in cues)
            clips[(int)cue] = Resources.Load<AudioClip>("SkillAudio/" + cue);

        oneShots = CreateSource();
        charge = CreateSource();
        ultimate = CreateSource();
        ultimate.volume *= SkillVolumeScale;
        charge.volume *= SkillVolumeScale;
        ultimate.clip = Resources.Load<AudioClip>("SkillAudio/R_Full");
    }

    AudioSource CreateSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.volume = .8f;
        return source;
    }

    public void Play(Cue cue) => Play(cue, 1f);

    void Play(Cue cue, float volumeScale)
    {
        var clip = clips[(int)cue];
        if (!clip) return;
        if (cue == Cue.W_Charge)
        {
            charge.clip = clip;
            charge.Play();
        }
        else
        {
            if (cue == Cue.W_Ready) CancelCharge();
            bool isSkill = cue != Cue.Attack_1 && cue != Cue.Attack_2 && cue != Cue.Step_1 && cue != Cue.Step_2;
            oneShots.PlayOneShot(clip, isSkill ? volumeScale * SkillVolumeScale : volumeScale);
        }
    }

    public void PlayUltimate()
    {
        if (!ultimate.clip) return;

        ultimate.Stop();
        ultimate.timeSamples = 0;
        ultimate.Play();
    }

    public void SkipToUltimateExit()
    {
        if (!ultimate.isPlaying) return;

        int exitSample = Mathf.RoundToInt(UltimateExitOffset * ultimate.clip.frequency);
        if (ultimate.timeSamples < exitSample) ultimate.timeSamples = exitSample;
    }

    public void CancelCharge() => charge.Stop();

    public void PlayBasicAttack()
    {
        Play(alternateAttack ? Cue.Attack_2 : Cue.Attack_1);
        alternateAttack = !alternateAttack;
    }

    // The captured footfalls are much quieter than the combat sounds.
    public void PlayFootstep(bool leftFoot) => Play(leftFoot ? Cue.Step_1 : Cue.Step_2, 6f);

    public void StopAll()
    {
        oneShots.Stop();
        ultimate.Stop();
        CancelCharge();
        alternateAttack = false;
    }
}
