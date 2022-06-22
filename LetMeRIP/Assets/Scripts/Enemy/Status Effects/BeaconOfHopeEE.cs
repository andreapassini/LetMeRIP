using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeaconOfHopeEE : EnemyEffect
{
    private float damagePerTick;
    private float tickOffset = 0.5f;

    private GameObject vfx;
    private GameObject vfxInstance;

    private void Awake()
    {
        duration = 5f;
        vfx = Resources.Load<GameObject>($"Particles/{nameof(BeaconOfHopeEE)}");
    }

    protected override void Start()
    {
        base.Start();
    }

    public void Init(float damagePerTick, float duration)
    {
        this.damagePerTick = damagePerTick;
        Destroy(this, duration);
    }

    public override void StartEffect()
    {
        base.StartEffect();
        if (vfxInstance != null) Destroy(vfxInstance);
        vfx ??= Resources.Load<GameObject>($"Particles/{nameof(BeaconOfHopeEE)}");
        vfxInstance = Instantiate(vfx, transform);
    }

    /**
     * deals damagePerTick damage to the holder every tickOffset seconds until this
     * effect is active
     */
    public override IEnumerator Effect(EnemyForm eform)
    {
        if (!PhotonNetwork.IsMasterClient) yield break;
        for (;;)
        {
            eform.TakeDamage(damagePerTick);
            yield return new WaitForSeconds(tickOffset);
        }
    }

    public void ResetDuration()
    {
        StopCoroutine(effectCoroutine);
        StartEffect();
    }

}
