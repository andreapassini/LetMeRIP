using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHudController : MonoBehaviour
{
    public void Hide() => HudController.Instance.Hide();
    public void Show() => HudController.Instance.Show();
}
