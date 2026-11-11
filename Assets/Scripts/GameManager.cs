using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class GameManager : MonoBehaviour
{
    // ========== Water ==========
    [Header("Water — Grid System")]
    public ProceduralWaterMesh waterPrefab;
    public float waterSearchRadius = 100f; // Радиус поиска ямы
    public float waterGridStep = 1.0f; // Шаг поиска уровня воды
    public float waterMinDepth = 0.02f;
    public float waterMeshStep = 0.5f; // Размер ячейки меша (чем меньше, тем плавнее края)

    // ========== Tools ==========
    public UIStyleConfig uiStyle;
    public float maxDistance = 80f;
    public enum ToolMode { Dig, Raise, Paint, Water, Placement }
    public ToolMode currentMode = ToolMode.Dig;
    public float strength = 0.8f;
    public float brushSize = 2.5f;
    public float smoothingStrength = 0.3f;
    public float minTerrainHeight = 10f;
    public float maxTerrainHeight = 190f;
    public int paintLayer = 0;
    public float paintStrength = 0.5f;
    public float actionInterval = 0.05f;

    // ========== Target Terrain ==========
    public Terrain targetTerrain;
    public MaterialProfileLibraryAsset materialLibraryAsset;
    private MaterialProfileLibrary materialLibrary;

    // ========== Object Placement ==========
    public GameObject placementPrefab;
    public Vector3 placementScale = Vector3.one;
    public float placementSink = 1f;
    public float placementIndicatorRadius = 0.5f;

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
    public Color digRingColor = new Color(1f, 0.45f, 0f);
    public Color raiseRingColor = new Color(0.15f, 1f, 0.3f);
    public Color paintRingColor = new Color(0.7f, 0.3f, 1f);
    public float laserWidth = 0.02f;

    // ========== Activation (Grip) ==========
    public InputActionProperty activateAction;
    public InputActionProperty toggleUIAction;
    public InputActionProperty undoAction;
    public InputActionProperty redoAction;
    public InputActionProperty brushBiggerAction;
    public InputActionProperty brushSmallerAction;
    public InputActionProperty strengthUpAction;
    public InputActionProperty strengthDownAction;
    public float brushButtonStep = 0.5f;
    public float strengthButtonStep = 0.1f;
    public float brushButtonHoldDelay = 0.35f;
    public float brushButtonHoldRate = 0.12f;
    private float brushBiggerNextTime;
    private float brushSmallerNextTime;
    private float strengthUpNextTime;
    private float strengthDownNextTime;

    // ========== Высотные слои ==========
    [Header("Высотные слои текстур")]
    public int grassLayerIndex = 0;
    public int soilLayerIndex = 1;
    public int rockLayerIndex = 2;
    public float rockHeight = 50f;
    public float soilHeight = 100f;

    // ========== UI ==========
    private GameObject uiRoot;
    private bool uiActive = false;
    private TMP_Text modeText;
    private TMP_Text strengthText;
    private TMP_Text brushSizeText;
    private TMP_Text paintLayerText;
    private TMP_Text paintStrengthText;
    private Button digButton, raiseButton, paintButton, waterButton, placementButton;
    private ToolMode lastHighlightMode = (ToolMode)(-1);
    private Slider strengthSlider, brushSizeSlider, paintStrengthSlider;
    private Button prevLayerButton, nextLayerButton;
    private Button clearWaterButton;

    private class WaterRecord
    {
        public ProceduralWaterMesh water;
        public WaterState state;
        public WaterRecord(ProceduralWaterMesh water, WaterState state) { this.water = water; this.state = state; }
    }
    private readonly List<WaterRecord> waterHistory = new();

    private float lastActionTime = 0f;
    public float uiDistance = 1.8f;
    public Vector3 uiScale = new Vector3(0.004f, 0.004f, 0.004f);
    private float lastUIDistance;
    private Vector3 lastUIScale;

    private LineRenderer laserLine;
    private Transform rightController;

    [Header("Brush Hint")]
    public bool showBrushHint = true;
    public float brushHintHeight = 0.15f;
    public float brushHintFontSize = 0.15f;
    public float brushHintMinAboveTerrain = 0.6f;
    public Vector3 brushHintControllerOffset = new Vector3(0f, 0.06f, 0f);
    private TextMeshPro brushHint;
    private string lastHintText = "";
    private ToolMode lastHintMode = (ToolMode)(-1);
    private float lastHintBrush = float.NaN;
    private float lastHintStrength = float.NaN;
    private float lastHintPaint = float.NaN;
    private int lastHintLayer = int.MinValue;

    private float[,] originalHeights;
    private float[,,] originalAlphamaps;
    private TerrainData currentTerrainData;

    private Vector2 strengthTextOrigPos, strengthSliderOrigPos;
    private Vector2 paintStrengthTextOrigPos, paintStrengthSliderOrigPos;
    private Vector2 prevLayerOrigPos, nextLayerOrigPos, layerTextOrigPos;

    private readonly ActionHistory actionHistory = new();
    private TerrainAction activeTerrainAction;

    void Awake()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetTerrain != null) currentTerrainData = targetTerrain.terrainData;

        if (materialLibraryAsset != null) materialLibrary = materialLibraryAsset.library;
        if (materialLibrary == null || materialLibrary.profiles == null || materialLibrary.profiles.Count == 0)
            materialLibrary = MaterialProfileLibrary.CreateDefault();

        if (targetTerrain != null) materialLibrary.ApplyAllToTerrain(targetTerrain);

        CreateUI();
        if (uiRoot != null) uiRoot.SetActive(false);
        lastUIDistance = uiDistance;
        lastUIScale = uiScale;
        CreateLaser();
        CreateBrushHint();
    }

    void Start()
    {
        if (currentTerrainData != null)
        {
            int res = currentTerrainData.heightmapResolution;
            originalHeights = currentTerrainData.GetHeights(0, 0, res, res);
            int alphaRes = currentTerrainData.alphamapResolution;
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
        Material laserMaterial = new Material(Shader.Find("Sprites/Default"));
        laserMaterial.SetInt("_ZTest", 8);
        laserMaterial.renderQueue = 4000;
        laserLine.material = laserMaterial;
        laserLine.startColor = laserColor;
        laserLine.endColor = laserColor;
        laserLine.positionCount = 2;
        laserLine.enabled = false;
    }

    void CreateBrushHint()
    {
        GameObject obj = new GameObject("BrushHint");
        obj.transform.SetParent(transform);
        brushHint = obj.AddComponent<TextMeshPro>();
        brushHint.fontSize = brushHintFontSize;
        brushHint.rectTransform.sizeDelta = new Vector2(1.2f, 0.25f);
        brushHint.alignment = TextAlignmentOptions.Center;
        brushHint.color = Color.white;
        brushHint.outlineWidth = 0.15f;
        brushHint.outlineColor = Color.black;
        Material hintMaterial = new Material(brushHint.fontSharedMaterial);
        hintMaterial.SetInt("_ZTest", 8);
        hintMaterial.SetInt("_ZTestMode", 8);
        hintMaterial.SetInt("unity_GUIZTestMode", 8);
        hintMaterial.renderQueue = 4000;
        hintMaterial.DisableKeyword("UNDERLAY_ON");
        brushHint.fontMaterial = hintMaterial;
        MeshRenderer hintRenderer = obj.GetComponent<MeshRenderer>();
        hintRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hintRenderer.receiveShadows = false;
        hintRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        hintRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        obj.SetActive(false);
    }

    void UpdateBrushHint(Vector3 brushCenter, bool brushVisible)
    {
        if (!brushHint) return;
        bool show = showBrushHint && currentMode != ToolMode.Water;
        brushHint.gameObject.SetActive(show);
        if (!show) return;

        bool changed = currentMode != lastHintMode || !Mathf.Approximately(brushSize, lastHintBrush) ||
                       !Mathf.Approximately(strength, lastHintStrength) || !Mathf.Approximately(paintStrength, lastHintPaint) ||
                       paintLayer != lastHintLayer;

        Transform leftController = FindLeftController();
        if (leftController != null)
        {
            brushHint.transform.SetParent(leftController, false);
            brushHint.transform.localPosition = brushHintControllerOffset;
            brushHint.transform.localRotation = Quaternion.identity;
        }
        else if (Camera.main != null)
        {
            brushHint.transform.SetParent(null);
            Camera cam = Camera.main;
            brushHint.transform.position = cam.transform.position + cam.transform.forward * 1.2f + cam.transform.up * -0.3f;
            brushHint.transform.rotation = Quaternion.LookRotation(brushHint.transform.position - cam.transform.position);
        }
        else
        {
            brushHint.transform.SetParent(transform);
            brushHint.transform.position = GetBrushHintPosition(brushCenter);
        }

        if (!changed && lastHintText != "") return;

        string text = currentMode switch
        {
            ToolMode.Dig => $"Копание  •  Кисть {brushSize:F1} м  •  Сила {strength:F2}",
            ToolMode.Raise => $"Насыпь  •  Кисть {brushSize:F1} м  •  Сила {strength:F2}",
            ToolMode.Paint => $"Краска  •  Кисть {brushSize:F1} м  •  {paintStrength:F2}  •  {GetLayerName(paintLayer)}",
            ToolMode.Placement => "Размещение  •  Нажмите для установки",
            _ => ""
        };

        brushHint.text = text;
        lastHintText = text;
        lastHintMode = currentMode;
        lastHintBrush = brushSize;
        lastHintStrength = strength;
        lastHintPaint = paintStrength;
        lastHintLayer = paintLayer;
    }

    private Transform cachedLeftController;
    private float leftControllerSearchTime;
    Transform FindLeftController()
    {
        if (cachedLeftController != null) return cachedLeftController;
        if (Time.time - leftControllerSearchTime < 1f) return null;
        leftControllerSearchTime = Time.time;
        string[] candidates = new string[] { "Left Controller", "LeftController", "Left Hand Controller", "LeftHand Controller" };
        foreach (string name in candidates)
        {
            GameObject found = GameObject.Find(name);
            if (found != null) { cachedLeftController = found.transform; return cachedLeftController; }
        }
        UnityEngine.XR.InputDevice leftHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (leftHand.isValid)
        {
            GameObject rig = GameObject.Find("XR Origin") ?? GameObject.Find("XROrigin") ?? GameObject.Find("XR Rig");
            if (rig != null)
            {
                Transform t = rig.transform.Find("Camera Offset/Left Controller");
                if (t == null) t = rig.transform.Find("Camera Offset/LeftHand Controller");
                if (t != null) cachedLeftController = t;
            }
        }
        return cachedLeftController;
    }

    Vector3 GetBrushHintPosition(Vector3 brushCenter)
    {
        float groundY = brushCenter.y;
        if (targetTerrain != null) groundY = targetTerrain.SampleHeight(brushCenter) + targetTerrain.transform.position.y;
        float hintY = Mathf.Max(brushCenter.y + brushHintHeight, groundY + brushHintMinAboveTerrain);
        return new Vector3(brushCenter.x, hintY, brushCenter.z);
    }

    void Update()
    {
        if (toggleUIAction.action != null && toggleUIAction.action.WasPressedThisFrame()) ToggleUI();
        bool grip = activateAction.action != null && activateAction.action.IsPressed();
        bool just = activateAction.action != null && activateAction.action.WasPressedThisFrame();

        UpdateLaser(grip);
        HandleBrushButtons();

        if (!uiActive)
        {
            if (currentMode is ToolMode.Water or ToolMode.Placement)
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
                    if (currentMode == ToolMode.Dig || currentMode == ToolMode.Raise || currentMode == ToolMode.Paint)
                    {
                        if (activeTerrainAction == null) activeTerrainAction = new TerrainAction();
                    }
                    ExecuteAction(controller);
                    lastActionTime = Time.time;
                }
            }
        }

        if (!grip) CompleteActiveActions();

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

        if (undoAction.action != null && undoAction.action.WasPressedThisFrame()) actionHistory.Undo();
        if (redoAction.action != null && redoAction.action.WasPressedThisFrame()) actionHistory.Redo();

        UpdateUI();
    }

    void HandleBrushButtons()
    {
        if (ApplyBrushButton(brushBiggerAction, ref brushBiggerNextTime)) ChangeBrushSize(brushButtonStep);
        if (ApplyBrushButton(brushSmallerAction, ref brushSmallerNextTime)) ChangeBrushSize(-brushButtonStep);
        if (ApplyBrushButton(strengthUpAction, ref strengthUpNextTime)) ChangeStrength(strengthButtonStep);
        if (ApplyBrushButton(strengthDownAction, ref strengthDownNextTime)) ChangeStrength(-strengthButtonStep);
    }

    bool ApplyBrushButton(InputActionProperty property, ref float nextTime)
    {
        if (property.action == null) return false;
        if (property.action.WasPressedThisFrame()) { nextTime = Time.time + brushButtonHoldDelay; return true; }
        if (property.action.IsPressed() && Time.time >= nextTime) { nextTime = Time.time + brushButtonHoldRate; return true; }
        return false;
    }

    void ChangeBrushSize(float delta)
    {
        brushSize = Mathf.Clamp(brushSize + delta, 0.1f, 10f);
        if (brushSizeSlider) brushSizeSlider.SetValueWithoutNotify(brushSize);
    }

    void ChangeStrength(float delta)
    {
        if (currentMode == ToolMode.Paint)
        {
            paintStrength = Mathf.Clamp(paintStrength + delta, 0f, 2f);
            if (paintStrengthSlider) paintStrengthSlider.SetValueWithoutNotify(paintStrength);
        }
        else
        {
            strength = Mathf.Clamp(strength + delta, 0.001f, 0.05f);
            if (strengthSlider) strengthSlider.SetValueWithoutNotify(strength);
        }
    }

    void UpdateLaser(bool pressed)
    {
        if (!laserLine) return;
        Transform controller = GetRightController();
        if (!controller) { laserLine.enabled = false; return; }

        Vector3 direction = Quaternion.AngleAxis(rayDownAngle, controller.right) * controller.forward;
        Ray ray = new Ray(controller.position, direction);
        int mask = 1 << LayerMask.NameToLayer("Terrain");
        if (mask == 0) mask = -1;

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask))
        {
            laserLine.enabled = false;
            UpdateBrushHint(Vector3.zero, false);
            return;
        }

        if (currentMode == ToolMode.Water) { laserLine.enabled = false; UpdateBrushHint(Vector3.zero, false); return; }
        if (currentMode == ToolMode.Placement) { DrawBrushIndicator(hit.point, placementIndicatorRadius); return; }
        DrawBrushIndicator(hit.point);
    }

    void DrawBrushIndicator(Vector3 center, float radius = -1f)
    {
        Terrain terrain = targetTerrain;
        if (!terrain) { laserLine.enabled = false; return; }

        const int segments = 48;
        const float heightOffset = 0.1f;
        laserLine.enabled = true;
        laserLine.useWorldSpace = true;
        laserLine.loop = true;
        laserLine.positionCount = segments;
        laserLine.widthMultiplier = 2f;

        Color ringColor = currentMode switch
        {
            ToolMode.Dig => digRingColor,
            ToolMode.Raise => raiseRingColor,
            ToolMode.Paint => paintRingColor,
            ToolMode.Water => new Color(0.2f, 0.6f, 1f),
            ToolMode.Placement => new Color(1f, 0.8f, 0.2f),
            _ => laserColor
        };
        laserLine.startColor = ringColor;
        laserLine.endColor = ringColor;

        float indicatorRadius = radius > 0f ? radius : brushSize;
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(angle) * indicatorRadius;
            float z = center.z + Mathf.Sin(angle) * indicatorRadius;
            Vector3 position = new Vector3(x, 0f, z);
            float terrainHeight = terrain.SampleHeight(position) + terrain.transform.position.y;
            position.y = terrainHeight + heightOffset;
            laserLine.SetPosition(i, position);
        }
        UpdateBrushHint(center, true);
    }

    Transform GetRightController()
    {
        if (rightController && rightController.gameObject.activeInHierarchy && (rightController.name == "RightHand Controller" || rightController.name == "Right Controller")) return rightController;
        rightController = null;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (t.name == "RightHand Controller" || t.name == "Right Controller") { rightController = t; break; }
        }
        return rightController;
    }

    void ToggleUI()
    {
        if (!uiRoot) return;
        uiActive = !uiActive;
        if (uiActive)
        {
            Camera cam = Camera.main;
            if (cam) { uiRoot.transform.SetParent(cam.transform, false); uiRoot.transform.localPosition = new Vector3(0, 0, uiDistance); uiRoot.transform.localRotation = Quaternion.identity; uiRoot.transform.localScale = uiScale; }
            else { uiRoot.transform.SetParent(null); uiRoot.transform.position = new Vector3(0, 2, 1.5f); uiRoot.transform.LookAt(Vector3.zero); }
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

    void ApplyDeformTool(Vector3 worldPos, float signedStrength, int particleCount, float particleSize, float particleForce)
    {
        activeTerrainAction.Record(targetTerrain, worldPos, brushSize, () => DeformTerrain(worldPos, signedStrength));
        SpawnPhysicsParticles(worldPos, new Color(0.77f, 0.64f, 0.52f), particleCount, particleSize, 1.5f, particleForce);
    }

    void DeformTerrain(Vector3 worldPos, float signedStrength)
    {
        TerrainDeformer.Deform(targetTerrain, worldPos, signedStrength, brushSize, minTerrainHeight, maxTerrainHeight, smoothingStrength);
        PaintTerrainByHeight(worldPos);
    }

    RectInt GetAlphaBoundsForPaint(Vector3 worldPos, float brushSize)
    {
        TerrainData data = targetTerrain.terrainData;
        Vector3 lp = worldPos - targetTerrain.transform.position;
        int w = data.alphamapWidth, h = data.alphamapHeight;
        int cx = Mathf.RoundToInt(lp.x / data.size.x * (w - 1));
        int cy = Mathf.RoundToInt(lp.z / data.size.z * (h - 1));
        int rad = Mathf.Clamp(Mathf.CeilToInt(brushSize / data.size.x * w), 1, 50);
        int x0 = Mathf.Max(0, cx - rad), x1 = Mathf.Min(w - 1, cx + rad);
        int y0 = Mathf.Max(0, cy - rad), y1 = Mathf.Min(h - 1, cy + rad);
        return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    void PerformAction(Vector3 worldPos)
    {
        switch (currentMode)
        {
            case ToolMode.Dig:
                ApplyDeformTool(worldPos, -strength * Time.deltaTime, digParticleCount, digParticleSize, digParticleForce);
                break;
            case ToolMode.Raise:
                ApplyDeformTool(worldPos, strength * Time.deltaTime, raiseParticleCount, raiseParticleSize, raiseParticleForce);
                break;
            case ToolMode.Paint:
                {
                    Vector3 wp = worldPos;
                    activeTerrainAction.Record(targetTerrain, wp, brushSize, () =>
                    {
                        TerrainData data = targetTerrain.terrainData;
                        RectInt bounds = GetAlphaBoundsForPaint(wp, brushSize);
                        float[,,] oldAlphas = data.GetAlphamaps(bounds.x, bounds.y, bounds.width, bounds.height);
                        TerrainDeformer.PaintTexture(targetTerrain, wp, brushSize, paintStrength, paintLayer);
                        float[,,] newAlphas = data.GetAlphamaps(bounds.x, bounds.y, bounds.width, bounds.height);
                        TerrainMaterialShaper.ApplyAlphaHeightDelta(targetTerrain, bounds, oldAlphas, newAlphas, materialLibrary.profiles);
                    });
                    SpawnPhysicsParticles(wp, Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f), paintParticleCount, paintParticleSize, 2f, paintParticleForce);
                    break;
                }
            case ToolMode.Water:
                {
                    CompleteActiveActions();
                    List<WaterState> before = CaptureWaterStates();
                    PourWater(worldPos);
                    List<WaterState> after = CaptureWaterStates();
                    if (!WaterStatesEqual(before, after)) actionHistory.Add(new WaterAction(before, after, RestoreWaterStates));
                    SpawnPhysicsParticles(worldPos, new Color(0.3f, 0.5f, 1f), waterParticleCount, waterParticleSize, 3f, waterParticleForce);
                    break;
                }
            case ToolMode.Placement:
                PlaceObject(worldPos);
                break;
        }
    }

    void PlaceObject(Vector3 worldPos)
    {
        if (!placementPrefab) { Debug.LogWarning("Назначьте Placement Prefab."); return; }
        CompleteActiveActions();
        GameObject instance = Instantiate(placementPrefab, worldPos, placementPrefab.transform.rotation);
        instance.transform.localScale = placementScale;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            instance.transform.position += Vector3.up * (worldPos.y - bounds.min.y);
        }
        instance.transform.position += Vector3.down * placementSink;
        actionHistory.Add(new PlacementAction(placementPrefab, instance));
    }

    // =========================================================
    // НОВАЯ СИСТЕМА ВОДЫ (Grid / Mask)
    // =========================================================
    void PourWater(Vector3 clickPoint)
    {
        if (!targetTerrain) return;

        // Рекомендую gridStep: 0.25f. На 0.05f генерация будет тормозить из-за огромного числа полигонов!
        var result = WaterGenerator.Generate(targetTerrain, clickPoint, gridStep: 0.1f, maxRadius: 15f);

        if (!result.success) return;

        float terrainHeightAtClick = targetTerrain.SampleHeight(clickPoint) + targetTerrain.transform.position.y;
        if (result.waterLevel - terrainHeightAtClick < waterMinDepth) return;

        // Добавляем расширение маски (Dilation), чтобы вода зашла под стенки
        int w = result.mask.GetLength(0);
        int h = result.mask.GetLength(1);
        bool[,] dilatedMask = new bool[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!result.mask[x, y]) continue;
                dilatedMask[x, y] = true;
                // Расширяем на 1 клетку во все стороны
                if (x > 0) dilatedMask[x - 1, y] = true;
                if (x < w - 1) dilatedMask[x + 1, y] = true;
                if (y > 0) dilatedMask[x, y - 1] = true;
                if (y < h - 1) dilatedMask[x, y + 1] = true;
            }
        }

        Vector3 center = new Vector3(clickPoint.x, result.waterLevel, clickPoint.z);

        RemoveOverlappingWater(center);

        ProceduralWaterMesh water = Instantiate(waterPrefab, center, Quaternion.identity);
        water.BuildFromMask(dilatedMask, result.origin, result.cellSize, result.cellSize, result.waterLevel);

        WaterState state = new WaterState(center, result.waterLevel, dilatedMask, result.origin, result.cellSize, result.cellSize);
        waterHistory.Add(new WaterRecord(water, state));
    }




    void RemoveOverlappingWater(Vector3 newCenter)
    {
        for (int i = waterHistory.Count - 1; i >= 0; i--)
        {
            WaterRecord record = waterHistory[i];
            if (record.water == null) { waterHistory.RemoveAt(i); continue; }
            if (Vector3.Distance(record.state.center, newCenter) < 10f)
            {
                Destroy(record.water.gameObject);
                waterHistory.RemoveAt(i);
            }
        }
    }

    void ClearAllWater()
    {
        List<WaterState> before = CaptureWaterStates();
        if (before.Count == 0) return;

        foreach (WaterRecord record in waterHistory)
            if (record.water != null) Destroy(record.water.gameObject);

        waterHistory.Clear();
        List<WaterState> after = CaptureWaterStates();
        actionHistory.Add(new WaterAction(before, after, RestoreWaterStates));
    }

    List<WaterState> CaptureWaterStates()
    {
        List<WaterState> states = new();
        for (int i = waterHistory.Count - 1; i >= 0; i--)
        {
            WaterRecord record = waterHistory[i];
            if (record.water == null) { waterHistory.RemoveAt(i); continue; }
            states.Add(record.state.Clone());
        }
        states.Reverse();
        return states;
    }

    bool WaterStatesEqual(IReadOnlyList<WaterState> first, IReadOnlyList<WaterState> second)
    {
        if (first.Count != second.Count) return false;
        for (int i = 0; i < first.Count; i++)
        {
            if (first[i].center != second[i].center || !Mathf.Approximately(first[i].waterLevel, second[i].waterLevel)) return false;
        }
        return true;
    }

    void RestoreWaterStates(IReadOnlyList<WaterState> states)
    {
        foreach (WaterRecord record in waterHistory)
            if (record.water != null) Destroy(record.water.gameObject);
        waterHistory.Clear();

        foreach (WaterState state in states)
        {
            ProceduralWaterMesh water = Instantiate(waterPrefab, state.center, Quaternion.identity);
            water.BuildFromMask(state.mask, state.worldOrigin, state.cellSizeX, state.cellSizeZ, state.waterLevel);
            waterHistory.Add(new WaterRecord(water, state.Clone()));
        }
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
        Vector3 lp = worldPos - targetTerrain.transform.position;
        int w = data.alphamapWidth, h = data.alphamapHeight;
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
        float blendRange = 5f / data.size.y;

        for (int y = 0; y < bh; y++)
        {
            for (int x = 0; x < bw; x++)
            {
                float hgt = heights[y, x];
                float wRock = 0f, wSoil = 0f, wGrass = 0f;

                if (hgt <= rockRel - blendRange) wRock = 1f;
                else if (hgt < rockRel + blendRange) { float t = (hgt - (rockRel - blendRange)) / (blendRange * 2f); wRock = 1f - t; wSoil = t; }
                else if (hgt <= soilRel - blendRange) wSoil = 1f;
                else if (hgt < soilRel + blendRange) { float t = (hgt - (soilRel - blendRange)) / (blendRange * 2f); wSoil = 1f - t; wGrass = t; }
                else wGrass = 1f;

                for (int l = 0; l < data.alphamapLayers; l++)
                {
                    if (l == rockLayerIndex) alphas[y, x, l] = wRock;
                    else if (l == soilLayerIndex) alphas[y, x, l] = wSoil;
                    else if (l == grassLayerIndex) alphas[y, x, l] = wGrass;
                    else alphas[y, x, l] = 0f;
                }
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

    void CompleteActiveActions()
    {
        if (activeTerrainAction != null)
        {
            activeTerrainAction.Complete();
            if (activeTerrainAction.HasChanges) actionHistory.Add(activeTerrainAction);
            activeTerrainAction = null;
        }
    }

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
        strengthSlider = CreateSlider("StrengthSlider", uiRoot.transform, new Vector2(lm, yPos), new Vector2(sw, sh), 0.001f, 0.05f, strength);
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

        float mbw = 120f, mbh = 55f, bm = 25f, sx = 60f, spx = 20f;

        digButton = CreateModeButton("BtnDig", "Копать", new Vector2(sx, bm), new Vector2(mbw, mbh));
        raiseButton = CreateModeButton("BtnRaise", "Насыпать", new Vector2(sx + (mbw + spx), bm), new Vector2(mbw, mbh));
        paintButton = CreateModeButton("BtnPaint", "Красить", new Vector2(sx + (mbw + spx) * 2, bm), new Vector2(mbw, mbh));
        waterButton = CreateModeButton("BtnWater", "Вода", new Vector2(sx + (mbw + spx) * 3, bm), new Vector2(mbw, mbh));
        placementButton = CreateModeButton("BtnPlacement", "Размещение", new Vector2(sx + (mbw + spx) * 4, bm), new Vector2(mbw, mbh));

        digButton.onClick.AddListener(() => currentMode = ToolMode.Dig);
        raiseButton.onClick.AddListener(() => currentMode = ToolMode.Raise);
        paintButton.onClick.AddListener(() => currentMode = ToolMode.Paint);
        waterButton.onClick.AddListener(() => currentMode = ToolMode.Water);
        placementButton.onClick.AddListener(() => currentMode = ToolMode.Placement);

        float bigH = 65f, hp = 30f;
        float baseY = bm + mbh + 10f;

        clearWaterButton = CreateButton("ClearAllWaterBtn", uiRoot.transform, "Удалить всю воду", Vector2.zero, Vector2.zero);
        RectTransform crt = clearWaterButton.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(1, 0); crt.pivot = new Vector2(0.5f, 0);
        crt.anchoredPosition = new Vector2(0, baseY);
        crt.sizeDelta = new Vector2(-hp * 2, bigH);
        clearWaterButton.onClick.AddListener(ClearAllWater);
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

    void SetModeButtonColor(Button btn, bool active, Color activeColor, Color baseColor)
    {
        if (!btn) return;
        Image img = btn.GetComponent<Image>();
        if (img) img.color = active ? activeColor : baseColor;
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
        string modeStr = currentMode switch
        {
            ToolMode.Dig => "Копание",
            ToolMode.Raise => "Насыпь",
            ToolMode.Paint => "Краска",
            ToolMode.Water => "Вода",
            ToolMode.Placement => "Размещение",
            _ => ""
        };

        if (modeText) modeText.text = $"Режим: {modeStr}";

        if (currentMode != lastHighlightMode)
        {
            Color baseColor = uiStyle?.buttonColor ?? new Color(0.3f, 0.3f, 0.3f, 0.8f);
            SetModeButtonColor(digButton, currentMode == ToolMode.Dig, digRingColor, baseColor);
            SetModeButtonColor(raiseButton, currentMode == ToolMode.Raise, raiseRingColor, baseColor);
            SetModeButtonColor(paintButton, currentMode == ToolMode.Paint, paintRingColor, baseColor);
            SetModeButtonColor(waterButton, currentMode == ToolMode.Water, new Color(0.2f, 0.6f, 1f), baseColor);
            SetModeButtonColor(placementButton, currentMode == ToolMode.Placement, new Color(1f, 0.8f, 0.2f), baseColor);
            lastHighlightMode = currentMode;
        }

        bool digRaise = currentMode is ToolMode.Dig or ToolMode.Raise;
        bool paint = currentMode == ToolMode.Paint;
        bool water = currentMode == ToolMode.Water;
        bool placement = currentMode == ToolMode.Placement;

        if (brushSizeText) brushSizeText.gameObject.SetActive(!water && !placement);
        if (brushSizeSlider) brushSizeSlider.gameObject.SetActive(!water && !placement);
        if (strengthText) strengthText.gameObject.SetActive(digRaise);
        if (strengthSlider) strengthSlider.gameObject.SetActive(digRaise);
        if (paintStrengthText) paintStrengthText.gameObject.SetActive(paint);
        if (paintStrengthSlider) paintStrengthSlider.gameObject.SetActive(paint);
        if (paintLayerText) paintLayerText.gameObject.SetActive(paint);
        if (prevLayerButton) prevLayerButton.gameObject.SetActive(paint);
        if (nextLayerButton) nextLayerButton.gameObject.SetActive(paint);
        if (clearWaterButton) clearWaterButton.gameObject.SetActive(water);

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