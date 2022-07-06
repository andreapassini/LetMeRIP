using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClassSelectionIndicator : MonoBehaviour
{
    [SerializeField] private List<Button> buttons;
    [SerializeField] private GameObject[] indicators;

    private void Awake()
    {
        if(buttons.Count != indicators.Length)
        {
            Debug.LogError("Class selector: buttons and indicators are not consistent");
            return;
        }

        foreach(Button button in buttons)
        {
            button.onClick.AddListener(() => { ShowIndicator(button); });
        }
    }

    public void ShowIndicator(Button button)
    {
        for (int i = 0; i < buttons.Count; i++) indicators[i].SetActive(false);
        indicators[buttons.IndexOf(button)].SetActive(true);
    }
}
