using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "UIStyle", menuName = "VR Tools/UI Style")]
public class UIStyleConfig : ScriptableObject
{
    [Header("Colors")]
    public Color buttonColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    public Color buttonTextColor = Color.white;
    public Color panelBgColor = new Color(0, 0, 0, 0.5f);
    public Color textColor = Color.white;
    public Color sliderFillColor = new Color(0.4f, 0.6f, 1f);
    public Color sliderBgColor = new Color(0.2f, 0.2f, 0.2f);

    [Header("Fonts")]
    public TMP_FontAsset mainFont; // если хочешь менять шрифт, перетащи сюда свой

    [Header("Sizes")]
    public float titleFontSize = 24f;
    public float labelFontSize = 16f;
    public float buttonFontSize = 14f;
    public Vector2 buttonSize = new Vector2(90, 40);
    public Vector2 sliderSize = new Vector2(210, 20);

    [Header("VR Positioning")]
    public float uiDistance = 1.2f;    // расстояние от камеры (метры)
    public Vector3 uiScale = new Vector3(0.003f, 0.003f, 0.003f); // общий масштаб
}