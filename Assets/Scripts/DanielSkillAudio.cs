using UnityEngine;

[DisallowMultipleComponent]
public sealed class DanielSkillAudio : MonoBehaviour
{
    public enum Cue
    {
        Q_Cast, Q_Snap, W_Charge, W_Ready, W_Burst, E_Dash, E_Strike,
        R_Latch, R_Enter, R_Tick, R_Exit, Attack_1, Attack_2, Step_1, Step_2
    }

    AudioClip[] clips;
    AudioSource oneShots;
    AudioSource charge;
    bool alternateAttack;

    void Awake()
    {
        var cues = (Cue[])System.Enum.GetValues(typeof(Cue));
        clips = new AudioClip[cues.Length];
        foreach (Cue cue in cues)
            clips[(int)cue] = Resources.Load<AudioClip>("SkillAudio/" + cue);

        oneShots = CreateSource();
        charge = CreateSource();
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
            oneShots.PlayOneShot(clip, volumeScale);
        }
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
        CancelCharge();
        alternateAttack = false;
    }
}
