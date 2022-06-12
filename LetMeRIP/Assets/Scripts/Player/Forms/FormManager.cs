using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class FormManager : MonoBehaviourPun
{
    public int ViewID { get => photonView.ViewID; }

    public event Action<FormManager> OnFormChanged;
    public event Action<FormManager> OnBodyExit;
    public static event Action<FormManager> OnBodyExitForEnemy;

    [HideInInspector] public List<PlayerForm> forms;
    [HideInInspector] public PlayerForm currentForm;

    protected PlayerInputActions playerInputActions;
    public AbilityHandler sharedAbilityHandler; // handler of shared abilities, like dash and interact
    protected PlayerController characterController;

    // units of distance from the player when the spirit spawns
    [SerializeField] private float spiritForwardOffset = 2f;
    [SerializeField] private float spiritReturnRange = 3f;
    protected bool isSpirit = false;

    private Rigidbody rb;
    public bool IsSpirit { get => isSpirit; }
    protected bool isOut;
    public bool IsOut { get { return isOut; } }

    public virtual void Init(PlayerController characterController)
    {
        this.characterController = characterController;
        isSpirit = characterController.playerClass.ToLower().Equals("spirit");
        isOut = isSpirit;

        playerInputActions = new PlayerInputActions();
        rb = GetComponent<Rigidbody>();
        // initialize list form and adding Spirit form as first form available (and shared by every macro class)
        forms = new List<PlayerForm>();

        //SpiritForm addedForm = gameObject.AddComponent<SpiritForm>();
        //forms.Add(addedForm);

        // adding shared abilities
        Dash dash = gameObject.AddComponent<Dash>();
        Interact interact = gameObject.AddComponent<Interact>();

        Dictionary<string, Ability> sharedAbilities = new Dictionary<string, Ability>();

        sharedAbilities[playerInputActions.Player.Dash.name] = dash;
        sharedAbilities[playerInputActions.Player.Interact.name] = interact;

        sharedAbilityHandler = gameObject.AddComponent<AbilityHandler>();
        sharedAbilityHandler.Init(sharedAbilities, characterController);
        characterController.movement.Init();
    }

    public virtual void BindAbilities()
    {
        if (!photonView.IsMine) return;

        playerInputActions.Player.Spirit.performed += ctx => ToggleSpiritForm();

        playerInputActions.Player.Interact.started += CastSharedAbility;
        playerInputActions.Player.Interact.performed += CastSharedAbility;
        playerInputActions.Player.Interact.canceled += CastSharedAbility;

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

        playerInputActions.Player.Interact.started -= CastSharedAbility;
        playerInputActions.Player.Interact.performed -= CastSharedAbility;
        playerInputActions.Player.Interact.canceled -= CastSharedAbility;

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
        playerInputActions.Player.Interact.Enable();
        playerInputActions.Player.LightAttack.Enable();
        playerInputActions.Player.HeavyAttack.Enable();
        playerInputActions.Player.Dash.Enable();
        playerInputActions.Player.Transformation1.Enable();
        playerInputActions.Player.Transformation2.Enable();
        playerInputActions.Player.Spirit.Enable();
        playerInputActions.Player.Ability1.Enable();
        playerInputActions.Player.Ability2.Enable();
    }

    public virtual void DisableAbilities()
    {
        if (!photonView.IsMine) return;

        playerInputActions.Player.Disable();
        playerInputActions.Player.Interact.Disable();
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

        float spawnDistance = spiritForwardOffset;
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit info, 50f))
        {
            if (info.collider.CompareTag("Obstacle") && (transform.position - info.transform.position).magnitude < 4f)
                spawnDistance *= -1;
        }

        GameObject spirit = PhotonNetwork.Instantiate("Prefabs/SpiritCharacter", transform.position + spawnDistance * transform.forward, transform.rotation);
        DisableAbilities();
        isOut = true;
        rb.velocity = Vector3.zero;
        Animator animator = GetComponentInChildren<Animator>();
        if(animator != null) animator.SetBool("isRunning", false);
        characterController.Exit();

        isOut = true;
        OnBodyExit?.Invoke(this);
        OnBodyExitForEnemy?.Invoke(this);
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
        myBody.formManager.OnFormChanged?.Invoke(myBody.formManager);
        characterController.playerManager.bodyStats.spiritGauge = characterController.SGManager.SpiritGauge; // transfer new value of sg
        isOut = false;
        myBody.formManager.OnBodyExit?.Invoke(this);
        OnBodyExitForEnemy?.Invoke(this);

        currentForm.RemoveComponents();
        characterController.Exit();
        PhotonNetwork.Destroy(gameObject); // exit not needed since i'm destroying this go
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, 3f);
    }
}
