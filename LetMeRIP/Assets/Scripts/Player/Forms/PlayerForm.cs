using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerForm : MonoBehaviour
{
    public string FormName { get => formName; }
    protected string formName;

    protected PlayerInputActions playerInputActions;
    public AbilityHandler abilityHandler;
    protected Dictionary<string, Ability> abilities;
    protected GameObject formModelPrefab; // contains the model and the animator of the transformation
    protected CharacterController characterController;

    private void Awake()
    {
        abilities = new Dictionary<string, Ability>();
        playerInputActions = new PlayerInputActions();
    }

    public virtual void Init(CharacterController characterController) 
    {
        this.characterController = characterController;
    }

    public void RemoveComponents()
    {
        foreach (Ability ability in abilities.Values)
            Destroy(ability);

        if(abilityHandler != null)
            Destroy(abilityHandler);
        Debug.Log($"Destroying {formModelPrefab.name}(Clone)");
        GameObject modelToDestroy = transform.Find($"{formModelPrefab.name}(Clone)").gameObject;
        modelToDestroy.SetActive(false);
        Destroy(modelToDestroy);
    }
}
