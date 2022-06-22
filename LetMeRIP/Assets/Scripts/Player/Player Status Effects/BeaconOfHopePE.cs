using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeaconOfHopePE : PlayerEffect
{
    private float strMultiplier = .1f;
    private float dexMultiplier = .1f;
    private float intMultiplier = .15f;

    private GameObject vfx;
    private GameObject vfxInstance;

    private void Awake()
    {
        duration = 5f;
        // load vfx
        vfx = Resources.Load<GameObject>($"Particles/{nameof(BeaconOfHopePE)}");
    }

    protected override void Start()
    {
        base.Start();
    }

    public override void StartEffect()
    {
        base.StartEffect();
        if (vfxInstance != null) Destroy(vfxInstance);
        vfx ??= Resources.Load<GameObject>($"Particles/{nameof(BeaconOfHopePE)}");
        vfxInstance = Instantiate(vfx, transform);
    }

    public void Init(float duration)
    {
        this.duration = duration;
    }

    public override IEnumerator Effect(PlayerController characterController)
    {
        characterController.stats.strength *= strMultiplier;
        characterController.stats.intelligence *= intMultiplier;
        characterController.stats.dexterity *= dexMultiplier;

        yield return new WaitForSeconds(duration);
        
        characterController.stats.strength /= strMultiplier;
        characterController.stats.intelligence /= intMultiplier;
        characterController.stats.dexterity /= dexMultiplier;

        Destroy(this);
    }

    public void ResetDuration()
    {
        StopCoroutine(effectCoroutine);
        StartEffect();
    }


    private void OnDestroy()
    {
        if (vfxInstance != null) Destroy(vfxInstance);
    }
}
