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
    public float strength = 0.02f;
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
    private TMP_Text strengthText;
    private TMP_Text brushSizeText;
    private TMP_Text paintLayerText;
    private TMP_Text paintStrengthText;
    private Button digButton, raiseButton, paintButton, waterButton;
    private Slider strengthSlider, brushSizeSlider, paintStrengthSlider;
    private Button clearWaterButton;
    private Button removeLastWaterButton;
    private Button placementButton;
    private readonly List<Button> layerButtons = new();
    private Button previousLayersButton, nextLayersButton;
    private int layerPage;
    private TMP_Text radiusValueText, strengthValueText, paintValueText;
    private Image digUnderline, raiseUnderline, waterUnderline, paintUnderline;
    private Sprite roundedUISprite;
    private Texture2D roundedUITexture;
    private ToolMode styledMode = (ToolMode)(-1);

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

    void OnDestroy()
    {
        ResetTerrainToOriginal();
        if (uiRoot) Destroy(uiRoot);
        if (roundedUISprite) Destroy(roundedUISprite);
        if (roundedUITexture) Destroy(roundedUITexture);
    }

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
        bool show = showBrushHint;
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
            ToolMode.Dig => $"Копание  •  Кисть {brushSize:F1} м  •  Сила {strength:F3}",
            ToolMode.Raise => $"Насыпь  •  Кисть {brushSize:F1} м  •  Сила {strength:F3}",
            ToolMode.Paint => $"Краска  •  Кисть {brushSize:F1} м  •  {paintStrength:F2}  •  {GetLayerName(paintLayer)}",
            ToolMode.Water => "Вода  •  Нажмите для заполнения",
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
            // Keep the terrain adjustment useful within its smaller range.
            strength = Mathf.Clamp(strength + delta * 0.05f, 0.001f, 0.05f);
            if (strengthSlider) strengthSlider.SetValueWithoutNotify(strength);
        }
    }

    void UpdateLaser(bool pressed)
    {
        if (!laserLine) return;
        Transform controller = GetRightController();
        if (!controller) { laserLine.enabled = false; UpdateBrushHint(Vector3.zero, false); return; }

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
            CompleteActiveActions();
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

    void RemoveLastWater()
    {
        List<WaterState> before = CaptureWaterStates();
        if (before.Count == 0) return;
        WaterRecord last = waterHistory[waterHistory.Count - 1];
        if (last.water) Destroy(last.water.gameObject);
        waterHistory.RemoveAt(waterHistory.Count - 1);
        actionHistory.Add(new WaterAction(before, CaptureWaterStates(), RestoreWaterStates));
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
        if (targetTerrain && targetTerrain.terrainData && idx >= 0 && idx < targetTerrain.terrainData.terrainLayers.Length)
        {
            TerrainLayer layer = targetTerrain.terrainData.terrainLayers[idx];
            if (layer) return layer.name;
        }
        return "Нет покрытия";
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

    static readonly Color PanelColor = new Color32(19, 26, 30, 255);
    static readonly Color ButtonColor = new Color32(37, 48, 52, 255);
    static readonly Color TrackColor = new Color32(65, 78, 81, 255);
    static readonly Color AccentColor = new Color32(112, 220, 167, 255);
    static readonly Color MutedColor = new Color32(190, 205, 202, 255);

    void CreateUI()
    {
        uiRoot = new GameObject("FixedUI", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
        uiRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        uiRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 500);
        uiRoot.layer = LayerMask.NameToLayer("UI");
        CreateRoundedUISprite();
        Image frame = MakeImage("PanelFrame", uiRoot.transform, Vector2.zero, new Vector2(800, 500), new Color32(66, 91, 83, 255));
        ApplyRounded(frame);
        Image panel = MakeImage("Panel", uiRoot.transform, new Vector2(2, 2), new Vector2(796, 496), PanelColor);
        ApplyRounded(panel);
        Image separator = MakeImage("NavigationDivider", uiRoot.transform, new Vector2(20, 400), new Vector2(760, 1), new Color32(49, 66, 63, 255));
        separator.raycastTarget = false;

        brushSizeText = MakeText("Radius", "Радиус", new Vector2(20, 28), new Vector2(180, 36), 27);
        radiusValueText = MakeText("RadiusValue", "", new Vector2(710, 85), new Vector2(75, 30), 21);
        brushSizeSlider = MakeSlider("RadiusSlider", new Vector2(20, 89), new Vector2(680, 28), 0.1f, 10f, brushSize);
        brushSizeSlider.onValueChanged.AddListener(v => brushSize = v);

        strengthText = MakeText("Strength", "Сила", new Vector2(20, 185), new Vector2(180, 36), 27);
        strengthValueText = MakeText("StrengthValue", "", new Vector2(710, 240), new Vector2(75, 30), 21);
        strengthSlider = MakeSlider("StrengthSlider", new Vector2(20, 244), new Vector2(680, 28), 0.001f, 0.05f, strength);
        strengthSlider.onValueChanged.AddListener(v => strength = v);
        paintStrengthText = MakeText("PaintStrength", "Сила", new Vector2(20, 220), new Vector2(180, 36), 27);
        paintValueText = MakeText("PaintValue", "", new Vector2(440, 265), new Vector2(75, 30), 21);
        paintStrengthSlider = MakeSlider("PaintSlider", new Vector2(20, 269), new Vector2(410, 28), 0f, 2f, paintStrength);
        paintStrengthSlider.onValueChanged.AddListener(v => paintStrength = v);

        digButton = MakeModeButton("Dig", "Копать", ToolMode.Dig, out digUnderline);
        raiseButton = MakeModeButton("Raise", "Насыпать", ToolMode.Raise, out raiseUnderline);
        waterButton = MakeModeButton("Water", "Вода", ToolMode.Water, out waterUnderline);
        paintButton = MakeModeButton("Paint", "Покраска", ToolMode.Paint, out paintUnderline);
        placementButton = MakeButton("Placement", "Объекты", new Vector2(610, 350), new Vector2(170, 44));
        placementButton.onClick.AddListener(() => { CompleteActiveActions(); currentMode = ToolMode.Placement; UpdateUI(); });

        removeLastWaterButton = MakeButton("RemoveLastWater", "Отменить последнюю воду", new Vector2(100, 75), new Vector2(600, 62));
        removeLastWaterButton.onClick.AddListener(RemoveLastWater);
        clearWaterButton = MakeButton("ClearWater", "Удалить всю воду", new Vector2(100, 185), new Vector2(600, 62));
        clearWaterButton.onClick.AddListener(ClearAllWater);

        TerrainLayer[] layers = targetTerrain && targetTerrain.terrainData ? targetTerrain.terrainData.terrainLayers : System.Array.Empty<TerrainLayer>();
        for (int i = 0; i < layers.Length; i++)
        {
            int index = i;
            Button button = MakeButton("Layer" + i, "", new Vector2(20 + (i % 4) * 160, 20 + ((i % 8) / 4) * 82), new Vector2(86, 70));
            button.onClick.AddListener(() => paintLayer = index);
            Image preview = MakeImage("Preview", button.transform, new Vector2(4, 4), new Vector2(78, 62), ButtonColor);
            preview.raycastTarget = false;
            if (layers[i] && layers[i].diffuseTexture)
            {
                Texture2D texture = layers[i].diffuseTexture;
                preview.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                preview.color = Color.white;
            }
            layerButtons.Add(button);
        }
        previousLayersButton = MakeButton("PreviousLayers", "◀", new Vector2(675, 20), new Vector2(50, 65));
        nextLayersButton = MakeButton("NextLayers", "▶", new Vector2(730, 20), new Vector2(50, 65));
        previousLayersButton.onClick.AddListener(() => { layerPage = Mathf.Max(0, layerPage - 1); UpdateUI(); });
        nextLayersButton.onClick.AddListener(() => { layerPage = Mathf.Min((layerButtons.Count - 1) / 8, layerPage + 1); UpdateUI(); });
        paintLayerText = MakeText("SelectedLayer", "", new Vector2(20, 180), new Vector2(620, 28), 18);
        paintLayerText.color = MutedColor;
        AssignLayerRecursively(uiRoot, uiRoot.layer);
        UpdateUI();
    }

    void CreateRoundedUISprite()
    {
        const int size = 64;
        const float radius = 15f;
        roundedUITexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        roundedUITexture.name = "Rounded UI";
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - (31.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y - 31.5f) - (31.5f - radius), 0f);
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                roundedUITexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        roundedUITexture.Apply();
        roundedUISprite = Sprite.Create(roundedUITexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
    }

    void ApplyRounded(Image image)
    {
        image.sprite = roundedUISprite;
        image.type = Image.Type.Sliced;
    }

    Image MakeImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(position.x, -position.y);
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    TMP_Text MakeText(string name, string value, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(uiRoot.transform, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
        text.rectTransform.anchoredPosition = new Vector2(position.x, -position.y);
        text.rectTransform.sizeDelta = size;
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        if (uiStyle && uiStyle.mainFont) text.font = uiStyle.mainFont;
        return text;
    }

    Button MakeButton(string name, string label, Vector2 position, Vector2 size)
    {
        Image image = MakeImage(name, uiRoot.transform, position, size, ButtonColor);
        ApplyRounded(image);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        SetButtonColors(button, ButtonColor);
        TMP_Text text = MakeText(name + "Label", label, Vector2.zero, size, 22);
        text.transform.SetParent(image.transform, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    void SetButtonColors(Button button, Color normal)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = Color.Lerp(normal, AccentColor, 0.24f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(normal, AccentColor, 0.40f);
        colors.disabledColor = new Color32(39, 48, 49, 150);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
    }

    Button MakeModeButton(string name, string label, ToolMode mode, out Image underline)
    {
        Button button = MakeButton(name, label, Vector2.zero, new Vector2(160, 48));
        button.onClick.AddListener(() => { CompleteActiveActions(); currentMode = mode; UpdateUI(); });
        underline = MakeImage("Selected", button.transform, new Vector2(0, 48), new Vector2(160, 3), AccentColor);
        underline.raycastTarget = false;
        return button;
    }

    Slider MakeSlider(string name, Vector2 position, Vector2 size, float min, float max, float value)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(uiRoot.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(position.x, -position.y);
        rect.sizeDelta = size;
        Image track = MakeImage("TrackHitArea", go.transform, Vector2.zero, size, new Color(0, 0, 0, 0));
        track.raycastTarget = true;
        track.rectTransform.anchorMin = new Vector2(0, 0.5f);
        track.rectTransform.anchorMax = new Vector2(1, 0.5f);
        track.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        track.rectTransform.anchoredPosition = Vector2.zero;
        track.rectTransform.sizeDelta = new Vector2(0, 48);
        Image bar = MakeImage("Track", track.transform, Vector2.zero, Vector2.zero, TrackColor);
        bar.raycastTarget = false;
        ApplyRounded(bar);
        bar.rectTransform.anchorMin = new Vector2(0, 0.5f);
        bar.rectTransform.anchorMax = new Vector2(1, 0.5f);
        bar.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        bar.rectTransform.anchoredPosition = Vector2.zero;
        bar.rectTransform.sizeDelta = new Vector2(0, 6);
        Image fill = MakeImage("Fill", go.transform, new Vector2(0, 11), new Vector2(0, 6), AccentColor);
        fill.raycastTarget = false;
        ApplyRounded(fill);
        fill.rectTransform.anchorMin = new Vector2(0, 0.5f);
        fill.rectTransform.anchorMax = new Vector2(0, 0.5f);
        fill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        fill.rectTransform.anchoredPosition = Vector2.zero;
        Image handle = MakeImage("Handle", go.transform, new Vector2(0, 0), new Vector2(24, 24), AccentColor);
        handle.raycastTarget = false;
        Texture2D circle = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                circle.SetPixel(x, y, new Vector2(x - 15.5f, y - 15.5f).sqrMagnitude <= 240f ? Color.white : Color.clear);
        circle.Apply();
        handle.sprite = Sprite.Create(circle, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, 0.5f);
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.rectTransform.anchoredPosition = Vector2.zero;
        Slider slider = go.GetComponent<Slider>();
        slider.minValue = min; slider.maxValue = max;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = track;
        slider.value = value;
        return slider;
    }

    void AssignLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform) AssignLayerRecursively(child.gameObject, layer);
    }

    void UpdateUI()
    {
        if (!brushSizeSlider) return;
        bool paint = currentMode == ToolMode.Paint;
        bool water = currentMode == ToolMode.Water;
        bool terrain = currentMode == ToolMode.Dig || currentMode == ToolMode.Raise;
        Button[] modes = { digButton, raiseButton, waterButton, paintButton };
        Image[] underlines = { digUnderline, raiseUnderline, waterUnderline, paintUnderline };
        ToolMode[] values = { ToolMode.Dig, ToolMode.Raise, ToolMode.Water, ToolMode.Paint };
        for (int i = 0; i < modes.Length; i++)
        {
            modes[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(20 + i * 200, -420);
            underlines[i].gameObject.SetActive(currentMode == values[i]);
            if (styledMode != currentMode)
                SetButtonColors(modes[i], currentMode == values[i] ? new Color32(38, 73, 60, 255) : ButtonColor);
        }
        styledMode = currentMode;
        brushSizeText.gameObject.SetActive(terrain || paint);
        brushSizeSlider.gameObject.SetActive(terrain || paint);
        radiusValueText.gameObject.SetActive(terrain || paint);
        brushSizeText.rectTransform.anchoredPosition = new Vector2(20, paint ? -210 : -28);
        brushSizeSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(20, paint ? -252 : -89);
        brushSizeSlider.GetComponent<RectTransform>().sizeDelta = new Vector2(paint ? 410 : 680, 28);
        radiusValueText.rectTransform.anchoredPosition = new Vector2(paint ? 440 : 710, paint ? -248 : -85);
        strengthText.gameObject.SetActive(terrain);
        strengthSlider.gameObject.SetActive(terrain);
        strengthValueText.gameObject.SetActive(terrain);
        paintStrengthText.gameObject.SetActive(paint);
        paintStrengthText.rectTransform.anchoredPosition = new Vector2(20, -305);
        paintStrengthSlider.gameObject.SetActive(paint);
        paintStrengthSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(20, -347);
        paintValueText.gameObject.SetActive(paint);
        paintValueText.rectTransform.anchoredPosition = new Vector2(440, -343);
        paintLayerText.gameObject.SetActive(paint);
        removeLastWaterButton.gameObject.SetActive(water);
        clearWaterButton.gameObject.SetActive(water);
        bool pagedLayers = paint && layerButtons.Count > 8;
        previousLayersButton.gameObject.SetActive(pagedLayers);
        nextLayersButton.gameObject.SetActive(pagedLayers);
        previousLayersButton.interactable = layerPage > 0;
        nextLayersButton.interactable = (layerPage + 1) * 8 < layerButtons.Count;
        placementButton.gameObject.SetActive(placementPrefab && (terrain || currentMode == ToolMode.Placement));
        bool hasWater = waterHistory.Count > 0;
        removeLastWaterButton.interactable = hasWater;
        clearWaterButton.interactable = hasWater;
        brushSizeSlider.SetValueWithoutNotify(brushSize);
        strengthSlider.SetValueWithoutNotify(strength);
        paintStrengthSlider.SetValueWithoutNotify(paintStrength);
        radiusValueText.text = $"{brushSize:F1} м";
        strengthValueText.text = $"{Mathf.RoundToInt(Mathf.InverseLerp(0.001f, 0.05f, strength) * 100)}%";
        paintValueText.text = $"{Mathf.RoundToInt(Mathf.InverseLerp(0f, 2f, paintStrength) * 100)}%";
        radiusValueText.color = strengthValueText.color = paintValueText.color = MutedColor;
        paintLayerText.text = GetLayerName(paintLayer);
        for (int i = 0; i < layerButtons.Count; i++)
        {
            layerButtons[i].gameObject.SetActive(paint && i / 8 == layerPage);
            layerButtons[i].GetComponent<Image>().color = i == paintLayer ? AccentColor : TrackColor;
        }
    }

}
