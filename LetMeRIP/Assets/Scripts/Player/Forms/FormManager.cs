using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FormManager : MonoBehaviour
{
    [HideInInspector] public List<PlayerForm> forms;
    [HideInInspector] public PlayerForm currentForm;

    private PlayerInputActions playerInputActions;
    private AbilityHandler sharedAbilityHandler; // handler of shared abilities, like dash and interact
    private void Start()
    {
        playerInputActions = new PlayerInputActions();

        forms = new List<PlayerForm>();
        forms.Add(gameObject.AddComponent<SampleForm1>());
        forms.Add(gameObject.AddComponent<SampleForm2>());

        SwitchForm(0);

        if(currentForm != null) BindAbilities();
    }

    private void BindAbilities()
    {
        playerInputActions.Player.LightAttack.started += CastAbility;
        playerInputActions.Player.LightAttack.performed += CastAbility;
        playerInputActions.Player.LightAttack.canceled += CastAbility;

        playerInputActions.Player.HeavyAttack.started += CastAbility;
        playerInputActions.Player.HeavyAttack.performed += CastAbility;
        playerInputActions.Player.HeavyAttack.canceled += CastAbility;
    }

    private void EnableAbilities()
    {
        playerInputActions.Player.LightAttack.Enable();
        playerInputActions.Player.HeavyAttack.Enable();
    }

    private void DisableAbilities()
    {
        playerInputActions.Player.LightAttack.Disable();
        playerInputActions.Player.HeavyAttack.Disable();
    }

    public void SwitchForm(int index)
    {
        if (index >= forms.Count || forms[index] == null) { Debug.Log("Invalid form"); return; }
        if (currentForm != null && forms[index].GetType().Name.Equals(currentForm.GetType().Name)) { Debug.Log("Form already in use"); return; }

        Debug.Log($"Switching to {forms[index].GetType().Name}");

        DisableAbilities();

        // clean player from old form components
        if (currentForm != null)
            currentForm.RemoveComponents();

        // switch to new form and add its components
        currentForm = forms[index];
        currentForm.Init();

        EnableAbilities();
    }


    public void CastAbility(InputAction.CallbackContext context)
    {
        if (context.started) currentForm.abilityHandler.StartAbility(context.action.name);
        else if (context.performed) currentForm.abilityHandler.PerformAbility(context.action.name);
        else if (context.canceled) currentForm.abilityHandler.CancelAbility(context.action.name);
    }

}
