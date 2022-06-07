using System;
using System.Collections.Generic;
using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class FormManager : MonoBehaviourPun
{
    public int ViewID { get => photonView.ViewID; }

    public static event Action<FormManager> OnFormChanged;

    [HideInInspector] public List<PlayerForm> forms;
    [HideInInspector] public PlayerForm currentForm;

    protected PlayerInputActions playerInputActions;
    protected AbilityHandler sharedAbilityHandler; // handler of shared abilities, like dash and interact
    protected PlayerController characterController;

    // units of distance from the player when the spirit spawns
    [SerializeField] private float spiritForwardOffset = 3f;
    [SerializeField] private float spiritReturnRange = 3f;
    protected bool isSpirit = false;

    public virtual void Init(PlayerController characterController)
    {
        this.characterController = characterController;
        playerInputActions = new PlayerInputActions();
        
        // initialize list form and adding Spirit form as first form available (and shared by every macro class)
        forms = new List<PlayerForm>();

        //SpiritForm addedForm = gameObject.AddComponent<SpiritForm>();
        //forms.Add(addedForm);

        // adding shared abilities
        Dash dash = gameObject.AddComponent<Dash>();
        Dictionary<string, Ability> sharedAbilities = new Dictionary<string, Ability>();
        sharedAbilities[playerInputActions.Player.Dash.name] = dash;
        
        sharedAbilityHandler = gameObject.AddComponent<AbilityHandler>();
        sharedAbilityHandler.Init(sharedAbilities, characterController);
    }

    public virtual void BindAbilities()
    {
        if (!photonView.IsMine) return;
        
        playerInputActions.Player.Spirit.performed += ctx => ToggleSpiritForm();

        playerInputActions.Player.Dash.started += CastSharedAbility;
        playerInputActions.Player.Dash.performed += CastSharedAbility;
        playerInputActions.Player.Dash.canceled += CastSharedAbility;

        playerInputActions.Player.LightAttack.started += CastAbility;
        playerInputActions.Player.LightAttack.performed += CastAbility;
        playerInputActions.Player.LightAttack.canceled += CastAbility;

        playerInputActions.Player.HeavyAttack.started += CastAbility;
        playerInputActions.Player.HeavyAttack.performed += CastAbility;
        playerInputActions.Player.HeavyAttack.canceled += CastAbility;

        playerInputActions.Player.Ability1.started += CastAbility;
        playerInputActions.Player.Ability1.performed += CastAbility;
        playerInputActions.Player.Ability1.canceled += CastAbility;

        playerInputActions.Player.Ability2.started += CastAbility;
        playerInputActions.Player.Ability2.performed += CastAbility;
        playerInputActions.Player.Ability2.canceled += CastAbility;
    }

    public virtual void UnbindAbilities()
    {
        if (!photonView.IsMine) return;

        playerInputActions.Player.Spirit.performed -= ctx => ToggleSpiritForm();

        playerInputActions.Player.Dash.started -= CastSharedAbility;
        playerInputActions.Player.Dash.performed -= CastSharedAbility;
        playerInputActions.Player.Dash.canceled -= CastSharedAbility;

        playerInputActions.Player.LightAttack.started -= CastAbility;
        playerInputActions.Player.LightAttack.performed -= CastAbility;
        playerInputActions.Player.LightAttack.canceled -= CastAbility;

        playerInputActions.Player.HeavyAttack.started -= CastAbility;
        playerInputActions.Player.HeavyAttack.performed -= CastAbility;
        playerInputActions.Player.HeavyAttack.canceled -= CastAbility;

        playerInputActions.Player.Ability1.started -= CastAbility;
        playerInputActions.Player.Ability1.performed -= CastAbility;
        playerInputActions.Player.Ability1.canceled -= CastAbility;

        playerInputActions.Player.Ability2.started -= CastAbility;
        playerInputActions.Player.Ability2.performed -= CastAbility;
        playerInputActions.Player.Ability2.canceled -= CastAbility;
    }

    public virtual void EnableAbilities()
    {
        if (!photonView.IsMine) return;

        playerInputActions.Player.Enable();
        playerInputActions.Player.LightAttack.Enable();
        playerInputActions.Player.HeavyAttack.Enable();
        playerInputActions.Player.Dash.Enable();
        playerInputActions.Player.Transformation1.Enable();
        playerInputActions.Player.Transformation2.Enable();
        playerInputActions.Player.Spirit.Enable();
        playerInputActions.Player.Ability1.Enable();
        playerInputActions.Player.Ability2.Enable();
        Debug.Log("abilities enabled");
    }

    public virtual void DisableAbilities()
    {
        if (!photonView.IsMine) return;

        playerInputActions.Player.LightAttack.Disable();
        playerInputActions.Player.HeavyAttack.Disable();
        playerInputActions.Player.Dash.Disable();
        playerInputActions.Player.Transformation1.Disable();
        playerInputActions.Player.Transformation2.Disable();
        playerInputActions.Player.Spirit.Disable();
        playerInputActions.Player.Ability1.Disable();
        playerInputActions.Player.Ability2.Disable();
    }

    public void SwitchForm(int index, bool enableAbilities = true)
    {
        photonView.RPC("RpcSwitchForm", RpcTarget.All, index, enableAbilities);
    }

    [PunRPC]
    public void RpcSwitchForm(int index, bool enableAbilities = true)
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
        
        EnableAbilities();
 
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
        AbilityHandler abilityHandler = isSharedAbility ? sharedAbilityHandler : currentForm.abilityHandler;
        
        if (isStarted) abilityHandler.StartAbility(actionName);
        else if (isPerformed) abilityHandler.PerformAbility(actionName);
        else if (isCanceled) abilityHandler.CancelAbility(actionName);
    }

    public void ToggleSpiritForm()
    {
        if (isSpirit) EnterBody();
        else ExitBody();
    }

    private void ExitBody()
    {
        GameObject spirit = PhotonNetwork.Instantiate("Prefabs/SpiritCharacter", transform.position + spiritForwardOffset * transform.forward, transform.rotation);
        PlayerController spiritController = spirit.GetComponent<PlayerController>();
        DisableAbilities();
        
        characterController.Exit();
        StartCoroutine(LateInit(spiritController));
        spiritController.Init();
    }

    private void EnterBody()
    {
        Debug.Log("Trying to enter body");

        // check if the body is in range
        PlayerController myBody = null;
        Collider[] playersHit = Physics.OverlapSphere(transform.position, spiritReturnRange);
        foreach (Collider playerHit in playersHit)
        {
            if (playerHit.CompareTag("Player"))
            {
                PlayerController pc = playerHit.GetComponent<PlayerController>();
                if (!pc.photonView.IsMine || pc.formManager.isSpirit) continue;
                myBody = pc;
                break;
            }
        }
        if (myBody == null) return; // body not found, abort

        myBody.Init();
        currentForm.RemoveComponents();
        characterController.Exit();
        PhotonNetwork.Destroy(gameObject); // exit not needed since i'm destroying this go
    }

    private IEnumerator LateInit(PlayerController pc)
    {
        // some vfx to cover this late init
        yield return new WaitForSeconds(1f);
        pc.Init();
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, 3f);
    }
}
