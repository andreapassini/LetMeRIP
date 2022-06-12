using System.Collections.Generic;
using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    [SerializeField] private List<Menu> menus;

    private void Awake()
    {
        Instance = this;
        
        menus = new List<Menu>();
        foreach (Transform child in transform)
        {
            var menu = child.GetComponent<Menu>();
            if(menu is null) continue;
            menus.Add(menu);
            menu.gameObject.SetActive(false);
        }
        OpenMenu("loading");
    }

    public void OpenMenu(string menuName)
    {
        foreach (Menu menu in menus)
            if (menu.menuName == menuName)
            {
                OpenMenu(menu);
                break;
            }
    }

    public void OpenMenu(Menu menu)
    {
        if (menu == null) return;
        
        CloseAllMenus();
        menu.Open();
    }

    private void CloseMenu(Menu menu)
    {
        menu.Close();
    }

    private void CloseAllMenus()
    {
        foreach (Menu menu in menus)
            if (menu.isOpen) CloseMenu(menu);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}