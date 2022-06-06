using UnityEngine;
using UnityEngine.Serialization;

public class Menu : MonoBehaviour
{
    public string menuName;
    public bool isOpen;

    public void Open()
    {
        isOpen = true;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        isOpen = false;
        gameObject.SetActive(false);
    }
}