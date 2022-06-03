using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WebSocketSharp;

public class FormManager : MonoBehaviourPun
{
    // protected PhotonView photonView;
    public int ViewID { get => photonView.ViewID; }
    public bool IsMine { get => photonView.IsMine; }

    public static event Action<FormManager> OnFormChanged;

    [HideInInspector] public List<PlayerForm> forms;
    [HideInInspector] public PlayerForm currentForm;

    protected PlayerInputActions playerInputActions;
    protected AbilityHandler sharedAbilityHandler; // handler of shared abilities, like dash and interact
    protected PlayerController characterController;

    public virtual void Init(PlayerController characterController)
    {
        this.characterController = characterController;
        playerInputActions = new PlayerInputActions();
        
        // initialize list form and adding Spirit form as first form available (and shared by every macro class)
        forms = new List<PlayerForm>();
        SpiritForm addedForm = gameObject.AddComponent<SpiritForm>();
        //addedForm.transform.parent = transform;
        forms.Add(addedForm);

        // adding shared abilities
        Dash dash = gameObject.AddComponent<Dash>();
        Dictionary<string, Ability> sharedAbilities = new Dictionary<string, Ability>();
        sharedAbilities[playerInputActions.Player.Dash.name] = dash;
        
        sharedAbilityHandler = gameObject.AddComponent<AbilityHandler>();
        sharedAbilityHandler.Init(sharedAbilities, characterController);
    }

    protected virtual void BindAbilities()
    {
        if (!IsMine) return;
        playerInputActions.Player.Spirit.performed += ctx => SwitchForm(0);

        playerInputActions.Player.Dash.started += CastSharedAbility;
        playerInputActions.Player.Dash.performed += CastSharedAbility;
        playerInputActions.Player.Dash.canceled += CastSharedAbility;

        playerInputActions.Player.LightAttack.started += CastAbility;
        playerInputActions.Player.LightAttack.performed += CastAbility;
        playerInputActions.Player.LightAttack.canceled += CastAbility;

        playerInputActions.Player.HeavyAttack.started += CastAbility;
        playerInputActions.Player.HeavyAttack.performed += CastAbility;
        playerInputActions.Player.HeavyAttack.canceled += CastAbility;
    }

    protected virtual void EnableAbilities()
    {
        if (!IsMine) return;
        playerInputActions.Player.LightAttack.Enable();
        playerInputActions.Player.HeavyAttack.Enable();
        playerInputActions.Player.Dash.Enable();
        playerInputActions.Player.Transformation1.Enable();
        playerInputActions.Player.Transformation2.Enable();
        playerInputActions.Player.Spirit.Enable();
    }

    protected virtual void DisableAbilities()
    {
        if (!IsMine) return;
        playerInputActions.Player.LightAttack.Disable();
        playerInputActions.Player.HeavyAttack.Disable();
        playerInputActions.Player.Dash.Disable();
        playerInputActions.Player.Transformation1.Disable();
        playerInputActions.Player.Transformation2.Disable();
        playerInputActions.Player.Spirit.Disable();
    }

    public void SwitchForm(int index, bool enableAbilities = true)
    {
        if (index >= forms.Count || forms[index] == null) { Debug.Log("Invalid form"); return; }
        if (currentForm != null && forms[index].GetType().Name.Equals(currentForm.GetType().Name)) { Debug.Log("Form already in use"); return; }

        Debug.Log($"Switching to {forms[index].GetType().Name}");

        DisableAbilities();

        // clean player from old form components
        if (currentForm != null) currentForm.RemoveComponents();

        // switch to new form and add its components
        currentForm = forms[index];
        currentForm.Init(characterController);
        
        if(enableAbilities) EnableAbilities();
 
        OnFormChanged?.Invoke(this);
    }


    public void CastAbility(InputAction.CallbackContext context)
    {
        photonView.RPC("RpcCastAbility", RpcTarget.All, false, context.started, context.performed, context.canceled, context.action.name);
    }

    public void CastSharedAbility(InputAction.CallbackContext context)
    {
        photonView.RPC("RpcCastAbility", RpcTarget.All, true, context.started, context.performed, context.canceled, context.action.name);
    }
    
    [PunRPC]
    public void RpcCastAbility(bool isSharedAbility, bool isStarted, bool isPerformed, bool isCanceled, string actionName)
    {
        Debug.Log("CIAO SONO l'RPC");
        Debug.Log("sharedAbilityHandler: " + sharedAbilityHandler);
        Debug.Log("abilityHandler: " + currentForm.abilityHandler);

        AbilityHandler abilityHandler = isSharedAbility ? sharedAbilityHandler : currentForm.abilityHandler;
        
        if (isStarted) abilityHandler.StartAbility(actionName);
        else if (isPerformed) abilityHandler.PerformAbility(actionName);
        else if (isCanceled) abilityHandler.CancelAbility(actionName);
    }
}
