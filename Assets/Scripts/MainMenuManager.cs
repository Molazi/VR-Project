using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene")]
    public string sceneToLoad = "TerrainScene";

    [Header("UI Settings")]
    public Vector3 uiLocalPosition = new Vector3(0, 0, 2f);
    public Vector2 uiSize = new Vector2(800, 500);
    public Vector3 uiScale = new Vector3(0.003f, 0.003f, 0.003f);

    private GameObject uiRoot;

    void Start()
    {
        // Создаём EventSystem с XR UI Input Module, если нет
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            es.AddComponent<XRUIInputModule>();
        }
        else
        {
            var es = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (es.GetComponent<XRUIInputModule>() == null)
                es.gameObject.AddComponent<XRUIInputModule>();
        }

        CreateMenuUI();
    }

    void CreateMenuUI()
    {
        uiRoot = new GameObject("MainMenuUI", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        uiRoot.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = uiRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = uiSize;
        uiRoot.layer = LayerMask.NameToLayer("UI");
        AssignLayerRecursively(uiRoot, LayerMask.NameToLayer("UI"));

        Camera cam = Camera.main;
        if (cam != null)
        {
            uiRoot.transform.SetParent(cam.transform, false);
            uiRoot.transform.localPosition = uiLocalPosition;
            uiRoot.transform.localRotation = Quaternion.identity;
            uiRoot.transform.localScale = uiScale;
        }
        else
        {
            uiRoot.transform.position = new Vector3(0, 1.5f, 2f);
            uiRoot.transform.LookAt(Vector3.zero);
            uiRoot.transform.localScale = uiScale;
        }

        // Фон
        GameObject panel = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(uiRoot.transform, false);
        panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Заголовок
        CreateText("Title", "Редактор ландшафта", 48, TextAlignmentOptions.Center,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -80),
            new Vector2(-40, 80));

        // Кнопка "Войти"
        Button playBtn = CreateButton("PlayButton", "Войти в сцену",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 50),
            new Vector2(300, 80));
        playBtn.onClick.AddListener(PlayGame);

        // Кнопка "Выход"
        Button quitBtn = CreateButton("QuitButton", "Выход",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -50),
            new Vector2(300, 80));
        quitBtn.onClick.AddListener(QuitGame);
    }

    TMP_Text CreateText(string name, string text, int fontSize, TextAlignmentOptions alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(uiRoot.transform, false);
        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;

        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        return tmp;
    }

    Button CreateButton(string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(uiRoot.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        Button button = go.GetComponent<Button>();

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        TMP_Text tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.sizeDelta = Vector2.zero;

        return button;
    }

    void AssignLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            AssignLayerRecursively(child.gameObject, layer);
    }

    public void PlayGame()
    {
        Debug.Log("Play button pressed");
        SceneManager.LoadScene(sceneToLoad);
    }

    public void QuitGame()
    {
        Debug.Log("Quit button pressed");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}