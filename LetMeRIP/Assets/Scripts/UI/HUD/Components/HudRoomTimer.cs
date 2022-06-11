using TMPro;
using UnityEngine;

public class HudRoomTimer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    private float remainingTime;
    private bool timerIsRunning;

    private void Awake() => gameObject.SetActive(false);
    public void Hide() => gameObject.SetActive(false);


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