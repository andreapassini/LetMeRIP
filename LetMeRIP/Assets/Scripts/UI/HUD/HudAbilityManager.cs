using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HudAbilityManager : MonoBehaviour
{
    //holds the gameobject for each ability
    private Dictionary<Form, Dictionary<EAbility, Sprite>> sprites;
    private Dictionary<EAbility, GameObject> hudAbilitiesGO;

    private void Awake()
    {
        //init the dictionary with all the abilities' gameobjects
        hudAbilitiesGO = new Dictionary<EAbility, GameObject>();
        
        foreach (EAbility ability in EnumUtils.GetValues<EAbility>())
        {
            Transform abilityTransform = transform.Find(ability.ToString());

            if (abilityTransform == null) throw new Exception("The ability " + ability + " is not available");

            hudAbilitiesGO[ability] = abilityTransform.gameObject;
        }
    }

    public void Init(Form initialForm,
        Dictionary<Form, Dictionary<EAbility, Sprite>> sprites,
        Dictionary<EAbility, Ability> initialAbilities)
    {
        this.sprites = sprites;
        setAbilities(initialForm, initialAbilities);
    }

    public void changeAbilities(Form newForm, Dictionary<EAbility, Ability> newAbilities)
    {
        foreach (var ability in hudAbilitiesGO.Values)
            Destroy(ability.GetComponent<HudAbility>());

        setAbilities(newForm, newAbilities);
    }

    private void setAbilities(Form form, Dictionary<EAbility, Ability> abilities)
    {
        var formSprites = sprites[form];

        foreach (EAbility ability in EnumUtils.GetValues<EAbility>())
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