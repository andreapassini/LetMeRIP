using System.Collections.Generic;
using UnityEngine;


public class HudWarriorStatusController : HudStatusController
{
    /*
     * Load all the sprites needed for the current player's class
     */
    private new void Awake()
    {
        base.Awake();    
        
        //holds the sprites of the abilities of each form
        abilitiesSprites = new Dictionary<HudEForm, Dictionary<HudEAbility, Sprite>>
        {
            [HudEForm.Spirit] = new Dictionary<HudEAbility, Sprite>()
            {
                [HudEAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Spirit/LightAttack"),
                [HudEAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Spirit/HeavyAttack"),
                [HudEAbility.Ability1] = Resources.Load<Sprite>("Sprites/Abilities/Spirit/Ability1"),
                [HudEAbility.Ability2] = Resources.Load<Sprite>("Sprites/Abilities/Spirit/Ability2"),
                [HudEAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Dash"),
            },
            [HudEForm.Base] = new Dictionary<HudEAbility, Sprite>()
            {
                [HudEAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Base/LightAttack"),
                [HudEAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Base/HeavyAttack"),
                [HudEAbility.Ability1] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Base/Ability1"),
                [HudEAbility.Ability2] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Base/Ability2"),
                [HudEAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Dash"),
            },
            [HudEForm.Trans1] = new Dictionary<HudEAbility, Sprite>()
            {
                [HudEAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Berserker/LightAttack"),
                [HudEAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Berserker/HeavyAttack"),
                [HudEAbility.Ability1] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Berserker/Ability1"),
                [HudEAbility.Ability2] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Berserker/Ability2"),
                [HudEAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Dash"),
            },
            [HudEForm.Trans2] = new Dictionary<HudEAbility, Sprite>()
            {
                [HudEAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Berserker/LightAttack"),
                [HudEAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Berserker/HeavyAttack"),
                [HudEAbility.Ability1] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Berserker/Ability1"),
                [HudEAbility.Ability2] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Berserker/Ability2"),
                [HudEAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Dash"),
            }
        };
        
        //Holds the sprites for all the forms
        formsSprites = new Dictionary<HudEForm, Sprite>
        {
            [HudEForm.Spirit] = Resources.Load<Sprite>("Sprites/Abilities/MissingForm"),
            [HudEForm.Base] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Base/Form"),
            [HudEForm.Trans1] = Resources.Load<Sprite>("Sprites/Abilities/Warrior/Berserker/Form"),
            [HudEForm.Trans2] = Resources.Load<Sprite>("Sprites/Abilities/MissingForm")
        };
    }
}