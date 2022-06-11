using System.Collections;
using TMPro;
using UnityEngine;

public class HudMessage : MonoBehaviour
{
    private TextMeshProUGUI textComponent;

    private void Awake() => textComponent = gameObject.GetComponent<TextMeshProUGUI>();
    
    public void Init(string text, float ttl = 2f)
    {
        textComponent.text = text;
        StartCoroutine(FadeInAndOut(ttl));
    }

    #region Fading Animation
    private IEnumerator FadeInAndOut(float ttl)
    {
        yield return StartCoroutine(FadeIn());
        yield return new WaitForSeconds(ttl);
        yield return StartCoroutine(FadeOut());
        Destroy(gameObject);
    }

    private IEnumerator FadeIn()
    {
        textComponent.color = new Color(textComponent.color.r, textComponent.color.g, textComponent.color.b, 0);
        while (textComponent.color.a < 1.0f)
        {
            textComponent.color = new Color(textComponent.color.r, textComponent.color.g, textComponent.color.b,
                textComponent.color.a + (Time.deltaTime * 2f));
            yield return null;
        }
    }

    private IEnumerator FadeOut()
    {
        textComponent.color = new Color(textComponent.color.r, textComponent.color.g, textComponent.color.b, 1);
        while (textComponent.color.a > 0.0f)
        {
            textComponent.color = new Color(textComponent.color.r, textComponent.color.g, textComponent.color.b,
                textComponent.color.a - (Time.deltaTime * 2f));
            yield return null;
        }
    }
    #endregion
}