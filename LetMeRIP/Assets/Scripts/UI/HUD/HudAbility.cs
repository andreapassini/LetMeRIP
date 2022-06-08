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
            Debug.Log("Cooldown Started");

            StartCoroutine(Cooldown(cooldown));
        }

        private void EndCooldown()
        {
            Debug.Log("Cooldown ended");
        }


        private IEnumerator Cooldown(float cooldown)
        {
            yield return new WaitForSeconds(cooldown);
        }
    }
