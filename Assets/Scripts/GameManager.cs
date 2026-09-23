using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class GameManager : MonoBehaviour
{
    // ========== Water ==========
    [Header("Water Settings")]
    public float waterEdgeOffset = 0.02f;
    public ProceduralWaterMesh waterPrefab;
    public float waterMaxRadius = 5f;
    public float waterDepthThreshold = 0.05f;
    public int waterBoundaryRays = 128;
    public float waterCleanupRadius = 5f;

    // ========== Tools ==========
    public UIStyleConfig uiStyle;
    public float maxDistance = 80f;
    public enum ToolMode { Dig, Raise, Paint, Water }
    public ToolMode currentMode = ToolMode.Dig;
    public float strength = 0.8f;
    public float brushSize = 2.5f;
    public int paintLayer = 0;
    public float paintStrength = 0.5f;
    public float actionInterval = 0.05f;

    // ========== Target Terrain ==========
    public Terrain targetTerrain;

    // ========== Particles Settings ==========
    [Header("Particles Settings")]
    public int digParticleCount = 12;
    public float digParticleSize = 0.05f;
    public float digParticleForce = 0.8f;
    public int raiseParticleCount = 12;
    public float raiseParticleSize = 0.05f;
    public float raiseParticleForce = 0.8f;
    public int paintParticleCount = 20;
    public float paintParticleSize = 0.04f;
    public float paintParticleForce = 0.8f;
    public int waterParticleCount = 50;
    public float waterParticleSize = 0.1f;
    public float waterParticleForce = 3.5f;

    // ========== Луч ==========
    [Header("Ray Settings")]
    public float rayDownAngle = 20f;
    [Header("Laser Visual")]
    public Color laserColor = Color.red;
    public float laserWidth = 0.02f;

    // ========== Activation (Grip) ==========
    public InputActionProperty activateAction;
    public InputActionProperty toggleUIAction;

    // ========== Высотные слои ==========
    [Header("Высотные слои текстур")]
    public int grassLayerIndex = 0;
    public int soilLayerIndex = 1;
    public int rockLayerIndex = 2;
    public float rockHeight = 200f;
    public float soilHeight = 400f;

    // ========== UI ==========
    private GameObject uiRoot;
    private bool uiActive = false;
    private TMP_Text modeText;
    private TMP_Text strengthText;
    private TMP_Text brushSizeText;
    private TMP_Text paintLayerText;
    private TMP_Text paintStrengthText;
    private Button digButton, raiseButton, paintButton, waterButton;
    private Slider strengthSlider, brushSizeSlider, paintStrengthSlider;
    private Button prevLayerButton, nextLayerButton;
    private Button clearWaterButton;
    private Button undoWaterButton;
    private List<ProceduralWaterMesh> waterHistory = new List<ProceduralWaterMesh>();

    private float lastActionTime = 0f;
    public float uiDistance = 1.8f;
    public Vector3 uiScale = new Vector3(0.004f, 0.004f, 0.004f);
    private float lastUIDistance;
    private Vector3 lastUIScale;

    private LineRenderer laserLine;
    private Transform rightController;

    private float[,] originalHeights;
    private float[,,] originalAlphamaps;
    private TerrainData currentTerrainData;

    private Vector2 strengthTextOrigPos, strengthSliderOrigPos;
    private Vector2 paintStrengthTextOrigPos, paintStrengthSliderOrigPos;
    private Vector2 prevLayerOrigPos, nextLayerOrigPos, layerTextOrigPos;

    void Awake()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetTerrain != null) currentTerrainData = targetTerrain.terrainData;
        CreateUI();
        if (uiRoot != null) uiRoot.SetActive(false);
        lastUIDistance = uiDistance;
        lastUIScale = uiScale;
        CreateLaser();
    }

    void Start()
    {
        if (currentTerrainData != null)
        {
            int res = currentTerrainData.heightmapResolution;
            originalHeights = currentTerrainData.GetHeights(0, 0, res, res);
            int alphaRes = currentTerrainData.alphamapResolution;
            int layers = currentTerrainData.alphamapLayers;
            originalAlphamaps = currentTerrainData.GetAlphamaps(0, 0, alphaRes, alphaRes);
        }
        ResetTerrainToOriginal();
    }

    void OnDestroy() => ResetTerrainToOriginal();

    void ResetTerrainToOriginal()
    {
        if (currentTerrainData == null || originalHeights == null) return;
        currentTerrainData.SetHeights(0, 0, originalHeights);
        if (originalAlphamaps != null) currentTerrainData.SetAlphamaps(0, 0, originalAlphamaps);
        currentTerrainData.SyncHeightmap();
    }

    void CreateLaser()
    {
        GameObject obj = new GameObject("Laser");
        obj.transform.SetParent(transform);
        laserLine = obj.AddComponent<LineRenderer>();
        laserLine.startWidth = laserWidth;
        laserLine.endWidth = laserWidth;
        laserLine.material = new Material(Shader.Find("Sprites/Default"));
        laserLine.startColor = laserColor;
        laserLine.endColor = laserColor;
        laserLine.positionCount = 2;
        laserLine.enabled = false;
    }

    void Update()
    {
        if (toggleUIAction.action != null && toggleUIAction.action.WasPressedThisFrame())
            ToggleUI();

        bool grip = activateAction.action != null && activateAction.action.IsPressed();
        bool just = activateAction.action != null && activateAction.action.WasPressedThisFrame();

        UpdateLaser(grip);

        if (!uiActive)
        {
            if (currentMode == ToolMode.Water)
            {
                if (just)
                {
                    Transform controller = GetRightController();
                    if (controller != null) ExecuteAction(controller);
                }
            }
            else if (grip && Time.time - lastActionTime >= actionInterval)
            {
                Transform controller = GetRightController();
                if (controller != null)
                {
                    ExecuteAction(controller);
                    lastActionTime = Time.time;
                }
            }
        }

        if (uiActive && uiRoot != null)
        {
            if (uiDistance != lastUIDistance || uiScale != lastUIScale)
            {
                uiRoot.transform.localPosition = new Vector3(0, 0, uiDistance);
                uiRoot.transform.localScale = uiScale;
                lastUIDistance = uiDistance;
                lastUIScale = uiScale;
            }
        }

        UpdateUI();
    }

    void UpdateLaser(bool pressed)
    {
        if (!laserLine) return;
        Transform c = GetRightController();
        if (!c) { laserLine.enabled = false; return; }
        Vector3 dir = Quaternion.AngleAxis(rayDownAngle, c.right) * c.forward;
        Ray ray = new Ray(c.position, dir);
        int mask = 1 << LayerMask.NameToLayer("Terrain");
        if (mask == 0) mask = -1;
        if (pressed && Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask))
        {
            laserLine.enabled = true;
            laserLine.SetPosition(0, c.position);
            laserLine.SetPosition(1, hit.point);
        }
        else laserLine.enabled = false;
    }

    Transform GetRightController()
    {
        if (rightController && rightController.gameObject.activeInHierarchy) return rightController;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "RightHand Controller" || t.name == "Right Controller" || t.name.Contains("Right"))
            { rightController = t; break; }
        return rightController;
    }

    void ToggleUI()
    {
        if (!uiRoot) return;
        uiActive = !uiActive;
        if (uiActive)
        {
            Camera cam = Camera.main;
            if (cam)
            {
                uiRoot.transform.SetParent(cam.transform, false);
                uiRoot.transform.localPosition = new Vector3(0, 0, uiDistance);
                uiRoot.transform.localRotation = Quaternion.identity;
                uiRoot.transform.localScale = uiScale;
            }
            else
            {
                uiRoot.transform.SetParent(null);
                uiRoot.transform.position = new Vector3(0, 2, 1.5f);
                uiRoot.transform.LookAt(Vector3.zero);
            }
            uiRoot.SetActive(true);
        }
        else { uiRoot.SetActive(false); uiRoot.transform.SetParent(null); }
    }

    void ExecuteAction(Transform ctrl)
    {
        if (!targetTerrain) return;
        Vector3 dir = Quaternion.AngleAxis(rayDownAngle, ctrl.right) * ctrl.forward;
        int mask = 1 << LayerMask.NameToLayer("Terrain");
        if (mask == 0) mask = -1;
        Ray ray = new Ray(ctrl.position, dir);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask))
            if (hit.collider.GetComponent<Terrain>()) PerformAction(hit.point);
    }

    void PerformAction(Vector3 worldPos)
    {
        switch (currentMode)
        {
            case ToolMode.Dig:
                TerrainDeformer.Deform(targetTerrain, worldPos, -strength * Time.deltaTime, brushSize);
                PaintTerrainByHeight(worldPos);
                SpawnPhysicsParticles(worldPos, new Color(0.77f, 0.64f, 0.52f), digParticleCount, digParticleSize, 1.5f, digParticleForce);
                break;
            case ToolMode.Raise:
                TerrainDeformer.Deform(targetTerrain, worldPos, strength * Time.deltaTime, brushSize);
                PaintTerrainByHeight(worldPos);
                SpawnPhysicsParticles(worldPos, new Color(0.77f, 0.64f, 0.52f), raiseParticleCount, raiseParticleSize, 1.5f, raiseParticleForce);
                break;
            case ToolMode.Paint:
                TerrainDeformer.PaintTexture(targetTerrain, worldPos, brushSize, paintStrength, paintLayer);
                SpawnPhysicsParticles(worldPos, Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f), paintParticleCount, paintParticleSize, 2f, paintParticleForce);
                break;
            case ToolMode.Water:
                PourWater(worldPos);
                SpawnPhysicsParticles(worldPos, new Color(0.3f, 0.5f, 1f), waterParticleCount, waterParticleSize, 3f, waterParticleForce);
                break;
        }
    }

    void PourWater(Vector3 clickPoint)
    {
        if (!targetTerrain) return;
        float clickHeight = targetTerrain.SampleHeight(clickPoint);
        int rays = Mathf.Max(waterBoundaryRays, 64);
        float maxR = 50f, step = 0.05f;
        float[] maxHeights = new float[rays];
        List<Vector3> contour = new List<Vector3>();

        for (int i = 0; i < rays; i++)
        {
            float angle = i * Mathf.PI * 2f / rays;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float maxH = clickHeight;
            for (float d = 0; d < maxR; d += step * 4)
            {
                float h = targetTerrain.SampleHeight(clickPoint + dir * d);
                if (h > maxH) maxH = h;
            }
            maxHeights[i] = maxH;
        }

        float overflow = Mathf.Min(maxHeights);
        float waterLevel = overflow - waterEdgeOffset;
        if (waterLevel - clickHeight < 0.15f) return;

        for (int i = 0; i < rays; i++)
        {
            float angle = i * Mathf.PI * 2f / rays;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float prevDist = 0, prevHeight = clickHeight, dist = 0;
            bool found = false;
            while (dist < maxR)
            {
                Vector3 p = clickPoint + dir * dist;
                float h = targetTerrain.SampleHeight(p);
                if (h >= waterLevel)
                {
                    float t = (Mathf.Abs(h - prevHeight) > 0.0001f) ? (waterLevel - prevHeight) / (h - prevHeight) : 0;
                    Vector3 cp = clickPoint + dir * (prevDist + t * (dist - prevDist));
                    cp.y = waterLevel;
                    contour.Add(cp);
                    found = true;
                    break;
                }
                prevDist = dist; prevHeight = h;
                dist += step;
            }
            if (!found)
            {
                Vector3 fp = clickPoint + dir * maxR;
                fp.y = waterLevel;
                contour.Add(fp);
            }
        }

        if (contour.Count < 3) return;
        Vector3 center = new Vector3(clickPoint.x, waterLevel, clickPoint.z);
        RemoveOldWater(center, waterCleanupRadius);
        ProceduralWaterMesh water = Instantiate(waterPrefab, center, Quaternion.identity);
        water.transform.localScale = Vector3.one;
        water.BuildFromBoundary(center, contour.ToArray(), waterLevel);
        waterHistory.Add(water);
    }

    void RemoveOldWater(Vector3 pos, float radius)
    {
        waterHistory.RemoveAll(w => w == null);
        for (int i = waterHistory.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(pos, waterHistory[i].transform.position) <= radius)
            {
                Destroy(waterHistory[i].gameObject);
                waterHistory.RemoveAt(i);
            }
        }
    }

    void UndoLastWater()
    {
        for (int i = waterHistory.Count - 1; i >= 0; i--)
        {
            if (waterHistory[i] == null) { waterHistory.RemoveAt(i); continue; }
            Destroy(waterHistory[i].gameObject);
            waterHistory.RemoveAt(i);
            break;
        }
    }

    void ClearAllWater()
    {
        foreach (var w in FindObjectsOfType<ProceduralWaterMesh>()) Destroy(w.gameObject);
        waterHistory.Clear();
    }

    void SpawnPhysicsParticles(Vector3 pos, Color color, int count, float size, float lifetime, float force)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = pos + Random.insideUnitSphere * 0.05f;
            cube.transform.localScale = Vector3.one * size;
            cube.transform.rotation = Random.rotation;
            Rigidbody rb = cube.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.AddForce(Random.onUnitSphere * force, ForceMode.Impulse);
            Renderer rend = cube.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Unlit/Color"));
            rend.material.color = color;
            Destroy(cube, lifetime);
        }
    }

    void PaintTerrainByHeight(Vector3 worldPos)
    {
        if (!targetTerrain) return;
        TerrainData data = targetTerrain.terrainData;
        int w = data.alphamapWidth, h = data.alphamapHeight;
        Vector3 lp = worldPos - targetTerrain.transform.position;
        int cx = Mathf.RoundToInt(lp.x / data.size.x * (w - 1));
        int cy = Mathf.RoundToInt(lp.z / data.size.z * (h - 1));
        int rad = Mathf.Clamp(Mathf.CeilToInt(brushSize / data.size.x * w), 1, 50);
        int x0 = Mathf.Max(0, cx - rad), x1 = Mathf.Min(w - 1, cx + rad);
        int y0 = Mathf.Max(0, cy - rad), y1 = Mathf.Min(h - 1, cy + rad);
        int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
        if (bw <= 0 || bh <= 0) return;
        float[,,] alphas = data.GetAlphamaps(x0, y0, bw, bh);
        float[,] heights = data.GetHeights(x0, y0, bw, bh);
        float rockRel = rockHeight / data.size.y;
        float soilRel = soilHeight / data.size.y;
        for (int y = 0; y < bh; y++)
        {
            for (int x = 0; x < bw; x++)
            {
                float hgt = heights[y, x];
                int layer = hgt < rockRel ? rockLayerIndex : (hgt < soilRel ? soilLayerIndex : grassLayerIndex);
                for (int l = 0; l < data.alphamapLayers; l++) alphas[y, x, l] = (l == layer) ? 1f : 0f;
            }
        }
        data.SetAlphamaps(x0, y0, alphas);
    }

    string GetLayerName(int idx)
    {
        if (targetTerrain && targetTerrain.terrainData.terrainLayers.Length > idx)
            return "Слой-" + targetTerrain.terrainData.terrainLayers[idx].name;
        return "Слой " + idx;
    }

    // ===================== UI =====================
    void CreateUI()
    {
        uiRoot = new GameObject("FixedUI", typeof(RectTransform), typeof(Canvas));
        uiRoot.transform.SetParent(null);
        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        uiRoot.AddComponent<TrackedDeviceGraphicRaycaster>();
        RectTransform cr = uiRoot.GetComponent<RectTransform>();
        cr.sizeDelta = new Vector2(800, 500);
        uiRoot.layer = LayerMask.NameToLayer("UI");
        AssignLayerRecursively(uiRoot, uiRoot.layer);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(uiRoot.transform, false);
        panel.GetComponent<Image>().color = uiStyle?.panelBgColor ?? new Color(0, 0, 0, 0.5f);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.sizeDelta = Vector2.zero;

        modeText = CreateText("ModeText", uiRoot.transform, "Режим: Копание", 28, TextAlignmentOptions.Left);
        modeText.rectTransform.anchorMin = new Vector2(0, 1); modeText.rectTransform.anchorMax = new Vector2(0, 1);
        modeText.rectTransform.pivot = new Vector2(0, 1);
        modeText.rectTransform.anchoredPosition = new Vector2(30, -15);
        modeText.rectTransform.sizeDelta = new Vector2(350, 40);

        float lm = 30f, sw = 740f, lh = 28f, sh = 24f;
        float yPos = -70f, rs = 50f;

        brushSizeText = CreateText("BrushLabel", uiRoot.transform, "Кисть: 2.5", 20, TextAlignmentOptions.Left);
        brushSizeText.rectTransform.anchorMin = new Vector2(0, 1); brushSizeText.rectTransform.anchorMax = new Vector2(0, 1);
        brushSizeText.rectTransform.pivot = new Vector2(0, 1);
        brushSizeText.rectTransform.anchoredPosition = new Vector2(lm, yPos);
        brushSizeText.rectTransform.sizeDelta = new Vector2(250, lh);
        yPos -= lh + 8;
        brushSizeSlider = CreateSlider("BrushSlider", uiRoot.transform, new Vector2(lm, yPos), new Vector2(sw, sh), 0.1f, 10f, 2.5f);
        brushSizeSlider.onValueChanged.AddListener(v => brushSize = v);
        yPos -= sh + rs;

        strengthText = CreateText("StrengthLabel", uiRoot.transform, "Сила: 0.800", 20, TextAlignmentOptions.Left);
        strengthText.rectTransform.anchorMin = new Vector2(0, 1); strengthText.rectTransform.anchorMax = new Vector2(0, 1);
        strengthText.rectTransform.pivot = new Vector2(0, 1);
        strengthText.rectTransform.anchoredPosition = new Vector2(lm, yPos);
        strengthText.rectTransform.sizeDelta = new Vector2(250, lh);
        strengthTextOrigPos = strengthText.rectTransform.anchoredPosition;
        yPos -= lh + 8;
        strengthSlider = CreateSlider("StrengthSlider", uiRoot.transform, new Vector2(lm, yPos), new Vector2(sw, sh), 0.001f, 3f, 0.8f);
        strengthSlider.onValueChanged.AddListener(v => strength = v);
        strengthSliderOrigPos = strengthSlider.GetComponent<RectTransform>().anchoredPosition;
        yPos -= sh + rs;

        paintStrengthText = CreateText("PaintLabel", uiRoot.transform, "Краска: 0.50", 20, TextAlignmentOptions.Left);
        paintStrengthText.rectTransform.anchorMin = new Vector2(0, 1); paintStrengthText.rectTransform.anchorMax = new Vector2(0, 1);
        paintStrengthText.rectTransform.pivot = new Vector2(0, 1);
        paintStrengthText.rectTransform.anchoredPosition = new Vector2(lm, yPos);
        paintStrengthText.rectTransform.sizeDelta = new Vector2(250, lh);
        paintStrengthTextOrigPos = paintStrengthText.rectTransform.anchoredPosition;
        yPos -= lh + 8;
        paintStrengthSlider = CreateSlider("PaintSlider", uiRoot.transform, new Vector2(lm, yPos), new Vector2(sw, sh), 0f, 2f, 0.5f);
        paintStrengthSlider.onValueChanged.AddListener(v => paintStrength = v);
        paintStrengthSliderOrigPos = paintStrengthSlider.GetComponent<RectTransform>().anchoredPosition;
        yPos -= sh + 45f;

        float aw = 120f, ah = 55f;
        prevLayerButton = CreateButton("PrevLayer", uiRoot.transform, "◄", Vector2.zero, Vector2.zero);
        RectTransform pvr = prevLayerButton.GetComponent<RectTransform>();
        pvr.anchorMin = pvr.anchorMax = new Vector2(0, 1); pvr.pivot = new Vector2(0, 1);
        pvr.anchoredPosition = new Vector2(60, yPos); pvr.sizeDelta = new Vector2(aw, ah);
        prevLayerButton.onClick.AddListener(PrevPaintLayer); prevLayerOrigPos = pvr.anchoredPosition;

        nextLayerButton = CreateButton("NextLayer", uiRoot.transform, "►", Vector2.zero, Vector2.zero);
        RectTransform nxr = nextLayerButton.GetComponent<RectTransform>();
        nxr.anchorMin = nxr.anchorMax = new Vector2(1, 1); nxr.pivot = new Vector2(1, 1);
        nxr.anchoredPosition = new Vector2(-60, yPos); nxr.sizeDelta = new Vector2(aw, ah);
        nextLayerButton.onClick.AddListener(NextPaintLayer); nextLayerOrigPos = nxr.anchoredPosition;

        paintLayerText = CreateText("LayerText", uiRoot.transform, "Слой: 0", 22, TextAlignmentOptions.Center);
        paintLayerText.rectTransform.anchorMin = new Vector2(0, 1); paintLayerText.rectTransform.anchorMax = new Vector2(1, 1);
        paintLayerText.rectTransform.pivot = new Vector2(0.5f, 1);
        paintLayerText.rectTransform.anchoredPosition = new Vector2(0, yPos - 8);
        paintLayerText.rectTransform.sizeDelta = new Vector2(-(aw * 2 + 60), 34);
        layerTextOrigPos = paintLayerText.rectTransform.anchoredPosition;

        yPos -= ah + 20f;

        float mbw = 140f, mbh = 55f, bm = 25f, sx = 60f, spx = 30f;
        digButton = CreateModeButton("BtnDig", "Копать", new Vector2(sx, bm), new Vector2(mbw, mbh));
        raiseButton = CreateModeButton("BtnRaise", "Насыпать", new Vector2(sx + mbw + spx, bm), new Vector2(mbw, mbh));
        paintButton = CreateModeButton("BtnPaint", "Красить", new Vector2(sx + (mbw + spx) * 2, bm), new Vector2(mbw, mbh));
        waterButton = CreateModeButton("BtnWater", "Вода", new Vector2(sx + (mbw + spx) * 3, bm), new Vector2(mbw, mbh));

        digButton.onClick.AddListener(() => currentMode = ToolMode.Dig);
        raiseButton.onClick.AddListener(() => currentMode = ToolMode.Raise);
        paintButton.onClick.AddListener(() => currentMode = ToolMode.Paint);
        waterButton.onClick.AddListener(() => currentMode = ToolMode.Water);

        // Широкие кнопки воды
        float bigH = 65f, hp = 30f, gap = 10f;
        float baseY = bm + mbh + 10f;

        clearWaterButton = CreateButton("ClearAllWaterBtn", uiRoot.transform, "Удалить всю воду", Vector2.zero, Vector2.zero);
        RectTransform crt = clearWaterButton.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(1, 0); crt.pivot = new Vector2(0.5f, 0);
        crt.anchoredPosition = new Vector2(0, baseY);
        crt.sizeDelta = new Vector2(-hp * 2, bigH);
        clearWaterButton.onClick.AddListener(ClearAllWater);

        undoWaterButton = CreateButton("UndoLastWaterBtn", uiRoot.transform, "Отменить последнюю воду", Vector2.zero, Vector2.zero);
        RectTransform ur = undoWaterButton.GetComponent<RectTransform>();
        ur.anchorMin = new Vector2(0, 0); ur.anchorMax = new Vector2(1, 0); ur.pivot = new Vector2(0.5f, 0);
        ur.anchoredPosition = new Vector2(0, baseY + bigH + gap);
        ur.sizeDelta = new Vector2(-hp * 2, bigH);
        undoWaterButton.onClick.AddListener(UndoLastWater);
    }

    Button CreateModeButton(string name, string text, Vector2 pos, Vector2 size)
    {
        Button btn = CreateButton(name, uiRoot.transform, text, pos, size);
        RectTransform r = btn.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 0);
        r.pivot = new Vector2(0, 0);
        r.anchoredPosition = pos;
        return btn;
    }

    void AssignLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform t in obj.transform) AssignLayerRecursively(t.gameObject, layer);
    }

    TMP_Text CreateText(string name, Transform parent, string text, int size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = uiStyle?.labelFontSize ?? size; tmp.alignment = align;
        tmp.color = uiStyle?.textColor ?? Color.white;
        tmp.rectTransform.localScale = Vector3.one;
        return tmp;
    }

    Button CreateButton(string name, Transform parent, string text, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        go.GetComponent<Image>().color = uiStyle?.buttonColor ?? new Color(0.3f, 0.3f, 0.3f, 0.8f);
        Button btn = go.GetComponent<Button>();
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        TMP_Text tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = uiStyle?.buttonFontSize ?? 20; tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = uiStyle?.buttonTextColor ?? Color.white;
        tmp.rectTransform.anchorMin = Vector2.zero; tmp.rectTransform.anchorMax = Vector2.one; tmp.rectTransform.sizeDelta = Vector2.zero;
        return btn;
    }

    Slider CreateSlider(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, float min, float max, float val)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        Slider sl = go.GetComponent<Slider>();
        sl.minValue = min; sl.maxValue = max; sl.value = val;

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        bg.GetComponent<Image>().color = uiStyle?.sliderBgColor ?? new Color(0.2f, 0.2f, 0.2f);
        RectTransform bgr = bg.GetComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.sizeDelta = Vector2.zero;

        GameObject fa = new GameObject("Fill Area", typeof(RectTransform));
        fa.transform.SetParent(go.transform, false);
        RectTransform far = fa.GetComponent<RectTransform>();
        far.anchorMin = Vector2.zero; far.anchorMax = Vector2.one;
        far.sizeDelta = new Vector2(-30, 0); far.anchoredPosition = new Vector2(15, 0);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(fa.transform, false);
        fill.GetComponent<Image>().color = uiStyle?.sliderFillColor ?? new Color(0.4f, 0.6f, 1f);
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.sizeDelta = Vector2.zero;

        GameObject ha = new GameObject("Handle Slide Area", typeof(RectTransform));
        ha.transform.SetParent(go.transform, false);
        RectTransform har = ha.GetComponent<RectTransform>();
        har.anchorMin = Vector2.zero; har.anchorMax = Vector2.one; har.sizeDelta = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(ha.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        RectTransform hr = handle.GetComponent<RectTransform>();
        hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one; hr.sizeDelta = new Vector2(12, 24);

        sl.fillRect = fr; sl.handleRect = hr; sl.targetGraphic = handle.GetComponent<Image>();
        sl.transition = Selectable.Transition.None;
        return sl;
    }

    void UpdateUI()
    {
        string modeStr = currentMode switch { ToolMode.Dig => "Копание", ToolMode.Raise => "Насыпь", ToolMode.Paint => "Краска", ToolMode.Water => "Вода", _ => "" };
        if (modeText) modeText.text = $"Режим: {modeStr}";

        bool digRaise = currentMode is ToolMode.Dig or ToolMode.Raise;
        bool paint = currentMode == ToolMode.Paint;
        bool water = currentMode == ToolMode.Water;

        if (brushSizeText) brushSizeText.gameObject.SetActive(!water);
        if (brushSizeSlider) brushSizeSlider.gameObject.SetActive(!water);
        if (strengthText) strengthText.gameObject.SetActive(digRaise);
        if (strengthSlider) strengthSlider.gameObject.SetActive(digRaise);
        if (paintStrengthText) paintStrengthText.gameObject.SetActive(paint);
        if (paintStrengthSlider) paintStrengthSlider.gameObject.SetActive(paint);
        if (paintLayerText) paintLayerText.gameObject.SetActive(paint);
        if (prevLayerButton) prevLayerButton.gameObject.SetActive(paint);
        if (nextLayerButton) nextLayerButton.gameObject.SetActive(paint);
        if (clearWaterButton) clearWaterButton.gameObject.SetActive(water);
        if (undoWaterButton) undoWaterButton.gameObject.SetActive(water);

        if (paint)
        {
            Vector2 shift = strengthTextOrigPos - paintStrengthTextOrigPos;
            paintStrengthText.rectTransform.anchoredPosition = paintStrengthTextOrigPos + shift;
            paintStrengthSlider.GetComponent<RectTransform>().anchoredPosition = paintStrengthSliderOrigPos + shift;
            prevLayerButton.GetComponent<RectTransform>().anchoredPosition = prevLayerOrigPos + shift;
            nextLayerButton.GetComponent<RectTransform>().anchoredPosition = nextLayerOrigPos + shift;
            paintLayerText.rectTransform.anchoredPosition = layerTextOrigPos + shift;
        }
        else
        {
            paintStrengthText.rectTransform.anchoredPosition = paintStrengthTextOrigPos;
            paintStrengthSlider.GetComponent<RectTransform>().anchoredPosition = paintStrengthSliderOrigPos;
            prevLayerButton.GetComponent<RectTransform>().anchoredPosition = prevLayerOrigPos;
            nextLayerButton.GetComponent<RectTransform>().anchoredPosition = nextLayerOrigPos;
            paintLayerText.rectTransform.anchoredPosition = layerTextOrigPos;
        }

        if (brushSizeText && !water) brushSizeText.text = $"Кисть: {brushSize:F1}";
        if (strengthText && digRaise) strengthText.text = $"Сила: {strength:F3}";
        if (paintStrengthText && paint) paintStrengthText.text = $"Краска: {paintStrength:F2}";
        if (paintLayerText && paint) paintLayerText.text = GetLayerName(paintLayer);
    }

    void PrevPaintLayer() { int cnt = targetTerrain?.terrainData.alphamapLayers ?? 1; paintLayer = (paintLayer - 1 + cnt) % cnt; }
    void NextPaintLayer() { int cnt = targetTerrain?.terrainData.alphamapLayers ?? 1; paintLayer = (paintLayer + 1) % cnt; }
}