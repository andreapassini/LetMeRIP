using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


    public class HudController : MonoBehaviour
    {
        public static HudController Instance;

        [SerializeField] private FillingBar spiritHealth;
        [SerializeField] private FillingBar bodyHealth;
        [SerializeField] private FillingBar spiritGauge;

        private GameObject currentAbilities;
        [SerializeField] protected GameObject baseAbilities;
        [SerializeField] protected GameObject transform1Abilities;
        [SerializeField] protected GameObject transform2Abilities;

        private StatusController statusController;
        
        private void Awake()
        {
            Instance = this;
        }

        public void Init(string playerClass, FormManager formManager)
        {
            statusController = playerClass switch
            {
                _ => gameObject.AddComponent<WarriorStatusController>()
            };
            
            
            
            
                
                
            // formManager.OnFormChanged += newFormManager => SwitchAbilities(newFormManager.currentForm.GetType().Name);
            formManager.OnFormChanged += newFormManager =>
            {
                Dictionary<string, Ability> ah = newFormManager.currentForm.abilityHandler.abilities;
                
                

                Dictionary<EAbility, Ability> abilities = new Dictionary<EAbility, Ability>();
                foreach (var entry in ah)
                {
                    Enum.TryParse(entry.Key, out EAbility ability);
                    abilities[ability] = entry.Value;
                }
                
                
                
                
                var output = String.Join(", ", abilities.Select(res => "Key " + res.Key + ": VAL = " + res.Value));
                Debug.Log(output);
                
                statusController.changeForm(Form.Trans1, abilities);
            };
        }

        private void BindAbilities(string playerClass)
        {
            switch (playerClass)
            {
                case "sample":
                    // Instance = new SampleHudController();


                    break;
                default:
                    Debug.Log("HUD: Unknown Class " + playerClass);
                    break;
            }
        }


        // public void SwitchAbilities(string newForm)
        // {
        //     disableAbilities();
        //
        //     switch (newForm)
        //     {
        //         case "SpiritForm":
        //             Debug.Log("HUD: moving to SpiritForm");
        //             currentAbilities = baseAbilities;
        //             baseAbilities.SetActive(true);
        //             break;
        //         case "SampleForm1":
        //             Debug.Log("HUD: moving to SampleForm1");
        //             currentAbilities = transform1Abilities;
        //             transform1Abilities.SetActive(true);
        //             break;
        //         case "SampleForm2":
        //             Debug.Log("HUD: moving to SampleForm2");
        //             currentAbilities = transform2Abilities;
        //             transform2Abilities.SetActive(true);
        //             break;
        //         default:
        //             Debug.Log("HUD: Unknown Form " + newForm);
        //             break;
        //     }
        // }
        //
        // private void disableAbilities()
        // {
        //     if (currentAbilities != null) currentAbilities.SetActive(false);
        //     else
        //     {
        //         baseAbilities.SetActive(false);
        //         transform1Abilities.SetActive(false);
        //         transform2Abilities.SetActive(false);
        //     }
        // }

        public void setSpiritMaxHealth(int maxHealth) => spiritHealth.SetMaxValue(maxHealth);
        public void setBodyMaxHealth(int maxHealth) => bodyHealth.SetMaxValue(maxHealth);
        public void setMaxSpiritGauge(int maxSpirits) => spiritGauge.SetMaxValue(maxSpirits);

        public void setSpiritHealth(int health) => spiritHealth.SetValue(health);
        public void setBodyHealth(int health) => spiritHealth.SetValue(health);
        public void setSpiritGauge(int spirits) => spiritHealth.SetValue(spirits);
    }
