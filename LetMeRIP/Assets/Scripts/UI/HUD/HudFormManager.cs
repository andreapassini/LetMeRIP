using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class HudFormManager: MonoBehaviour
    {
        // sprites of the transformations
        private Dictionary<Form, Sprite> sprites;
        private Dictionary<Form, GameObject> hudFormsGO;
        
        private void Awake()
        {
            Debug.Log("This is the form manager");
            Debug.Log("GO: ", transform);
            
            
            // populate abilities
            hudFormsGO = new Dictionary<Form, GameObject>();

            foreach (string formName in Enum.GetNames(typeof(Form)))
            {
                Enum.TryParse(formName, out Form ability);
                Transform formGO = transform.Find(formName).Find("Skill");

                if (formGO == null) throw new Exception("The form " + formName + " is not available");
            
                hudFormsGO[ability] = formGO.gameObject;
            }
            
            var output = String.Join(", ", hudFormsGO.Select(res => "Key " + res.Key + ": VAL = " + res.Value));
            Debug.Log(output);
        }

        public void Init(Dictionary<Form, Sprite> sprites, Form initialForm)
        {
            this.sprites = sprites;
            changeForm(initialForm);
        }

        public void changeForm(Form newForm) => hudFormsGO[newForm].GetComponent<Image>().sprite = sprites[newForm];
    }
