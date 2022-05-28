using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterController : MonoBehaviour
{
    AbilityHandler abilityHandler;
    PlayerInputActions playerInputActions;

    // Start is called before the first frame update
    void Start()
    {
        playerInputActions = new PlayerInputActions();
        // instantiate abilities
        SampleLightAttack lightAttack = gameObject.AddComponent<SampleLightAttack>();
        SampleHeavyAttack heavyAttack = gameObject.AddComponent<SampleHeavyAttack>();
        
        // populate the set of abilities
        Dictionary<string, Ability> abilities = new Dictionary<string, Ability>();

        abilities[playerInputActions.Player.LightAttack.name] = lightAttack;
        abilities[playerInputActions.Player.HeavyAttack.name] = heavyAttack;

        // instantiate handler with the set of abilities
        abilityHandler = gameObject.AddComponent<AbilityHandler>();
        abilityHandler.Init(abilities);

        // event linking
        playerInputActions.Player.Enable();

        playerInputActions.Player.LightAttack.started += CastAbility;
        playerInputActions.Player.LightAttack.performed += CastAbility;
        playerInputActions.Player.LightAttack.canceled += CastAbility;

        playerInputActions.Player.HeavyAttack.started += CastAbility;
        playerInputActions.Player.HeavyAttack.performed += CastAbility;
        playerInputActions.Player.HeavyAttack.canceled += CastAbility;
    }

    #region abilities
    public void CastAbility(InputAction.CallbackContext context)
    {
        if (context.started) abilityHandler.StartAbility(context.action.name);
        else if (context.performed) abilityHandler.PerformAbility(context.action.name);
        else if (context.canceled) abilityHandler.CancelAbility(context.action.name);
    }
    #endregion

}
