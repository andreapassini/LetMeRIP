using System.Collections;
using UnityEngine;
using UnityEngine.UI;


    public class HudAbility : MonoBehaviour
    {
        private Image iconImage;

        private void Awake()
        {
            iconImage = gameObject.transform.Find("Skill").GetComponent<Image>();
        }

        public void Init(Sprite abilityIcon, Ability ability)
        {
            iconImage.sprite = abilityIcon;
            ability.OnCooldownStart += StartCooldown;
        }

        private void StartCooldown(float cooldown)
        {
            

            StartCoroutine(Cooldown(cooldown));
        }

        private void EndCooldown()
        {
        }


        private IEnumerator Cooldown(float cooldown)
        {
            Debug.Log("Cooldown Started");
            
            iconImage.color = Color.black;
            yield return new WaitForSeconds(cooldown);
            iconImage.color = Color.white;
            
            Debug.Log("Cooldown ended");
        }
    }
