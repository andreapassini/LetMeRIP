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
                [HudEAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Ability1] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Ability2] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            },
            [HudEForm.Base] = new Dictionary<HudEAbility, Sprite>()
            {
                [HudEAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Ability1] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Ability2] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            },
            [HudEForm.Trans1] = new Dictionary<HudEAbility, Sprite>()
            {
                [HudEAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Mage/LightAttack"),
                [HudEAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Mage/HeavyAttack"),
                [HudEAbility.Ability1] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Ability2] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            },
            [HudEForm.Trans2] = new Dictionary<HudEAbility, Sprite>()
            {
                [HudEAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Ability1] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Ability2] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [HudEAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            }
        };
        
        //Holds the sprites for all the forms
        formsSprites = new Dictionary<HudEForm, Sprite>
        {
            [HudEForm.Spirit] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            [HudEForm.Base] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            [HudEForm.Trans1] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            [HudEForm.Trans2] = Resources.Load<Sprite>("Sprites/Abilities/Charge")
        };
    }
}