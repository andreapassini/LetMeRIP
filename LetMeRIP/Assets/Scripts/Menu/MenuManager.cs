using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    [SerializeField] private Menu[] menus;

    private void Awake()
    {
        Instance = this;
    }

    public void OpenMenu(string menuName)
    {
        foreach (Menu menu in menus)
            if (menu.menuName == menuName)
                OpenMenu(menu);
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
            if (menu.isOpen)
                CloseMenu(menu);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}