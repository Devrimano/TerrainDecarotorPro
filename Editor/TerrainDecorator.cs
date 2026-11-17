using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Terrain Decorator Pro - Standalone Unity Editor Extension
/// Procedural mountain generation, 6-layer PBR splatmapping, rock clustering, and vegetation scattering.
/// </summary>
public class TerrainDecorator : EditorWindow
{
    // ─── Target Terrain ───────────────────────────────────────────────
    [SerializeField] private Terrain targetTerrain;

    // ─── Mountain Settings ────────────────────────────────────────────
    [SerializeField] private float mountainHeight = 130f;
    [SerializeField] private float mountainBorderWidth = 0.28f;
    [SerializeField] private float mountainRoughness = 1.0f;
    [SerializeField] private Vector2 mountainCenter = new Vector2(490f, 500f);
    [SerializeField] private float valleyRadius = 240f;
    [SerializeField] private float valleyTransition = 90f;

    // ─── Biome & Splatmap Settings ───────────────────────────────────
    [SerializeField] private bool paintCliffTextures = true;
    [SerializeField] private float roadVergeWidth = 3.5f;
    [SerializeField] private float forestSoilIntensity = 0.65f;

    // ─── Counts & Densities ──────────────────────────────────────────
    [SerializeField] private int rockCount = 180;
    [SerializeField] private int flowerCount = 1100;
    [SerializeField] private int bushCount = 1200;
    [SerializeField] private int forestTreeCount = 2500;
    [SerializeField] private int neighborhoodTreeCount = 500;

    // ─── Margins & Clearances ────────────────────────────────────────
    [SerializeField] private float roadAsphaltWidth = 5.0f;
    [SerializeField] private float buildingClearanceRadius = 7.0f;
    [SerializeField] private string buildingTag = "House";
    [SerializeField] private string roadTag = "Road";

    // ─── Performance & Atmosphere ────────────────────────────────────
    [SerializeField] private float optimizedTreeDistance = 320f;
    [SerializeField] private float optimizedBillboardDistance = 65f;
    [SerializeField] private int maxFullLODTrees = 75;
    [SerializeField] private bool applyAtmosphere = true;

    // ─── Custom Asset References ─────────────────────────────────────
    [SerializeField] private Material terrainLitMaterial;
    [SerializeField] private GameObject globalVolumePrefab;

    [SerializeField] private TerrainLayer layerGrassA;
    [SerializeField] private TerrainLayer layerGrassB;
    [SerializeField] private TerrainLayer layerForestSoil;
    [SerializeField] private TerrainLayer layerRoadPebbles;
    [SerializeField] private TerrainLayer layerCliffRock;
    [SerializeField] private TerrainLayer layerSummitRock;

    [SerializeField] private List<GameObject> rockPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> forestTreePrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> neighborhoodTreePrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> bushPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> flowerPrefabs = new List<GameObject>();

    // Dynamic obstacle cache
    private List<Vector2> cachedHousePositions = new List<Vector2>();
    private List<Vector2> cachedRoadPoints = new List<Vector2>();

    // GUI Foldout States
    private bool foldoutTerrain = true;
    private bool foldoutAssets = false;
    private bool foldoutMountains = true;
    private bool foldoutTextures = true;
    private bool foldoutScatter = true;
    private bool foldoutExclusion = true;
    private bool foldoutPerformance = true;

    private Vector2 scrollPos;

    [MenuItem("Tools/Terrain Decorator/Open Decorator Window")]
    public static void ShowWindow()
    {
        var window = GetWindow<TerrainDecorator>("Terrain Decorator Pro");
        window.minSize = new Vector2(440, 720);
    }

    private void OnEnable()
    {
        if (targetTerrain == null)
            targetTerrain = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();

        if (rockPrefabs.Count == 0 && layerGrassA == null)
            AutoDetectAssets(silent: true);
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
        EditorGUILayout.LabelField("🌲 Terrain Decorator & Optimizer Pro", titleStyle);
        EditorGUILayout.HelpBox(
            "Procedural mountain generation, 6-layer PBR splatmapping, rock clustering, and vegetation placement with built-in performance optimization.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        // ─── Target Terrain & Preset Tools ───
        foldoutTerrain = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutTerrain, "Target Terrain & Presets");
        if (foldoutTerrain)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            targetTerrain = (Terrain)EditorGUILayout.ObjectField("Active Terrain", targetTerrain, typeof(Terrain), true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find Active Terrain"))
            {
                targetTerrain = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();
            }
            if (GUILayout.Button("🔍 Auto-Detect Project Assets"))
            {
                AutoDetectAssets(silent: false);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ─── Asset Slots Foldout ───
        foldoutAssets = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutAssets, "Asset References (Materials, Layers, Prefabs)");
        if (foldoutAssets)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            terrainLitMaterial = (Material)EditorGUILayout.ObjectField("Terrain Material", terrainLitMaterial, typeof(Material), false);
            globalVolumePrefab = (GameObject)EditorGUILayout.ObjectField("Global Volume Prefab", globalVolumePrefab, typeof(GameObject), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Terrain Biome Layers", EditorStyles.boldLabel);
            layerGrassA = (TerrainLayer)EditorGUILayout.ObjectField("1. Primary Grass", layerGrassA, typeof(TerrainLayer), false);
            layerGrassB = (TerrainLayer)EditorGUILayout.ObjectField("2. Grass Variation", layerGrassB, typeof(TerrainLayer), false);
            layerForestSoil = (TerrainLayer)EditorGUILayout.ObjectField("3. Forest Soil / Needles", layerForestSoil, typeof(TerrainLayer), false);
            layerRoadPebbles = (TerrainLayer)EditorGUILayout.ObjectField("4. Roadside Pebbles", layerRoadPebbles, typeof(TerrainLayer), false);
            layerCliffRock = (TerrainLayer)EditorGUILayout.ObjectField("5. Cliff / Steep Rock", layerCliffRock, typeof(TerrainLayer), false);
            layerSummitRock = (TerrainLayer)EditorGUILayout.ObjectField("6. Summit / High Peak", layerSummitRock, typeof(TerrainLayer), false);

            EditorGUILayout.Space(4);
            DrawPrefabList("Rock Prefabs", rockPrefabs);
            DrawPrefabList("Tall Forest Tree Prefabs", forestTreePrefabs);
            DrawPrefabList("Neighborhood Tree Prefabs", neighborhoodTreePrefabs);
            DrawPrefabList("Bush & Shrub Prefabs", bushPrefabs);
            DrawPrefabList("Flower Prefabs", flowerPrefabs);

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ─── Mountain Generator Foldout ───
        foldoutMountains = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutMountains, "⛰️ Mountain & Elevation Generator");
        if (foldoutMountains)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            mountainHeight = EditorGUILayout.Slider("Mountain Height", mountainHeight, 40f, 300f);
            mountainBorderWidth = EditorGUILayout.Slider("Border Width (0-1)", mountainBorderWidth, 0.10f, 0.45f);
            mountainRoughness = EditorGUILayout.Slider("Peak Roughness", mountainRoughness, 0.4f, 2.5f);
            mountainCenter = EditorGUILayout.Vector2Field("Valley Center", mountainCenter);
            valleyRadius = EditorGUILayout.Slider("Valley Flat Radius", valleyRadius, 50f, 500f);
            valleyTransition = EditorGUILayout.Slider("Valley Transition Width", valleyTransition, 30f, 200f);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ─── Biome Splatmapping Foldout ───
        foldoutTextures = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutTextures, "🎨 Biome Splatmap & Ground Layers");
        if (foldoutTextures)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            paintCliffTextures = EditorGUILayout.Toggle("Auto-Paint on Height Change", paintCliffTextures);
            roadVergeWidth = EditorGUILayout.Slider("Road Verge Width", roadVergeWidth, 1.5f, 8f);
            forestSoilIntensity = EditorGUILayout.Slider("Forest Soil Patch Intensity", forestSoilIntensity, 0.1f, 1f);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ─── Scatter Counts Foldout ───
        foldoutScatter = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutScatter, "🪨 & 🌿 Nature Asset Counts");
        if (foldoutScatter)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            rockCount = EditorGUILayout.IntSlider("Rock & Boulder Clusters", rockCount, 20, 500);
            flowerCount = EditorGUILayout.IntSlider("Heather & Wildflowers", flowerCount, 200, 4000);
            bushCount = EditorGUILayout.IntSlider("Bushes & Ferns", bushCount, 200, 3000);
            forestTreeCount = EditorGUILayout.IntSlider("Forest Trees", forestTreeCount, 500, 6000);
            neighborhoodTreeCount = EditorGUILayout.IntSlider("Neighborhood Trees", neighborhoodTreeCount, 100, 1500);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ─── Obstacle & Exclusion Foldout ───
        foldoutExclusion = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutExclusion, "🚫 Road & Building Clearances");
        if (foldoutExclusion)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            roadAsphaltWidth = EditorGUILayout.Slider("Road Half-Width (Clearance)", roadAsphaltWidth, 2f, 10f);
            buildingClearanceRadius = EditorGUILayout.Slider("Building Clearance Radius", buildingClearanceRadius, 3f, 15f);
            buildingTag = EditorGUILayout.TextField("Building Tag Filter", buildingTag);
            roadTag = EditorGUILayout.TextField("Road Tag Filter", roadTag);

            if (GUILayout.Button("🔄 Scan Scene for Buildings & Roads"))
            {
                ScanSceneObstacles();
            }
            EditorGUILayout.HelpBox($"Cached: {cachedHousePositions.Count} Buildings, {cachedRoadPoints.Count} Road points.", MessageType.None);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ─── Performance & Atmosphere Foldout ───
        foldoutPerformance = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutPerformance, "⚡ Performance & Atmosphere");
        if (foldoutPerformance)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            optimizedTreeDistance = EditorGUILayout.Slider("Tree Draw Distance (m)", optimizedTreeDistance, 150f, 800f);
            optimizedBillboardDistance = EditorGUILayout.Slider("Billboard Start Distance (m)", optimizedBillboardDistance, 30f, 150f);
            maxFullLODTrees = EditorGUILayout.IntSlider("Max Full LOD Trees", maxFullLODTrees, 20, 200);
            applyAtmosphere = EditorGUILayout.Toggle("Apply Atmosphere & Post-Process", applyAtmosphere);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(12);

        // ─── MASTER BUTTON ───
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
        if (GUILayout.Button("🚀 GENERATE ALL (Mountains + Splatmap + Rocks + Foliage)", GUILayout.Height(45)))
        {
            if (EditorUtility.DisplayDialog("Terrain Decorator",
                "This action will:\n" +
                "1. Apply performance optimizations & material\n" +
                "2. Generate fractal mountains\n" +
                "3. Paint 6-layer procedural biome splatmaps\n" +
                "4. Scatter mossy boulders and rock clusters\n" +
                "5. Plant all vegetation layers\n" +
                "6. Configure atmosphere / post-processing\n\nDo you want to proceed?", "Yes, Apply All!", "Cancel"))
            {
                ApplyAll();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🛠️ Individual Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("⚡ Apply Performance & Terrain Settings Only", GUILayout.Height(28)))
            OptimizeTerrainSettings();

        if (GUILayout.Button("⛰️ Generate Mountains Only", GUILayout.Height(28)))
            GenerateMountains();

        if (GUILayout.Button("🎨 Paint Biome Splatmap Only", GUILayout.Height(28)))
            PaintBiomeTextures();

        if (GUILayout.Button("🪨 Place Rocks & Boulders Only", GUILayout.Height(28)))
            PlaceRocks();

        if (GUILayout.Button("🌸 Place All Foliage Only", GUILayout.Height(28)))
            PlaceAllVegetation();

        if (GUILayout.Button("🌟 Apply Atmosphere & Post-Processing Only", GUILayout.Height(28)))
            ApplyAtmosphere();

        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
        if (GUILayout.Button("🗑️ Clear All Trees & Foliage", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("Warning", "All foliage instances on the terrain will be removed!", "Yes", "Cancel"))
                ClearFoliage();
        }
        if (GUILayout.Button("🗑️ Clear All Rocks", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("Warning", "All rock GameObjects under the 'Rocks' root will be removed!", "Yes", "Cancel"))
                ClearRocks();
        }
        if (GUILayout.Button("🗑️ Flatten Terrain Heightmap", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("Warning", "The terrain heightmap will be flattened!", "Yes", "Cancel"))
                FlattenHeightmap();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void DrawPrefabList(string title, List<GameObject> list)
    {
        EditorGUILayout.LabelField($"{title} ({list.Count})", EditorStyles.boldLabel);
        for (int i = 0; i < list.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            list[i] = (GameObject)EditorGUILayout.ObjectField(list[i], typeof(GameObject), false);
            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                list.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        var newObj = (GameObject)EditorGUILayout.ObjectField("+ Add Prefab", null, typeof(GameObject), false);
        if (newObj != null && !list.Contains(newObj))
            list.Add(newObj);
        EditorGUILayout.Space(3);
    }

    // ════════════════════════════════════════════════════════════════
    //  MASTER WORKFLOW
    // ════════════════════════════════════════════════════════════════
    public void ApplyAll()
    {
        if (!ValidateTerrain()) return;
        ScanSceneObstacles();

        OptimizeTerrainSettings();
        GenerateMountains();
        PaintBiomeTextures();
        PlaceRocks();
        PlaceAllVegetation();
        if (applyAtmosphere)
            ApplyAtmosphere();

        Debug.Log("[TerrainDecorator] 🎉 All operations completed successfully!");
    }

    private bool ValidateTerrain()
    {
        if (targetTerrain == null)
            targetTerrain = Terrain.activeTerrain ?? Object.FindFirstObjectByType<Terrain>();

        if (targetTerrain == null)
        {
            EditorUtility.DisplayDialog("Error", "No active Terrain found in the current scene!", "OK");
            return false;
        }
        return true;
    }

    // ════════════════════════════════════════════════════════════════
    //  1. OPTIMIZE TERRAIN SETTINGS & MATERIAL
    // ════════════════════════════════════════════════════════════════
    public void OptimizeTerrainSettings()
    {
        if (!ValidateTerrain()) return;

        Undo.RegisterCompleteObjectUndo(targetTerrain, "Optimize Terrain Settings");

        if (terrainLitMaterial != null)
        {
            targetTerrain.materialTemplate = terrainLitMaterial;
            Debug.Log($"[TerrainDecorator] 🎨 Assigned Terrain Material: {terrainLitMaterial.name}");
        }

        targetTerrain.treeDistance = optimizedTreeDistance;
        targetTerrain.treeBillboardDistance = optimizedBillboardDistance;
        targetTerrain.treeCrossFadeLength = 15f;
        targetTerrain.treeMaximumFullLODCount = maxFullLODTrees;
        targetTerrain.detailObjectDistance = 75f;
        targetTerrain.detailObjectDensity = 0.5f;
        targetTerrain.heightmapPixelError = 6f;
        targetTerrain.drawInstanced = true;
        targetTerrain.drawTreesAndFoliage = true;

        EditorUtility.SetDirty(targetTerrain);
        Debug.Log($"[TerrainDecorator] ⚡ Terrain optimized! DrawDistance: {optimizedTreeDistance}m, BillboardStart: {optimizedBillboardDistance}m, Instancing: ON");
    }

    // ════════════════════════════════════════════════════════════════
    //  2. PROCEDURAL MOUNTAIN GENERATION
    // ════════════════════════════════════════════════════════════════
    public void GenerateMountains()
    {
        if (!ValidateTerrain()) return;

        Undo.RegisterCompleteObjectUndo(targetTerrain.terrainData, "Generate Mountains");

        var data = targetTerrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        float maxTerrainHeight = data.size.y;

        EditorUtility.DisplayProgressBar("Generating Mountains", "Computing ridged multifractal heights...", 0f);

        for (int z = 0; z < res; z++)
        {
            if (z % 35 == 0)
                EditorUtility.DisplayProgressBar("Generating Mountains", $"Row {z}/{res}...", (float)z / res);

            for (int x = 0; x < res; x++)
            {
                float nx = (float)x / (res - 1);
                float nz = (float)z / (res - 1);

                float worldX = nx * data.size.x;
                float worldZ = nz * data.size.z;

                float warpX = Mathf.PerlinNoise(nx * 3.5f + 17.2f, nz * 3.5f + 42.1f) - 0.5f;
                float warpZ = Mathf.PerlinNoise(nx * 3.5f + 84.6f, nz * 3.5f + 11.8f) - 0.5f;
                float borderWarp = (warpX + warpZ) * 0.12f;

                float dLeft = nx + borderWarp;
                float dRight = (1f - nx) + borderWarp;
                float dBottom = nz + borderWarp;
                float dTop = (1f - nz) + borderWarp;

                float distFromBorder = Mathf.Max(0f, Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dBottom, dTop)));
                float finalHeight = 0f;

                if (distFromBorder < mountainBorderWidth)
                {
                    float t = Mathf.Clamp01(1f - (distFromBorder / mountainBorderWidth));
                    float baseSlope = Mathf.Pow(t, 1.7f);

                    float r1 = 1.0f - Mathf.Abs(Mathf.PerlinNoise(nx * 8f + 142.1f, nz * 8f + 71.3f) * 2f - 1f);
                    float r2 = 1.0f - Mathf.Abs(Mathf.PerlinNoise(nx * 18f + 33.5f, nz * 18f + 92.4f) * 2f - 1f);
                    float r3 = 1.0f - Mathf.Abs(Mathf.PerlinNoise(nx * 42f + 81.2f, nz * 42f + 15.6f) * 2f - 1f);
                    float ridgedNoise = (r1 * r1 * 0.60f + r2 * r2 * 0.28f + r3 * 0.12f) * mountainRoughness;

                    float billow = Mathf.Abs(Mathf.PerlinNoise(nx * 6f + 55.5f, nz * 6f + 66.6f) * 2f - 1f);

                    float cx = nx < 0.5f ? nx : 1f - nx;
                    float cz = nz < 0.5f ? nz : 1f - nz;
                    float cornerDist = Mathf.Sqrt(cx * cx + cz * cz);
                    float cornerBoost = Mathf.Lerp(1.4f, 1.0f, Mathf.Clamp01(cornerDist / 0.35f));

                    float mountainProfile = (baseSlope * 0.65f + baseSlope * ridgedNoise * 0.60f + baseSlope * billow * 0.15f) * cornerBoost;
                    finalHeight = mountainProfile * (mountainHeight / maxTerrainHeight);

                    // Valley Transition Mask
                    float distToCenter = Vector2.Distance(new Vector2(worldX, worldZ), mountainCenter);
                    if (distToCenter < valleyRadius + valleyTransition)
                    {
                        float safety = Mathf.Clamp01((distToCenter - valleyRadius) / valleyTransition);
                        safety = safety * safety * (3f - 2f * safety);
                        finalHeight *= safety;
                    }
                }

                heights[z, x] = Mathf.Max(0f, finalHeight);
            }
        }

        data.SetHeights(0, 0, heights);
        EditorUtility.ClearProgressBar();
        Debug.Log("[TerrainDecorator] ⛰️ Mountains generated successfully!");

        if (paintCliffTextures)
            PaintBiomeTextures();
    }

    // ════════════════════════════════════════════════════════════════
    //  3. 6-LAYER PBR BIOME TEXTURING
    // ════════════════════════════════════════════════════════════════
    public void PaintBiomeTextures()
    {
        if (!ValidateTerrain()) return;
        Undo.RegisterCompleteObjectUndo(targetTerrain.terrainData, "Paint Biome Textures");

        var data = targetTerrain.terrainData;
        var terrainSize = data.size;

        if (layerGrassA == null)
        {
            EditorUtility.DisplayDialog("Notice", "Primary Grass layer (Grass A) is required for texturing. Please assign it in the Asset References.", "OK");
            return;
        }

        var layers = new List<TerrainLayer>
        {
            layerGrassA,
            layerGrassB ?? layerGrassA,
            layerForestSoil ?? layerGrassA,
            layerRoadPebbles ?? layerForestSoil ?? layerGrassA,
            layerCliffRock ?? layerGrassA,
            layerSummitRock ?? layerCliffRock ?? layerGrassA
        };

        data.terrainLayers = layers.ToArray();

        int alphaRes = data.alphamapResolution;
        int numLayers = layers.Count;
        float[,,] splatmap = new float[alphaRes, alphaRes, numLayers];

        EditorUtility.DisplayProgressBar("Painting Biomes", "Calculating splatmap weights...", 0f);

        for (int z = 0; z < alphaRes; z++)
        {
            if (z % 30 == 0)
                EditorUtility.DisplayProgressBar("Painting Biomes", $"Row {z}/{alphaRes}...", (float)z / alphaRes);

            for (int x = 0; x < alphaRes; x++)
            {
                float nx = (float)x / (alphaRes - 1);
                float nz = (float)z / (alphaRes - 1);

                float worldX = nx * terrainSize.x;
                float worldZ = nz * terrainSize.z;

                float steepness = data.GetSteepness(nx, nz);
                float height = data.GetInterpolatedHeight(nx, nz);

                Vector2 pos2D = new Vector2(worldX, worldZ);
                float distToRoad = GetMinDistanceToRoad(pos2D);
                float distToValleyCenter = Vector2.Distance(pos2D, mountainCenter);

                // 1. Steep Cliff / Rock (> 17 deg)
                float cliffFactor = Mathf.Clamp01((steepness - 17f) / 14f);

                // 2. High Summit Crags (> 55m)
                float summitFactor = 0f;
                if (height > 55f)
                {
                    float hFactor = Mathf.Clamp01((height - 55f) / 45f);
                    summitFactor = hFactor * 0.75f;
                    float ridge = Mathf.Abs(Mathf.PerlinNoise(worldX * 0.05f, worldZ * 0.05f) - 0.5f) * 2f;
                    summitFactor *= (0.6f + ridge * 0.4f);
                }

                // 3. Roadside Gravel Verge
                float vergeFactor = 0f;
                if (distToRoad >= 4.5f && distToRoad <= (5.0f + roadVergeWidth))
                {
                    float offset = distToRoad - 5.0f;
                    float vergeFalloff = 1.0f - Mathf.Clamp01(offset / roadVergeWidth);
                    float vergeNoise = 0.8f + Mathf.PerlinNoise(worldX * 0.35f, worldZ * 0.35f) * 0.4f;
                    vergeFactor = Mathf.Clamp01(vergeFalloff * vergeNoise);
                }

                // 4. Forest Soil
                float forestSoil = 0f;
                if (distToValleyCenter > valleyRadius)
                {
                    float fade = Mathf.Clamp01((distToValleyCenter - valleyRadius) / valleyTransition);
                    float soilNoise = Mathf.PerlinNoise(worldX * 0.035f + 14.2f, worldZ * 0.035f + 81.6f);
                    float soilNoise2 = Mathf.PerlinNoise(worldX * 0.10f, worldZ * 0.10f) * 0.3f;
                    forestSoil = Mathf.Clamp01(((soilNoise + soilNoise2) * forestSoilIntensity - 0.32f) / 0.5f) * fade;
                }

                // 5. Grass Variation
                float grassNoise = Mathf.PerlinNoise(worldX * 0.018f + 37.1f, worldZ * 0.018f + 92.4f);
                float grassBFactor = Mathf.Clamp01((grassNoise - 0.42f) * 2.2f);

                float wCliff = cliffFactor;
                float wSummit = summitFactor * (1f - wCliff * 0.5f);
                float rockTotal = Mathf.Clamp01(wCliff + wSummit);

                float wPebbles = vergeFactor * (1f - rockTotal);
                float wSoil = forestSoil * (1f - rockTotal) * (1f - wPebbles);

                float remainingGround = Mathf.Max(0f, 1f - (rockTotal + wPebbles + wSoil));
                float wGrassB = remainingGround * grassBFactor * 0.38f;
                float wGrassA = remainingGround - wGrassB;

                float totalW = wGrassA + wGrassB + wSoil + wPebbles + wCliff + wSummit;
                if (totalW < 0.0001f) totalW = 1f;

                splatmap[z, x, 0] = wGrassA / totalW;
                splatmap[z, x, 1] = wGrassB / totalW;
                splatmap[z, x, 2] = wSoil / totalW;
                splatmap[z, x, 3] = wPebbles / totalW;
                splatmap[z, x, 4] = wCliff / totalW;
                splatmap[z, x, 5] = wSummit / totalW;
            }
        }

        data.SetAlphamaps(0, 0, splatmap);
        EditorUtility.ClearProgressBar();
        Debug.Log("[TerrainDecorator] 🎨 6-Layer PBR Biome Textures Painted Successfully!");
    }

    // ════════════════════════════════════════════════════════════════
    //  4. ROCK & BOULDER CLUSTERS
    // ════════════════════════════════════════════════════════════════
    public void PlaceRocks()
    {
        if (!ValidateTerrain()) return;
        if (rockPrefabs.Count == 0)
        {
            Debug.LogWarning("[TerrainDecorator] No rock prefabs assigned in Asset References!");
            return;
        }

        GameObject rocksRoot = GameObject.Find("Rocks");
        if (rocksRoot != null)
            Undo.DestroyObjectImmediate(rocksRoot);

        rocksRoot = new GameObject("Rocks");
        Undo.RegisterCreatedObjectUndo(rocksRoot, "Place Rocks");

        var data = targetTerrain.terrainData;
        var terrainSize = data.size;

        Random.InitState(777);
        int placed = 0;
        int clusterCount = rockCount / 3;

        for (int c = 0; c < clusterCount; c++)
        {
            float cx = Random.Range(50f, terrainSize.x - 50f);
            float cz = Random.Range(50f, terrainSize.z - 50f);
            Vector2 clusterCenter = new Vector2(cx, cz);

            if (GetMinDistanceToRoad(clusterCenter) < 7.0f || GetMinDistanceToHouse(clusterCenter) < 12.0f)
                continue;

            int countInCluster = Random.Range(2, 5);
            for (int i = 0; i < countInCluster; i++)
            {
                float rx = cx + Random.Range(-8f, 8f);
                float rz = cz + Random.Range(-8f, 8f);
                Vector2 rPos = new Vector2(rx, rz);

                if (GetMinDistanceToRoad(rPos) < 6.0f || GetMinDistanceToHouse(rPos) < 9.0f)
                    continue;

                float nx = rx / terrainSize.x;
                float nz = rz / terrainSize.z;
                if (nx < 0.02f || nx > 0.98f || nz < 0.02f || nz > 0.98f) continue;

                float terrainH = data.GetInterpolatedHeight(nx, nz);
                var prefab = rockPrefabs[Random.Range(0, rockPrefabs.Count)];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rocksRoot.transform);

                float scale = Random.Range(0.7f, 1.6f);
                instance.transform.localScale = Vector3.one * scale;

                float sink = Random.Range(0.2f, 0.45f) * scale;
                instance.transform.position = new Vector3(rx, terrainH - sink, rz);
                instance.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));

                placed++;
            }
        }

        Debug.Log($"[TerrainDecorator] 🪨 Placed {placed} rocks and boulders!");
    }

    // ════════════════════════════════════════════════════════════════
    //  5. VEGETATION & FOLIAGE PLACEMENT
    // ════════════════════════════════════════════════════════════════
    public void PlaceAllVegetation()
    {
        if (!ValidateTerrain()) return;

        var allFoliage = new List<GameObject>();
        allFoliage.AddRange(forestTreePrefabs);
        allFoliage.AddRange(neighborhoodTreePrefabs);
        allFoliage.AddRange(bushPrefabs);
        allFoliage.AddRange(flowerPrefabs);

        if (allFoliage.Count == 0)
        {
            EditorUtility.DisplayDialog("Notice", "No foliage prefabs assigned in Asset References!", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(targetTerrain.terrainData, "Place Foliage");

        var data = targetTerrain.terrainData;
        var terrainSize = data.size;

        var prototypes = new List<TreePrototype>();
        foreach (var p in allFoliage)
        {
            if (p != null)
                prototypes.Add(new TreePrototype { prefab = p });
        }
        data.treePrototypes = prototypes.ToArray();

        var instances = new List<TreeInstance>();
        Random.InitState(1337);

        EditorUtility.DisplayProgressBar("Placing Vegetation", "Scattering foliage across biomes...", 0f);

        int totalGoal = forestTreeCount + neighborhoodTreeCount + bushCount + flowerCount;
        int maxAttempts = totalGoal * 8;
        int attempts = 0;
        int placedCount = 0;

        while (placedCount < totalGoal && attempts < maxAttempts)
        {
            attempts++;
            float nx = Random.Range(0.02f, 0.98f);
            float nz = Random.Range(0.02f, 0.98f);

            float worldX = nx * terrainSize.x;
            float worldZ = nz * terrainSize.z;
            Vector2 pos2D = new Vector2(worldX, worldZ);

            float distToRoad = GetMinDistanceToRoad(pos2D);
            float distToHouse = GetMinDistanceToHouse(pos2D);

            if (distToRoad < roadAsphaltWidth) continue;
            if (distToHouse < buildingClearanceRadius) continue;

            float steepness = data.GetSteepness(nx, nz);
            if (steepness > 36f) continue;

            float terrainH = data.GetInterpolatedHeight(nx, nz);
            int protoIdx = Random.Range(0, prototypes.Count);
            float scale = Random.Range(0.8f, 1.3f);

            instances.Add(new TreeInstance
            {
                position = new Vector3(nx, terrainH / terrainSize.y, nz),
                prototypeIndex = protoIdx,
                widthScale = scale,
                heightScale = scale,
                color = Color.white,
                lightmapColor = Color.white,
                rotation = Random.Range(0f, Mathf.PI * 2f)
            });
            placedCount++;
        }

        data.SetTreeInstances(instances.ToArray(), true);
        EditorUtility.ClearProgressBar();
        Debug.Log($"[TerrainDecorator] 🌿 Placed a total of {instances.Count} foliage instances!");
    }

    // ════════════════════════════════════════════════════════════════
    //  6. ATMOSPHERE & POST-PROCESSING
    // ════════════════════════════════════════════════════════════════
    public void ApplyAtmosphere()
    {
        if (globalVolumePrefab != null)
        {
            GameObject volumeObj = GameObject.Find("GlobalVolume");
            if (volumeObj == null)
            {
                volumeObj = (GameObject)PrefabUtility.InstantiatePrefab(globalVolumePrefab);
                Undo.RegisterCreatedObjectUndo(volumeObj, "Instantiate GlobalVolume");
                Debug.Log("[TerrainDecorator] 🌟 GlobalVolume instantiated!");
            }
        }

        var cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            var comp = cam.GetComponent("UniversalAdditionalCameraData");
            if (comp != null)
            {
                var prop = comp.GetType().GetProperty("renderPostProcessing");
                if (prop != null)
                {
                    prop.SetValue(comp, true);
                    EditorUtility.SetDirty(cam);
                    Debug.Log("[TerrainDecorator] 📷 Post-Processing enabled on camera.");
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  OBSTACLE & SCENE DETECTION
    // ════════════════════════════════════════════════════════════════
    public void ScanSceneObstacles()
    {
        cachedHousePositions.Clear();
        cachedRoadPoints.Clear();

        if (!string.IsNullOrEmpty(buildingTag))
        {
            try
            {
                var gos = GameObject.FindGameObjectsWithTag(buildingTag);
                foreach (var go in gos)
                    cachedHousePositions.Add(new Vector2(go.transform.position.x, go.transform.position.z));
            }
            catch {}
        }

        if (!string.IsNullOrEmpty(roadTag))
        {
            try
            {
                var roads = GameObject.FindGameObjectsWithTag(roadTag);
                foreach (var r in roads)
                    cachedRoadPoints.Add(new Vector2(r.transform.position.x, r.transform.position.z));
            }
            catch {}
        }

        // Fallback default sample positions if scene tags are empty
        if (cachedHousePositions.Count == 0)
        {
            cachedHousePositions.Add(new Vector2(465.8f, 349.1f));
            cachedHousePositions.Add(new Vector2(465.8f, 381.2f));
            cachedHousePositions.Add(new Vector2(502.0f, 349.1f));
            cachedHousePositions.Add(new Vector2(502.0f, 381.2f));
            cachedHousePositions.Add(new Vector2(555.2f, 499.5f));
            cachedHousePositions.Add(new Vector2(624.0f, 450.0f));
        }

        if (cachedRoadPoints.Count == 0)
        {
            cachedRoadPoints.Add(new Vector2(483f, 500f));
            cachedRoadPoints.Add(new Vector2(537f, 500f));
            cachedRoadPoints.Add(new Vector2(420f, 501f));
            cachedRoadPoints.Add(new Vector2(484f, 710f));
            cachedRoadPoints.Add(new Vector2(483f, 315f));
        }

        Debug.Log($"[TerrainDecorator] 🔍 Scanned scene: {cachedHousePositions.Count} buildings, {cachedRoadPoints.Count} road waypoints.");
    }

    private float GetMinDistanceToRoad(Vector2 pos)
    {
        if (cachedRoadPoints.Count == 0) return 9999f;
        float minSq = float.MaxValue;
        for (int i = 0; i < cachedRoadPoints.Count; i++)
        {
            float dx = pos.x - cachedRoadPoints[i].x;
            float dz = pos.y - cachedRoadPoints[i].y;
            float sq = dx * dx + dz * dz;
            if (sq < minSq) minSq = sq;
        }
        return Mathf.Sqrt(minSq);
    }

    private float GetMinDistanceToHouse(Vector2 pos)
    {
        if (cachedHousePositions.Count == 0) return 9999f;
        float minSq = float.MaxValue;
        for (int i = 0; i < cachedHousePositions.Count; i++)
        {
            float dx = pos.x - cachedHousePositions[i].x;
            float dz = pos.y - cachedHousePositions[i].y;
            float sq = dx * dx + dz * dz;
            if (sq < minSq) minSq = sq;
        }
        return Mathf.Sqrt(minSq);
    }

    // ════════════════════════════════════════════════════════════════
    //  AUTO-DETECT PROJECT ASSETS
    // ════════════════════════════════════════════════════════════════
    public void AutoDetectAssets(bool silent = false)
    {
        // 1. TerrainLit Material
        if (terrainLitMaterial == null)
        {
            string[] matGuids = AssetDatabase.FindAssets("TerrainLit t:Material");
            if (matGuids.Length > 0)
                terrainLitMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(matGuids[0]));
        }

        // 2. GlobalVolume Prefab
        if (globalVolumePrefab == null)
        {
            string[] volGuids = AssetDatabase.FindAssets("GlobalVolume t:Prefab");
            if (volGuids.Length > 0)
                globalVolumePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(volGuids[0]));
        }

        // 3. Terrain Layers
        if (layerGrassA == null) layerGrassA = FindAssetByNames<TerrainLayer>(new[] { "Grass_A", "Grass" });
        if (layerGrassB == null) layerGrassB = FindAssetByNames<TerrainLayer>(new[] { "Grass_B", "Grass_Dry" });
        if (layerForestSoil == null) layerForestSoil = FindAssetByNames<TerrainLayer>(new[] { "Grass_Soil_A", "Soil", "Dirt" });
        if (layerRoadPebbles == null) layerRoadPebbles = FindAssetByNames<TerrainLayer>(new[] { "Pebbles_B", "Gravel", "Pebbles" });
        if (layerCliffRock == null) layerCliffRock = FindAssetByNames<TerrainLayer>(new[] { "Cliff_Mossy_E", "Cliff", "Rock" });
        if (layerSummitRock == null) layerSummitRock = FindAssetByNames<TerrainLayer>(new[] { "Black_Sand_Rocks_B", "Summit", "MountainRock" });

        // 4. Rock Prefabs
        if (rockPrefabs.Count == 0)
        {
            AddPrefabsByNames(rockPrefabs, new[] { "Rock_Overgrown_A", "Rock_Overgrown_B", "Rock_A_01", "Rock_B_01", "Rock_C_01", "Rock_D" });
        }

        // 5. Trees
        if (forestTreePrefabs.Count == 0)
        {
            AddPrefabsByNames(forestTreePrefabs, new[] { "Pine_A", "Pine_B", "Pine_C", "Pine_D", "Conifer", "Cypress_Forest_Desktop" });
        }
        if (neighborhoodTreePrefabs.Count == 0)
        {
            AddPrefabsByNames(neighborhoodTreePrefabs, new[] { "TreeSmall_Static", "TreeLarge_Static" });
        }

        // 6. Bushes & Details
        if (bushPrefabs.Count == 0)
        {
            AddPrefabsByNames(bushPrefabs, new[] { "Bush_A", "Bush_B", "Bush_Red", "Shrub", "Fern_A", "Fern_B", "Plant_HedgeBush" });
        }

        // 7. Flowers
        if (flowerPrefabs.Count == 0)
        {
            AddPrefabsByNames(flowerPrefabs, new[] { "Heather_A", "Heather_B", "Plant_FlowersRed", "Plant_FlowersYellow", "Plant_FlowersWhite" });
        }

        if (!silent)
        {
            int total = rockPrefabs.Count + forestTreePrefabs.Count + neighborhoodTreePrefabs.Count + bushPrefabs.Count + flowerPrefabs.Count;
            EditorUtility.DisplayDialog("Asset Auto-Detection",
                $"Auto-detection completed!\nFound {total} prefabs and assigned biome layers.", "OK");
        }
    }

    private T FindAssetByNames<T>(string[] candidateNames) where T : Object
    {
        foreach (var name in candidateNames)
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        return null;
    }

    private void AddPrefabsByNames(List<GameObject> list, string[] names)
    {
        foreach (var name in names)
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:Prefab");
            foreach (var guid in guids)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && !list.Contains(go))
                {
                    list.Add(go);
                    break;
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  CLEANUP TOOLS
    // ════════════════════════════════════════════════════════════════
    public void ClearFoliage()
    {
        if (!ValidateTerrain()) return;
        Undo.RegisterCompleteObjectUndo(targetTerrain.terrainData, "Clear Foliage");
        targetTerrain.terrainData.SetTreeInstances(new TreeInstance[0], true);
        Debug.Log("[TerrainDecorator] 🗑️ All foliage cleared from terrain.");
    }

    public void ClearRocks()
    {
        GameObject rocksRoot = GameObject.Find("Rocks");
        if (rocksRoot != null)
        {
            Undo.DestroyObjectImmediate(rocksRoot);
            Debug.Log("[TerrainDecorator] 🗑️ All rock instances cleared.");
        }
    }

    public void FlattenHeightmap()
    {
        if (!ValidateTerrain()) return;
        Undo.RegisterCompleteObjectUndo(targetTerrain.terrainData, "Flatten Heightmap");
        var data = targetTerrain.terrainData;
        int res = data.heightmapResolution;
        data.SetHeights(0, 0, new float[res, res]);
        Debug.Log("[TerrainDecorator] 🗑️ Terrain heightmap flattened.");
    }
}
