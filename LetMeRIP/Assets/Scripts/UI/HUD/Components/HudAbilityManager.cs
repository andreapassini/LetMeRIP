using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HudAbilityManager : MonoBehaviour
{
    //holds the gameobject for each ability
    private Dictionary<HudEForm, Dictionary<HudEAbility, Sprite>> sprites;
    private Dictionary<HudEAbility, GameObject> hudAbilitiesGO;

    private void Awake()
    {
        //init the dictionary with all the abilities' gameobjects
        hudAbilitiesGO = new Dictionary<HudEAbility, GameObject>();
        
        foreach (HudEAbility ability in EnumUtils.GetValues<HudEAbility>())
        {
            Transform abilityTransform = transform.Find(ability.ToString());

            if (abilityTransform == null) throw new Exception("The ability " + ability + " is not available");

            hudAbilitiesGO[ability] = abilityTransform.gameObject;
        }
    }

    public void Init(HudEForm initialHudEForm,
        Dictionary<HudEForm, Dictionary<HudEAbility, Sprite>> sprites,
        Dictionary<HudEAbility, Ability> initialAbilities)
    {
        this.sprites = sprites;
        setAbilities(initialHudEForm, initialAbilities);
    }

    public void changeAbilities(HudEForm newHudEForm, Dictionary<HudEAbility, Ability> newAbilities)
    {
        foreach (var ability in hudAbilitiesGO.Values)
            // Destroy(ability.GetComponent<HudAbility>());
            ability.GetComponent<HudAbility>().Destroy();

        setAbilities(newHudEForm, newAbilities);
    }

    private void setAbilities(HudEForm hudEForm, Dictionary<HudEAbility, Ability> abilities)
    {
        var formSprites = sprites[hudEForm];

        foreach (HudEAbility ability in EnumUtils.GetValues<HudEAbility>())
            if (hudAbilitiesGO.ContainsKey(ability) &&
                formSprites.ContainsKey(ability) &&
                abilities.ContainsKey(ability))
            {
                hudAbilitiesGO[ability]
                    .AddComponent<HudAbility>()
                    .Init(formSprites[ability], abilities[ability]);
            }
    }
}