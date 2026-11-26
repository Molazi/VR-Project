using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "UIStyle", menuName = "VR Tools/UI Style")]
public class UIStyleConfig : ScriptableObject
{
    [Header("Colors")]
    public Color buttonColor = new Color(0.2f, 0.3f, 0.3f, 0.8f);
    public Color buttonTextColor = Color.white;
    public Color panelBackground = new Color(0.15f, 0.17f, 0.22f);
    public Color textColor = Color.white;
    public Color disabledTextColor = new Color(0.6f, 0.6f, 0.6f);
    public Color dividerColor = new Color(0.3f, 0.4f, 0.5f);
    public Color successColor = new Color(0.2f, 0.8f, 0.35f);
    public Color warningColor = new Color(1f, 0.6f, 0.1f);
    public Color errorColor = new Color(0.9f, 0.2f, 0.2f);
    public Color accentColor = new Color(0.83f, 0.25f, 0.85f);
    public Color buttonNormal = panelBackground;
    public Color buttonHover = primaryColor;
    public Color buttonPressed = secondaryColor; = new Color(0.4f, 0.6f, 1f);
    public Color sliderBgColor = new Color(0.2f, 0.2f, 0.2f);

    [Header("Fonts")]
    public TMP_FontAsset mainFont; // если хочешь менять шрифт, перетащи сюда свой

    [Header("Sizes")]
    public float titleFontSize = 28f;
    public float labelFontSize = 14f;
    public float buttonFontSize = 14f;
    public Vector2 buttonSize = new Vector2(100, 44);
    public Vector2 sliderSize = new Vector2(180, 22);
    [Header("VR Positioning")]
    public float uiDistance = 1.2f;    // расстояние от камеры (метры)
    public Vector3 uiScale = new Vector3(0.003f, 0.003f, 0.003f); // общий масштаб
}