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

        private HudStatusController statusController;
        
        private void Awake()
        {
            Instance = this;
        }

        public void Init(string playerClass, FormManager formManager)
        {
            statusController = playerClass switch
            {
                _ => gameObject.AddComponent<HudWarriorStatusController>()
            };

            // formManager.OnFormChanged += newFormManager => SwitchAbilities(newFormManager.currentForm.GetType().Name);
            formManager.OnFormChanged += newFormManager =>
            {
                Form newForm = newFormManager.currentForm.GetType().Name switch
                {
                    "SampleForm1" => Form.Trans1,
                    "SampleForm2" => Form.Trans2,
                    _ => Form.Trans1
                };
                
                Dictionary<string, Ability> ah = newFormManager.currentForm.abilityHandler.abilities;
                Dictionary<string, Ability> ah2 = newFormManager.sharedAbilityHandler.abilities;
                
                Dictionary<EAbility, Ability> abilities = new Dictionary<EAbility, Ability>();
                foreach (var entry in ah)
                {
                    Enum.TryParse(entry.Key, out EAbility ability);
                    abilities[ability] = entry.Value;
                }
                
                abilities[EAbility.Dash] = ah2["Dash"];
                
                var output = String.Join(", ", abilities.Select(res => "Key " + res.Key + ": VAL = " + res.Value));
                Debug.Log(output);
                
                statusController.changeForm(newForm, abilities);
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

        public void setSpiritMaxHealth(int maxHealth) => spiritHealth.SetMaxValue(maxHealth);
        public void setBodyMaxHealth(int maxHealth) => bodyHealth.SetMaxValue(maxHealth);
        public void setMaxSpiritGauge(int maxSpirits) => spiritGauge.SetMaxValue(maxSpirits);

        public void setSpiritHealth(int health) => spiritHealth.SetValue(health);
        public void setBodyHealth(int health) => spiritHealth.SetValue(health);
        public void setSpiritGauge(int spirits) => spiritHealth.SetValue(spirits);
    }
