using System.Collections;
using TMPro;
using UnityEngine;

public class HudRoomTimer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    private float remainingTime;
    private bool timerIsRunning;

    private void Awake() => gameObject.SetActive(false);


    void Update()
    {
        if (!timerIsRunning) return;

        if (remainingTime > 0)
        {
            remainingTime -= Time.deltaTime;
            timerText.text = remainingTime.ToString("0.0").Replace(",", ".");
        }
        else
        {
            Debug.Log("Time has run out");
            remainingTime = 0;
            timerIsRunning = false;
        }
    }

    public void Init(float totalTime)
    {
        gameObject.SetActive(true);

        this.remainingTime = totalTime;
        timerIsRunning = true;
    }
}