using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RagePE : PlayerEffect
{
    private float consumedDefense = .33f;
    private float strengthIncrement = 0.2f;
    private float cooldownReduction = 0.3f;

    private void Awake()
    {
        duration = 8f; // if set in the start it doesn't work apparently ¯\_(?)_/¯
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

        characterController.stats.defense *= 1-consumedDefense;
        characterController.stats.strength *= 1 + strengthIncrement;
        foreach(Ability ability in characterController.formManager.currentForm.abilityHandler.abilities.Values)
            ability.cooldown *= 1-cooldownReduction;

        yield return new WaitForSeconds(duration);

        characterController.stats.defense /= 1 - consumedDefense;
        characterController.stats.strength /= 1 + strengthIncrement;
        foreach (Ability ability in characterController.formManager.currentForm.abilityHandler.abilities.Values)
            ability.cooldown /= 1 - cooldownReduction;

        Destroy(this);
    }
}
