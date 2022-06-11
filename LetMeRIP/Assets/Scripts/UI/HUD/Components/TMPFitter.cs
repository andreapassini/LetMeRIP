using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TMPFitter : MonoBehaviour
{
    private TextMeshProUGUI TMProUGUI;
    private RectTransform rectTransform;
    private float preferredHeight;

    private void Awake()
    {
        TMProUGUI = transform.GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable() => SetHeight();
    private void Start() => SetHeight();

    private void Update()
    {
        if (Math.Abs(preferredHeight - TMProUGUI.preferredHeight) > 0.01f) SetHeight();
    }

    private void SetHeight()
    {
        if (TMProUGUI == null)
            return;

        preferredHeight = TMProUGUI.preferredHeight;
        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, preferredHeight);

        SetDirty();
    }


    #region MarkLayoutForRebuild

    private void SetDirty()
    {
        if (!isActiveAndEnabled) return;

        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
    }

    private void OnRectTransformDimensionsChange() => SetDirty();

    #endregion
}