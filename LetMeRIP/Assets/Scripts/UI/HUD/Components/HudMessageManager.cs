using UnityEngine;

public class HudMessageManager : MonoBehaviour
{
    public void PostMessage(string message, float ttl)
    {
        var messageGO = Instantiate(Resources.Load<GameObject>("Prefabs/HUD/HudMessage"), transform);
        var hudMessage = messageGO.GetComponent<HudMessage>();

        hudMessage.Init(message, ttl);
    }
}