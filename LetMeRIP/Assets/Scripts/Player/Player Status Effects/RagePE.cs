using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RagePE : PlayerEffect
{
    private float consumedDefense = .33f;
    private float strengthIncrement = 0.2f;
    private float cooldownReduction = 0.3f;
    private GameObject ps;

    private void Awake()
    {
        duration = 8f; // if set in the start it doesn't work apparently ¯\_(?)_/¯
        ps = Resources.Load<GameObject>("Particles/BerserkerAbility1");

    }

    protected override void Start()
    {
        base.Start();
    }

    public override void StartEffect()
    {
        base.StartEffect();
    }

    public override IEnumerator Effect(PlayerController characterController)
    {
        // vfx here
        //GameObject psInstance = PhotonNetwork.Instantiate("Particles/BerserkerAbility1", transform.position, transform.rotation);
        ps ??= Resources.Load<GameObject>("Particles/BerserkerAbility1");
        GameObject psInstance = Instantiate(ps, transform.position, transform.rotation);
        psInstance.transform.SetParent(transform);
        psInstance.GetComponent<ParticleSystem>().Play();

        characterController.stats.defense *= 1-consumedDefense;
        characterController.stats.strength *= 1 + strengthIncrement;
        foreach(Ability ability in characterController.formManager.currentForm.abilityHandler.abilities.Values)
            ability.cooldown *= 1-cooldownReduction;

        yield return new WaitForSeconds(duration);

        characterController.stats.defense /= 1 - consumedDefense;
        characterController.stats.strength /= 1 + strengthIncrement;
        foreach (Ability ability in characterController.formManager.currentForm.abilityHandler.abilities.Values)
            ability.cooldown /= 1 - cooldownReduction;
        Destroy(psInstance);
        Destroy(this);
    }
}
