using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class HudFormManager : MonoBehaviour
{
    // sprites of the transformations
    // private Dictionary<Form, Sprite> sprites;
    private Dictionary<Form, GameObject> hudFormsGO;

    private Form currentForm;


    private Image currentBorder;

    private void Awake()
    {
        hudFormsGO = new Dictionary<Form, GameObject>();
        foreach (Form form in EnumUtils.GetValues<Form>())
        {
            Transform formTransform = transform.Find(form.ToString())?.Find("Skill");

            if (formTransform == null) throw new Exception("The form " + form + " is not available");

            hudFormsGO[form] = formTransform.gameObject;
        }
    }

    public void Init(Form initialForm, Dictionary<Form, Sprite> sprites)
    {
        // this.sprites = sprites;

        foreach (var sprite in sprites)
            if (hudFormsGO.ContainsKey(sprite.Key))
                hudFormsGO[sprite.Key].GetComponent<Image>().sprite = sprite.Value;

        //TODO temporary
        currentForm = Form.Base;
        changeForm(initialForm);
    }

    public void changeForm(Form newForm)
    {
        if (currentForm != newForm)
        {
            hudFormsGO[currentForm].transform.parent.transform.localPosition += new Vector3(0, -15, 0);
            hudFormsGO[currentForm].GetComponent<Image>().color = Color.cyan;
        }
        hudFormsGO[newForm].transform.parent.transform.localPosition += new Vector3(0, 15, 0);
        hudFormsGO[newForm].GetComponent<Image>().color = Color.white;

        
        currentForm = newForm;
    }
}