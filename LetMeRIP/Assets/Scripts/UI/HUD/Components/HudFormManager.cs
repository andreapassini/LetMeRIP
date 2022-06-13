using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class HudFormManager : MonoBehaviour
{
    // sprites of the transformations
    // private Dictionary<HudEForm, Sprite> sprites;
    private Dictionary<HudEForm, GameObject> hudFormsGO;
    
    private HudEForm m_CurrentHudEForm;

    private void Awake()
    {
        hudFormsGO = new Dictionary<HudEForm, GameObject>();
        foreach (HudEForm form in EnumUtils.GetValues<HudEForm>())
        {
            Transform formTransform = transform.Find(form.ToString())?.Find("Skill");

            if (formTransform == null) throw new Exception("The hudEForm " + form + " is not available");

            hudFormsGO[form] = formTransform.gameObject;
        }
    }

    public void Init(HudEForm initialHudEForm, Dictionary<HudEForm, Sprite> sprites)
    {
        // this.sprites = sprites;

        foreach (var sprite in sprites)
            if (hudFormsGO.ContainsKey(sprite.Key))
                hudFormsGO[sprite.Key].GetComponent<Image>().sprite = sprite.Value;

        //TODO temporary
        m_CurrentHudEForm = HudEForm.Base;
        changeForm(initialHudEForm);
    }

    public void changeForm(HudEForm newHudEForm)
    {
        if (m_CurrentHudEForm != newHudEForm)
            hudFormsGO[m_CurrentHudEForm].transform.parent.transform.localPosition += new Vector3(0, -15, 0);
        
        hudFormsGO[newHudEForm].transform.parent.transform.localPosition += new Vector3(0, 15, 0);
        
        m_CurrentHudEForm = newHudEForm;
    }
}