using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HudAbility : MonoBehaviour
{
    private Image iconImage;
    private Image cooldownIndicatorImage;
    private float cooldown;
    private bool coolingDown;
    private Ability ability;
    
    
    public int id;

    private void Awake()
    {
        iconImage = gameObject.transform.Find("Skill").GetComponent<Image>();
        cooldownIndicatorImage = gameObject.transform.Find("CooldownIndicator").GetComponent<Image>();

        cooldownIndicatorImage.fillAmount = 0;
    }

    public void Init(Sprite abilityIcon, Ability ability)
    {
        id = GetInstanceID();
        this.ability = ability;
        
        Debug.LogError($"ID:{this.GetInstanceID()} Reinitializing ability: {ability}");
        iconImage.sprite = abilityIcon;
        ability.OnCooldownStart += StartCooldown;
    }

    public void Destroy()
    {
        ability.OnCooldownStart -= StartCooldown;
        Destroy(this);
    }

    private void StartCooldown(float cooldown)
    {
        Debug.LogError($"ID:{this.GetInstanceID()} Starting Cooldown");
        Debug.LogError(
            $"cooldown {this.cooldown}, fillAmount {cooldownIndicatorImage.fillAmount}, coolingdown {this.coolingDown}");
        
        
        this.cooldown = cooldown;

        cooldownIndicatorImage.fillAmount = 1;
        this.coolingDown = true;
        StartCoroutine(Cooldown());
    }

    private void EndCooldown()
    {
        coolingDown = false;
    }

    private void Update()
    {
        if (coolingDown)
        {
            //Reduce fill amount over 30 seconds
            cooldownIndicatorImage.fillAmount -= 1.0f / cooldown * Time.deltaTime;
        }
    }
    
    private IEnumerator Cooldown()
    {
        yield return new WaitForSeconds(cooldown);
        Debug.LogError("CIAO DAL COOL");
        
        EndCooldown();
    }
}