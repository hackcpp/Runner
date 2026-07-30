using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class EndlessRunnerGame : MonoBehaviour
{
    private enum GameState
    {
        StartScreen,
        Playing,
        Celebrating,
        Recovering,
        Paused,
        GameOver,
        LevelComplete,
        CampaignComplete
    }

    private sealed class Obstacle
    {
        public GameObject Body;
        public RunnerObstacleKind Kind;
        public RunnerActionReward Reward = new RunnerActionReward();
        public float X;
        public float Z;
        public bool ActionClearObserved;
        public bool Passed;
    }

    private const float LaneWidth = RunnerMotor.DefaultLaneWidth;
    private const float SegmentLength = 14f;
    private const float LookAheadDistance = RunnerRunTuning.LookAheadDistance;
    private const float CleanupDistance = 24f;
    private const float StartingSpeed = RunnerRunTuning.StartingSpeed;
    private const float ObstacleDepthTolerance = 0.72f;
    private const float ObstacleLaneTolerance = 0.82f;
    private const float CheckpointRecoverySafeDistance = 16f;
    private const float FinishVisualObstacleDistance = 42f;
    private const float LevelCelebrationDuration = 1.6f;
    private const float CelebrationDeceleration = 12f;
    private const string BestDistanceKey = "EndlessRunner.HighScore";
    private const string BestScoreKey = "EndlessRunner.BestScore";
    private const string BestComboKey = "EndlessRunner.BestCombo";

    private static readonly Color RunnerColor = new Color(0.05f, 0.72f, 0.8f);
    private static readonly Color CrashColor = new Color(1f, 0.24f, 0.12f);

    private readonly List<GameObject> roadSegments = new List<GameObject>();
    private readonly List<GameObject> cityBlocks = new List<GameObject>();
    private readonly List<Obstacle> obstacles = new List<Obstacle>();
    private readonly RunnerComboTracker combo = new RunnerComboTracker();
    private readonly Vector3 cameraOffset = new Vector3(0f, 7.2f, -9.5f);

    private GameState state;
    private GameObject worldRoot;
    private RunnerWorldPool worldPool;
    private GameObject player;
    private GameObject playerVisualRoot;
    private GameObject playerBody;
    private GameObject playerShadow;
    private RunnerMotor runnerMotor;
    private RunnerTouchInput touchInput;
    private RunnerVisualRig runnerVisualRig;
    private RunnerMotionEffects runnerMotionEffects;
    private RunnerHud runnerHud;
    private RunnerCameraRig cameraRig;
    private Camera gameCamera;
    private Light sun;
    private ParticleSystem actionParticles;
    private ProceduralRunnerMusic music;
    private ProceduralRunnerSfx soundEffects;
    private RunnerPatternSequence patternSequence;

    private Material roadMaterial;
    private Material laneMaterial;
    private Material finishWhiteMaterial;
    private Material edgeMaterial;
    private Material playerMaterial;
    private Material playerAccentMaterial;
    private Material blockerMaterial;
    private Material hurdleMaterial;
    private Material overheadMaterial;
    private Material buildingMaterialA;
    private Material buildingMaterialB;
    private Material buildingMaterialC;
    private Material roofMaterial;
    private Material roofSeamMaterial;
    private Material parapetMaterial;
    private Material parapetCapMaterial;
    private Material utilityMaterial;
    private Material windowMaterial;
    private Material skyboxMaterial;

    private int nextSegmentIndex;
    private int actionClearCount;
    private int actionFeedbackPoints;
    private int bestCombo;
    private int activeLevelIndex;
    private int activeRunSeed;
    private int remainingLives;
    private int checkpointIndex;
    private int bestScore;
    private int? nextRunSeed;
    private float nextPatternZ;
    private float distance;
    private float furthestDistance;
    private float obstacleGenerationMinimumZ;
    private float highScore;
    private float currentSpeed;
    private float laneHintTimer;
    private float actionHintTimer;
    private float actionFeedbackTimer;
    private float celebrationTimer;
    private string actionHintSymbol;
    private bool jumpHintShown;
    private bool slideHintShown;
    private bool tutorialGenerated;

    public RunnerMotor Motor => runnerMotor;
    public RunnerTouchInput TouchInput => touchInput;
    public RunnerHud Hud => runnerHud;
    public RunnerVisualRig VisualRig => runnerVisualRig;
    public RunnerMotionEffects MotionEffects => runnerMotionEffects;
    public ProceduralRunnerMusic Music => music;
    public int CurrentScore => RunnerScore.CalculateWithBonus(furthestDistance, combo.TotalBonusScore);
    public int ActionClearCount => actionClearCount;
    public int ActionBonusScore => combo.TotalBonusScore;
    public int CurrentCombo => combo.ComboCount;
    public int RunBestCombo => combo.HighestCombo;
    public int BestCombo => bestCombo;
    public int ActiveObstacleCount => obstacles.Count;
    public int ActiveWorldCubeCount => CountActiveWorldCubes();
    public int PooledCubeCount => worldPool == null ? 0 : worldPool.PooledCubeCount;
    public int PooledObstacleRootCount => worldPool == null ? 0 : worldPool.PooledObstacleRootCount;
    public int TotalCreatedCubeCount => worldPool == null ? 0 : worldPool.TotalCreatedCubeCount;
    public int TotalCreatedObstacleRootCount => worldPool == null ? 0 : worldPool.TotalCreatedObstacleRootCount;
    public float Distance => distance;
    public int ActiveLevelNumber => ActiveLevel.Number;
    public int RemainingLives => remainingLives;
    public int CheckpointIndex => checkpointIndex;
    public float LevelTargetDistance => ActiveLevel.TargetDistance;
    public float RecoverySafetyDistance => CheckpointRecoverySafeDistance;
    public int FinishClearanceObstacleCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < obstacles.Count; index++)
            {
                if (Mathf.Abs(obstacles[index].Z - ActiveLevel.TargetDistance) <
                    RunnerRunTuning.FinishLineClearanceDistance)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public bool IsPlaying => state == GameState.Playing;
    public bool IsPaused => state == GameState.Paused;
    public bool IsCelebrating => state == GameState.Celebrating;
    public bool IsAwaitingRecovery => state == GameState.Recovering;
    public bool IsLevelComplete => state == GameState.LevelComplete;
    public bool IsCampaignComplete => state == GameState.CampaignComplete;
    public bool ExitRequested { get; private set; }

    private RunnerLevelDefinition ActiveLevel => RunnerLevelCatalog.Levels[activeLevelIndex];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateGame()
    {
        if (FindObjectOfType<EndlessRunnerGame>() != null)
        {
            return;
        }

        new GameObject("Endless Runner Game").AddComponent<EndlessRunnerGame>();
    }

    private void Awake()
    {
        highScore = PlayerPrefs.GetFloat(BestDistanceKey, 0f);
        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        bestCombo = PlayerPrefs.GetInt(BestComboKey, 0);
        CreateMaterials();
        ConfigureScene();
        music = ProceduralRunnerMusic.AttachTo(gameObject);
        soundEffects = ProceduralRunnerSfx.AttachTo(gameObject);
        CreatePlayer();
        CreateActionParticles();
        runnerHud = RunnerHud.AttachTo(
            gameObject,
            HandlePrimaryAction,
            PauseRun,
            ResumeRun,
            HandlePrimaryAction,
            RequestExit,
            BeginDebugLevel);
        touchInput = RunnerTouchInput.AttachTo(gameObject, runnerMotor);
        ResetRun(GameState.StartScreen);
        RunnerMediaCapture.AttachIfRequested(gameObject, this);
    }

    private void Update()
    {
        music.Tick(Time.unscaledDeltaTime);
        touchInput.Tick(state == GameState.Playing);

        if (Input.GetKeyDown(KeyCode.Escape) && HandleBackPressed())
        {
            return;
        }

        if (state == GameState.StartScreen)
        {
            if (CanSelectDebugLevel())
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                {
                    BeginDebugLevel(1);
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                {
                    BeginDebugLevel(2);
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                {
                    BeginDebugLevel(3);
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                BeginCampaign();
            }

            AnimateIdlePlayer();
            UpdateCamera();
            return;
        }

        if (state == GameState.GameOver)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.R))
            {
                RestartLevelFromCheckpoint();
            }

            UpdateCamera();
            return;
        }

        if (state == GameState.Recovering)
        {
            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.R))
            {
                ContinueAfterHit();
            }

            UpdateCamera();
            return;
        }

        if (state == GameState.LevelComplete || state == GameState.CampaignComplete)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                HandlePrimaryAction();
            }

            UpdateCamera();
            return;
        }

        if (state == GameState.Celebrating)
        {
            UpdateCelebration(Time.deltaTime);
            UpdateCamera();
            return;
        }

        if (state == GameState.Paused)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                ResumeRun();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            PauseRun();
            return;
        }

        AdvanceRunner();
        if (TryCompleteLevel())
        {
            UpdateCamera();
            return;
        }
        GenerateWorldAhead();
        CleanupWorldBehind();
        CheckObstacleHits();
        UpdateGameplayHints();
        UpdateFeedbackTimers();
        UpdateCamera();
    }

    private void LateUpdate()
    {
        UpdateHud();
    }

    public void StartRunForTests(int seed)
    {
        activeLevelIndex = 0;
        checkpointIndex = 0;
        nextRunSeed = seed;
        ResetRun(GameState.Playing);
    }

    public void StartLevelForTests(int levelNumber, int seed)
    {
        activeLevelIndex = Mathf.Clamp(levelNumber - 1, 0, RunnerLevelCatalog.Levels.Count - 1);
        checkpointIndex = 0;
        nextRunSeed = seed;
        ResetRun(GameState.Playing);
    }

    private void HandlePrimaryAction()
    {
        if (state == GameState.StartScreen || state == GameState.CampaignComplete)
        {
            BeginCampaign();
        }
        else if (state == GameState.GameOver)
        {
            RestartLevelFromCheckpoint();
        }
        else if (state == GameState.Recovering)
        {
            ContinueAfterHit();
        }
        else if (state == GameState.LevelComplete)
        {
            activeLevelIndex++;
            ResetRun(GameState.Playing);
        }
    }

    private void BeginCampaign()
    {
        activeLevelIndex = 0;
        ResetRun(GameState.Playing);
    }

    private void BeginDebugLevel(int levelNumber)
    {
        if (!CanSelectDebugLevel())
        {
            return;
        }

        activeLevelIndex = Mathf.Clamp(levelNumber - 1, 0, RunnerLevelCatalog.Levels.Count - 1);
        ResetRun(GameState.Playing);
    }

    private static bool CanSelectDebugLevel()
    {
        return Debug.isDebugBuild && !Application.isMobilePlatform;
    }

    public void TakeHitForTests()
    {
        if (state == GameState.Playing)
        {
            HandleHit();
        }
    }

    public void CheckObstacleHitsForTests()
    {
        if (state == GameState.Playing)
        {
            CheckObstacleHits();
        }
    }

    public void AdvanceCelebrationForTests(float seconds)
    {
        if (state == GameState.Celebrating && seconds > 0f)
        {
            UpdateCelebration(seconds);
        }
    }

    public void PauseForTests()
    {
        PauseRun();
    }

    public void ResumeForTests()
    {
        ResumeRun();
    }

    public void BackForTests()
    {
        HandleBackPressed();
    }

    public void EndRunForTests()
    {
        if (state == GameState.Playing)
        {
            FailLevel();
        }
    }

    public void AdvanceWorldForTests(float meters)
    {
        if (state != GameState.Playing || meters <= 0f)
        {
            return;
        }

        Vector3 position = player.transform.position;
        position.z += meters;
        player.transform.position = position;
        distance = Mathf.Max(distance, position.z);
        furthestDistance = Mathf.Max(furthestDistance, distance);
        currentSpeed = ActiveLevel.MaximumSpeed;
        UpdateCheckpoint();
        UpdateMusicIntensity();
        if (TryCompleteLevel())
        {
            return;
        }
        GenerateWorldAhead();
        CleanupWorldBehind();
    }

    private void UpdateHud()
    {
        if (runnerHud == null)
        {
            return;
        }

        RunnerHudMode mode = state == GameState.StartScreen
            ? RunnerHudMode.Start
            : state == GameState.Playing
                ? RunnerHudMode.Playing
                : state == GameState.Celebrating
                    ? RunnerHudMode.Celebrating
                    : state == GameState.Recovering
                        ? RunnerHudMode.Recovering
                        : state == GameState.Paused
                            ? RunnerHudMode.Paused
                            : state == GameState.LevelComplete
                                ? RunnerHudMode.LevelComplete
                                : state == GameState.CampaignComplete
                                    ? RunnerHudMode.CampaignComplete
                                    : RunnerHudMode.GameOver;

        string hintSymbol = null;
        float hintAlpha = 0f;
        if (laneHintTimer > 0f)
        {
            hintSymbol = "\u2190    \u2192";
            hintAlpha = Mathf.Clamp01(laneHintTimer * 2f);
        }
        else if (actionHintTimer > 0f && !string.IsNullOrEmpty(actionHintSymbol))
        {
            hintSymbol = actionHintSymbol;
            hintAlpha = Mathf.Clamp01(actionHintTimer * 2f);
        }

        runnerHud.Render(new RunnerHudViewModel(
            mode,
            CurrentScore,
            Mathf.FloorToInt(distance),
            bestScore,
            actionClearCount,
            combo.HighestCombo,
            combo.Multiplier,
            combo.ComboCount > 0
                ? Mathf.Clamp01(combo.RemainingTime / RunnerComboTracker.ComboWindow)
                : 0f,
            actionFeedbackPoints,
            Mathf.Clamp01(actionFeedbackTimer * 2f),
            hintSymbol,
            hintAlpha,
            Application.isMobilePlatform,
            ActiveLevel.Number,
            RunnerLevelCatalog.Levels.Count,
            Mathf.FloorToInt(ActiveLevel.TargetDistance),
            remainingLives,
            RunnerLevelCatalog.LivesPerLevel,
            checkpointIndex,
            CanSelectDebugLevel()));
    }

    private void CreateMaterials()
    {
        roadMaterial = CreateMaterial("Rooftop Surface", new Color(0.16f, 0.17f, 0.19f), 0f, 0f, 0.1f);
        laneMaterial = CreateMaterial("Signal Gold", new Color(0.96f, 0.72f, 0.18f), 0.18f, 0.05f, 0.22f);
        finishWhiteMaterial = CreateMaterial("Finish White", new Color(0.94f, 0.96f, 0.98f), 0.12f, 0f, 0.18f);
        edgeMaterial = CreateMaterial("Deep Graphite", new Color(0.07f, 0.09f, 0.11f), 0f, 0.05f, 0.12f);
        playerMaterial = CreateMaterial("Player", RunnerColor, 0f, 0.05f, 0.32f);
        playerAccentMaterial = CreateMaterial("Player Accent", new Color(1f, 0.86f, 0.52f), 0.1f, 0.05f, 0.4f);
        blockerMaterial = CreateMaterial("Blocker", new Color(0.92f, 0.18f, 0.16f), 0.28f, 0.05f, 0.28f);
        hurdleMaterial = CreateMaterial("Hurdle", new Color(1f, 0.52f, 0.06f), 0.26f, 0.05f, 0.28f);
        overheadMaterial = CreateMaterial("Overhead", new Color(0.55f, 0.2f, 0.72f), 0.3f, 0.05f, 0.3f);
        buildingMaterialA = CreateMaterial("Building Slate", new Color(0.24f, 0.29f, 0.35f), 0f, 0f, 0.1f);
        buildingMaterialB = CreateMaterial("Building Teal", new Color(0.2f, 0.38f, 0.39f), 0f, 0f, 0.12f);
        buildingMaterialC = CreateMaterial("Building Brick", new Color(0.4f, 0.27f, 0.3f), 0f, 0f, 0.1f);
        roofMaterial = CreateMaterial("Roof Tops", new Color(0.09f, 0.12f, 0.14f), 0f, 0f, 0.08f);
        roofSeamMaterial = CreateMaterial("Roof Seams", new Color(0.055f, 0.065f, 0.075f), 0f, 0f, 0.05f);
        parapetMaterial = CreateMaterial("Parapet", new Color(0.35f, 0.37f, 0.38f), 0f, 0f, 0.12f);
        parapetCapMaterial = CreateMaterial("Parapet Cap", new Color(0.62f, 0.57f, 0.49f), 0f, 0f, 0.16f);
        utilityMaterial = CreateMaterial("Roof Utilities", new Color(0.38f, 0.47f, 0.49f), 0f, 0.25f, 0.2f);
        windowMaterial = CreateMaterial("Warm Windows", new Color(1f, 0.5f, 0.16f), 0.72f, 0f, 0.18f);
    }

    private Material CreateMaterial(
        string materialName,
        Color color,
        float emissionStrength = 0f,
        float metallic = 0f,
        float smoothness = 0.16f)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };

        if (emissionStrength > 0f && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emissionStrength);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", smoothness);
        }

        return material;
    }

    private void ConfigureScene()
    {
        worldRoot = new GameObject("Generated Runner World");
        worldPool = new RunnerWorldPool(worldRoot.transform);

        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            gameCamera = new GameObject("Main Camera").AddComponent<Camera>();
            gameCamera.tag = "MainCamera";
            gameCamera.gameObject.AddComponent<AudioListener>();
        }

        gameCamera.clearFlags = CameraClearFlags.Skybox;
        gameCamera.fieldOfView = 58f;
        gameCamera.nearClipPlane = 0.1f;
        gameCamera.farClipPlane = 260f;
        gameCamera.backgroundColor = new Color(0.24f, 0.34f, 0.45f);
        cameraRig = RunnerCameraRig.AttachTo(gameCamera, cameraOffset);

        ConfigureSkybox();
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.57f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.3f, 0.36f, 0.43f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.13f, 0.17f);
        RenderSettings.ambientIntensity = 0.9f;
        RenderSettings.reflectionIntensity = 0.38f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.27f, 0.36f, 0.46f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 72f;
        RenderSettings.fogEndDistance = 205f;

        sun = FindObjectOfType<Light>();
        if (sun == null)
        {
            sun = new GameObject("Directional Light").AddComponent<Light>();
        }

        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.76f, 0.58f);
        sun.intensity = 1.42f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.72f;
        sun.shadowBias = 0.06f;
        sun.transform.rotation = Quaternion.Euler(32f, -38f, 8f);
        RenderSettings.sun = sun;
    }

    private void ConfigureSkybox()
    {
        Shader skyboxShader = Shader.Find("Skybox/Procedural");
        if (skyboxShader == null)
        {
            return;
        }

        skyboxMaterial = new Material(skyboxShader)
        {
            name = "Rooftop Dusk Sky"
        };
        skyboxMaterial.SetColor("_SkyTint", new Color(0.25f, 0.42f, 0.58f));
        skyboxMaterial.SetColor("_GroundColor", new Color(0.16f, 0.13f, 0.18f));
        skyboxMaterial.SetFloat("_AtmosphereThickness", 0.72f);
        skyboxMaterial.SetFloat("_SunSize", 0.035f);
        skyboxMaterial.SetFloat("_SunSizeConvergence", 7f);
        skyboxMaterial.SetFloat("_Exposure", 1.08f);
        RenderSettings.skybox = skyboxMaterial;
    }

    private void ApplyLevelPresentation()
    {
        if (skyboxMaterial == null || sun == null)
        {
            return;
        }

        if (ActiveLevel.Mood == RunnerLevelMood.GoldenSunset)
        {
            skyboxMaterial.SetColor("_SkyTint", new Color(0.58f, 0.31f, 0.2f));
            RenderSettings.fogColor = new Color(0.48f, 0.28f, 0.23f);
            RenderSettings.fogStartDistance = 72f;
            RenderSettings.fogEndDistance = 205f;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.44f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.29f, 0.25f);
            sun.color = new Color(1f, 0.61f, 0.36f);
            sun.intensity = 1.18f;
            return;
        }

        if (ActiveLevel.Mood == RunnerLevelMood.VioletNight)
        {
            skyboxMaterial.SetColor("_SkyTint", new Color(0.31f, 0.27f, 0.56f));
            RenderSettings.fogColor = new Color(0.28f, 0.27f, 0.47f);
            RenderSettings.fogStartDistance = 96f;
            RenderSettings.fogEndDistance = 260f;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.43f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.31f, 0.52f);
            sun.color = new Color(0.78f, 0.72f, 1f);
            sun.intensity = 1.16f;
            return;
        }

        skyboxMaterial.SetColor("_SkyTint", new Color(0.25f, 0.42f, 0.58f));
        RenderSettings.fogColor = new Color(0.27f, 0.36f, 0.46f);
        RenderSettings.fogStartDistance = 72f;
        RenderSettings.fogEndDistance = 205f;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.57f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.3f, 0.36f, 0.43f);
        sun.color = new Color(1f, 0.76f, 0.58f);
        sun.intensity = 1.42f;
    }

    private void CreatePlayer()
    {
        player = new GameObject("Runner");
        playerVisualRoot = new GameObject("Runner Visual");
        playerVisualRoot.transform.SetParent(player.transform);

        playerBody = new GameObject("Runner Logic Body");
        playerBody.transform.SetParent(playerVisualRoot.transform);
        playerBody.transform.localPosition = new Vector3(0f, 0.8f, 0f);

        playerShadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        playerShadow.name = "Runner Shadow";
        playerShadow.transform.SetParent(player.transform);
        playerShadow.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        playerShadow.transform.localScale = new Vector3(0.82f, 0.025f, 0.82f);
        RunnerWorldPool.RemovePhysicsCollider(playerShadow);
        SetMaterial(playerShadow, edgeMaterial);

        runnerMotor = player.AddComponent<RunnerMotor>();
        runnerMotor.Configure(
            playerVisualRoot.transform,
            playerBody.transform,
            playerShadow.transform,
            LaneWidth);

        runnerVisualRig = player.AddComponent<RunnerVisualRig>();
        runnerVisualRig.Configure(
            runnerMotor,
            playerVisualRoot.transform,
            playerMaterial,
            playerAccentMaterial,
            edgeMaterial);

        runnerMotionEffects = player.AddComponent<RunnerMotionEffects>();
        runnerMotionEffects.Configure(
            runnerMotor,
            parapetCapMaterial,
            playerAccentMaterial,
            playerMaterial);
    }

    private void CreateActionParticles()
    {
        GameObject particleObject = new GameObject("Action Clear Particles");
        actionParticles = particleObject.AddComponent<ParticleSystem>();
        actionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = actionParticles.main;
        main.duration = 0.45f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.42f;
        main.startSpeed = 2.2f;
        main.startSize = 0.13f;
        main.startColor = new Color(1f, 0.84f, 0.2f);
        main.maxParticles = 32;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = actionParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = actionParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.24f;

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = laneMaterial;
    }

    private void ResetRun(GameState nextState)
    {
        RestoreGlobalPauseState();
        state = nextState;
        distance = 0f;
        furthestDistance = 0f;
        obstacleGenerationMinimumZ = float.NegativeInfinity;
        remainingLives = RunnerLevelCatalog.LivesPerLevel;
        checkpointIndex = 0;
        currentSpeed = ActiveLevel.StartingSpeed;
        nextSegmentIndex = -1;
        nextPatternZ = RunnerRunTuning.FirstRandomPatternZ;
        actionClearCount = 0;
        actionFeedbackPoints = RunnerScore.ActionClearPoints;
        laneHintTimer = nextState == GameState.Playing
            ? Application.isMobilePlatform ? 3.2f : 2.4f
            : 0f;
        actionHintTimer = 0f;
        actionFeedbackTimer = 0f;
        celebrationTimer = 0f;
        cameraRig.ResetFeedback();
        actionHintSymbol = null;
        jumpHintShown = false;
        slideHintShown = false;
        tutorialGenerated = false;

        activeRunSeed = nextRunSeed ?? ActiveLevel.Seed;
        nextRunSeed = null;
        patternSequence = new RunnerPatternSequence(activeRunSeed);
        combo.Reset();

        runnerMotor.ResetForRun();
        runnerVisualRig.ResetForRun();
        runnerMotionEffects.ResetForRun();
        playerMaterial.color = RunnerColor;
        actionParticles.Clear();

        ApplyLevelPresentation();
        ClearWorld();
        GenerateWorldAhead();
        music.SetState(nextState == GameState.Playing
            ? RunnerMusicState.RunningLow
            : RunnerMusicState.Menu);
        UpdateCamera(true);
    }

    private void RestartLevelFromCheckpoint()
    {
        int restartCheckpoint = checkpointIndex;
        ResetRun(GameState.Playing);
        RestoreCheckpoint(restartCheckpoint, false);
    }

    private void RestoreCheckpoint(int targetCheckpoint, bool preserveFurthestDistance)
    {
        float previousFurthestDistance = furthestDistance;
        checkpointIndex = Mathf.Clamp(targetCheckpoint, 0, RunnerLevelCatalog.CheckpointCount);
        distance = ActiveLevel.CheckpointDistance(checkpointIndex);
        furthestDistance = preserveFurthestDistance
            ? Mathf.Max(previousFurthestDistance, distance)
            : distance;
        currentSpeed = ActiveLevel.SpeedAtDistance(distance);
        nextSegmentIndex = -1;
        nextPatternZ = RunnerRunTuning.FirstRandomPatternZ;
        tutorialGenerated = false;
        patternSequence = new RunnerPatternSequence(activeRunSeed);
        obstacleGenerationMinimumZ = distance + CheckpointRecoverySafeDistance;
        SkipPatternsBeforeRecoveryBuffer();
        runnerMotor.ResetForRun();
        runnerVisualRig.ResetForRun();
        runnerMotionEffects.ResetForRun();
        player.transform.position = new Vector3(0f, 0f, distance);
        playerMaterial.color = RunnerColor;
        playerVisualRoot.transform.localRotation = Quaternion.identity;
        actionParticles.Clear();
        ClearWorld();
        GenerateWorldAhead();
        CleanupWorldBehind();
        UpdateCamera(true);
    }

    private void SkipPatternsBeforeRecoveryBuffer()
    {
        while (nextPatternZ < obstacleGenerationMinimumZ)
        {
            int tier = ActiveLevel.TierForDistance(
                RunnerRunTuning.GenerationDistanceForPattern(nextPatternZ));
            RunnerPatternDefinition pattern = patternSequence.Next(tier);
            nextPatternZ += pattern.Length + patternSequence.NextSpacing();
        }
    }

    private void AdvanceRunner()
    {
        currentSpeed = Mathf.Min(
            ActiveLevel.MaximumSpeed,
            currentSpeed + Time.deltaTime * ActiveLevel.SpeedAcceleration);

        runnerMotor.Tick(currentSpeed);
        runnerVisualRig.Tick(currentSpeed, true, Time.deltaTime);
        runnerMotionEffects.Tick(currentSpeed, true, Time.deltaTime);

        if (runnerVisualRig.FootstepThisFrame)
        {
            int footSide = runnerVisualRig.FootstepSideThisFrame;
            soundEffects.PlayFootstep(footSide);
            runnerMotionEffects.EmitFootstep(footSide);
        }

        if (runnerMotor.JumpStartedThisFrame)
        {
            soundEffects.PlayJump();
            music.TriggerDuck();
            cameraRig.PulseFieldOfView(1.1f);
            if (actionHintSymbol == "\u2191")
            {
                actionHintTimer = 0f;
            }
        }

        if (runnerMotor.SlideStartedThisFrame)
        {
            soundEffects.PlaySlide();
            music.TriggerDuck();
            cameraRig.PulseFieldOfView(0.75f);
            if (actionHintSymbol == "\u2193")
            {
                actionHintTimer = 0f;
            }
        }

        if (runnerMotor.LandedThisFrame)
        {
            cameraRig.TriggerImpact(0.08f, 0.45f);
            soundEffects.PlayLanding();
            music.TriggerDuck();
        }

        distance = Mathf.Max(distance, player.transform.position.z);
        furthestDistance = Mathf.Max(furthestDistance, distance);
        UpdateCheckpoint();
        UpdateMusicIntensity();
    }

    private void UpdateMusicIntensity()
    {
        music.SetState(distance >= RunnerRunTuning.AdvancedTierDistance
            ? RunnerMusicState.RunningHigh
            : RunnerMusicState.RunningLow);
    }

    private void UpdateCheckpoint()
    {
        while (checkpointIndex < RunnerLevelCatalog.CheckpointCount &&
               distance >= ActiveLevel.CheckpointDistance(checkpointIndex + 1))
        {
            checkpointIndex++;
            actionFeedbackPoints = RunnerScore.ActionClearPoints;
            actionFeedbackTimer = 0.72f;
            actionParticles.transform.position = player.transform.position + new Vector3(0f, 1f, 0f);
            actionParticles.Emit(18);
            soundEffects.PlayClear(1);
        }
    }

    private bool TryCompleteLevel()
    {
        if (distance < ActiveLevel.TargetDistance)
        {
            return false;
        }

        BeginLevelCelebration();
        return true;
    }

    private void BeginLevelCelebration()
    {
        state = GameState.Celebrating;
        celebrationTimer = LevelCelebrationDuration;
        actionHintTimer = 0f;
        actionFeedbackTimer = 0f;
        playerMaterial.color = RunnerColor;
        playerVisualRoot.transform.localRotation = Quaternion.identity;
        actionParticles.transform.position = player.transform.position + new Vector3(0f, 1.1f, 0f);
        actionParticles.Emit(activeLevelIndex >= RunnerLevelCatalog.Levels.Count - 1 ? 72 : 48);
        cameraRig.PulseFieldOfView(activeLevelIndex >= RunnerLevelCatalog.Levels.Count - 1 ? 2.4f : 1.6f);
        cameraRig.TriggerImpact(0.12f, 0.8f);
        soundEffects.PlayClear(RunnerComboTracker.MaximumMultiplier);
        music.TriggerDuck();
    }

    private void UpdateCelebration(float deltaTime)
    {
        float step = Mathf.Max(0f, deltaTime);
        currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, CelebrationDeceleration * step);
        player.transform.position += Vector3.forward * currentSpeed * step;
        runnerVisualRig.Tick(currentSpeed, currentSpeed > 0.1f, step);
        runnerMotionEffects.Tick(currentSpeed, currentSpeed > 0.1f, step);
        celebrationTimer = Mathf.Max(0f, celebrationTimer - step);
        if (celebrationTimer <= 0f)
        {
            CompleteLevel();
        }
    }

    private void AnimateIdlePlayer()
    {
        runnerVisualRig.Tick(0f, false, Time.deltaTime);
        playerVisualRoot.transform.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f) * 0.03f, 0f);
        playerVisualRoot.transform.localRotation = Quaternion.identity;
        playerVisualRoot.transform.localScale = Vector3.one;
        player.transform.rotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.8f) * 4f, 0f);
    }

    private void GenerateWorldAhead()
    {
        float playerZ = player.transform.position.z;
        int targetSegmentIndex = Mathf.CeilToInt((playerZ + LookAheadDistance) / SegmentLength);

        while (nextSegmentIndex <= targetSegmentIndex)
        {
            CreateRoadSegment(nextSegmentIndex);
            CreateCitySlice(nextSegmentIndex);
            nextSegmentIndex++;
        }

        if (state != GameState.Playing)
        {
            return;
        }

        if (!tutorialGenerated)
        {
            CreateObstacleMask(
                RunnerObstacleKind.Blocker,
                1 << 1,
                RunnerRunTuning.TutorialLaneChangeZ);
            CreateObstacleMask(
                RunnerObstacleKind.Hurdle,
                0b111,
                RunnerRunTuning.TutorialJumpZ);
            CreateObstacleMask(
                RunnerObstacleKind.Overhead,
                0b111,
                RunnerRunTuning.TutorialSlideZ);
            tutorialGenerated = true;
        }

        float generationLimit = Mathf.Min(
            playerZ + LookAheadDistance,
            ActiveLevel.TargetDistance + FinishVisualObstacleDistance);
        while (nextPatternZ < generationLimit)
        {
            int tier = ActiveLevel.TierForDistance(
                RunnerRunTuning.GenerationDistanceForPattern(nextPatternZ));
            RunnerPatternDefinition pattern = patternSequence.Next(tier);
            CreatePattern(pattern, nextPatternZ);
            nextPatternZ += pattern.Length + patternSequence.NextSpacing();
        }
    }

    private void CreatePattern(RunnerPatternDefinition pattern, float startZ)
    {
        for (int elementIndex = 0; elementIndex < pattern.Elements.Count; elementIndex++)
        {
            RunnerPatternElement element = pattern.Elements[elementIndex];
            CreateObstacleMask(element.Kind, element.LaneMask, startZ + element.ZOffset);
        }
    }

    private void CreateObstacleMask(RunnerObstacleKind kind, int laneMask, float z)
    {
        if (z > ActiveLevel.TargetDistance + FinishVisualObstacleDistance ||
            z < obstacleGenerationMinimumZ ||
            Mathf.Abs(z - ActiveLevel.TargetDistance) <
            RunnerRunTuning.FinishLineClearanceDistance)
        {
            return;
        }

        for (int lane = 0; lane < 3; lane++)
        {
            if ((laneMask & (1 << lane)) != 0)
            {
                CreateObstacle(kind, lane, z);
            }
        }
    }

    private void CreateRoadSegment(int segmentIndex)
    {
        float centerZ = segmentIndex * SegmentLength + SegmentLength * 0.5f;

        CreateTrackedCube(
            roadSegments,
            "Rooftop Slab " + segmentIndex,
            new Vector3(0f, -0.18f, centerZ),
            new Vector3(8.12f, 0.36f, SegmentLength),
            roadMaterial);

        CreateTrackedCube(
            roadSegments,
            "Left Parapet",
            new Vector3(-4.05f, 0.24f, centerZ),
            new Vector3(0.28f, 0.84f, SegmentLength),
            parapetMaterial);

        CreateTrackedCube(
            roadSegments,
            "Right Parapet",
            new Vector3(4.05f, 0.24f, centerZ),
            new Vector3(0.28f, 0.84f, SegmentLength),
            parapetMaterial);

        for (int side = -1; side <= 1; side += 2)
        {
            CreateTrackedCube(
                roadSegments,
                side < 0 ? "Left Parapet Cap" : "Right Parapet Cap",
                new Vector3(side * 4.05f, 0.7f, centerZ),
                new Vector3(0.46f, 0.1f, SegmentLength),
                parapetCapMaterial);

            CreateTrackedCube(
                roadSegments,
                "Roof Lane Seam",
                new Vector3(side * LaneWidth * 0.5f, 0.026f, centerZ),
                new Vector3(0.035f, 0.025f, SegmentLength),
                roofSeamMaterial);
        }

        CreateTrackedCube(
            roadSegments,
            "Roof Expansion Joint",
            new Vector3(0f, 0.027f, segmentIndex * SegmentLength + 0.04f),
            new Vector3(7.58f, 0.026f, 0.065f),
            roofSeamMaterial);

        CreatePlayableRoofFixture(segmentIndex, centerZ);

        float segmentStartZ = segmentIndex * SegmentLength;
        if (ActiveLevel.TargetDistance >= segmentStartZ &&
            ActiveLevel.TargetDistance < segmentStartZ + SegmentLength)
        {
            CreateFinishLine(ActiveLevel.TargetDistance);
        }
    }

    private void CreateFinishLine(float z)
    {
        const int columnCount = 12;
        const int rowCount = 2;
        const float tileWidth = 0.62f;
        const float tileDepth = 0.44f;

        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                float x = (column - (columnCount - 1) * 0.5f) * tileWidth;
                float tileZ = z + (row - (rowCount - 1) * 0.5f) * tileDepth;
                CreateTrackedCube(
                    roadSegments,
                    "Finish Line Tile " + row + " " + column,
                    new Vector3(x, 0.046f, tileZ),
                    new Vector3(tileWidth, 0.038f, tileDepth),
                    (row + column) % 2 == 0 ? finishWhiteMaterial : edgeMaterial);
            }
        }

        float borderOffset = rowCount * tileDepth * 0.5f + 0.08f;
        for (int side = -1; side <= 1; side += 2)
        {
            CreateTrackedCube(
                roadSegments,
                side < 0 ? "Finish Line Near Border" : "Finish Line Far Border",
                new Vector3(0f, 0.05f, z + side * borderOffset),
                new Vector3(columnCount * tileWidth, 0.045f, 0.12f),
                laneMaterial);
        }
    }

    private void CreatePlayableRoofFixture(int segmentIndex, float centerZ)
    {
        int variant = Math.Abs(segmentIndex % 3);
        float side = segmentIndex % 2 == 0 ? 1f : -1f;
        float x = side * 3.42f;
        float z = centerZ + (variant - 1) * 1.8f;

        if (variant == 0)
        {
            CreateTrackedCube(
                roadSegments,
                "Rooftop HVAC Unit",
                new Vector3(x, 0.24f, z),
                new Vector3(0.82f, 0.48f, 1.12f),
                utilityMaterial);
            CreateTrackedCube(
                roadSegments,
                "Rooftop HVAC Fan",
                new Vector3(x, 0.52f, z),
                new Vector3(0.56f, 0.08f, 0.76f),
                edgeMaterial);
            return;
        }

        if (variant == 1)
        {
            GameObject skylight = CreateTrackedCube(
                roadSegments,
                "Rooftop Skylight",
                new Vector3(x, 0.14f, z),
                new Vector3(0.92f, 0.2f, 1.3f),
                utilityMaterial);
            skylight.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            CreateTrackedCube(
                roadSegments,
                "Rooftop Skylight Base",
                new Vector3(x, 0.055f, z),
                new Vector3(1.06f, 0.1f, 1.44f),
                edgeMaterial);
            return;
        }

        CreateTrackedCube(
            roadSegments,
            "Rooftop Vent Stack",
            new Vector3(x, 0.38f, z),
            new Vector3(0.46f, 0.76f, 0.46f),
            utilityMaterial);
        CreateTrackedCube(
            roadSegments,
            "Rooftop Vent Cap",
            new Vector3(x, 0.79f, z),
            new Vector3(0.68f, 0.1f, 0.68f),
            edgeMaterial);
    }

    private void CreateCitySlice(int segmentIndex)
    {
        System.Random random = new System.Random(segmentIndex * 92821 + 37);
        float centerZ = segmentIndex * SegmentLength + SegmentLength * 0.5f;

        for (int side = -1; side <= 1; side += 2)
        {
            for (int index = 0; index < 3; index++)
            {
                float width = RandomRange(random, 3.2f + index, 5.4f + index * 1.4f);
                float depth = RandomRange(random, 4.8f + index, 8.2f + index * 1.6f);
                float height = RandomRange(random, 5.5f + index * 1.8f, 11.5f + index * 3.2f);
                float minimumX = index == 0 ? 7.4f : index == 1 ? 12.2f : 20f;
                float maximumX = index == 0 ? 10.4f : index == 1 ? 17.8f : 29f;
                float x = side * RandomRange(random, minimumX, maximumX);
                float z = centerZ + RandomRange(random, -SegmentLength * 0.45f, SegmentLength * 0.45f);
                float roofLevel = index == 0
                    ? RandomRange(random, -3.1f, -0.8f)
                    : index == 1
                        ? RandomRange(random, -2.5f, 1.2f)
                        : RandomRange(random, -3.8f, 5.2f);
                float y = roofLevel - height * 0.5f;

                double materialChoice = random.NextDouble();
                Material material = materialChoice < 0.34
                    ? buildingMaterialA
                    : materialChoice < 0.68
                        ? buildingMaterialB
                        : buildingMaterialC;
                CreateTrackedCube(
                    cityBlocks,
                    "Background Building",
                    new Vector3(x, y, z),
                    new Vector3(width, height, depth),
                    material);

                CreateTrackedCube(
                    cityBlocks,
                    "Background Roof",
                    new Vector3(x, roofLevel + 0.05f, z),
                    new Vector3(width * 1.04f, 0.1f, depth * 1.04f),
                    roofMaterial);

                if (index == 0)
                {
                    CreateWindowBand(side, x, z, y, width, depth, height);

                    int fixtureSide = segmentIndex % 2 == 0 ? 1 : -1;
                    if (side == fixtureSide)
                    {
                        CreateBackgroundRoofFixture(segmentIndex, side, x, z, roofLevel, width, depth);
                    }
                }
            }
        }
    }

    private void CreateWindowBand(
        int side,
        float x,
        float z,
        float buildingCenterY,
        float width,
        float depth,
        float height)
    {
        float bandY = buildingCenterY + height * 0.5f - Mathf.Min(1.35f, height * 0.22f);
        GameObject windows = CreateTrackedCube(
            cityBlocks,
            "Lit Window Band",
            new Vector3(x - side * (width * 0.5f + 0.035f), bandY, z),
            new Vector3(0.07f, 0.24f, depth * 0.58f),
            windowMaterial);
        Renderer windowRenderer = windows.GetComponent<Renderer>();
        if (windowRenderer != null)
        {
            windowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            windowRenderer.receiveShadows = false;
        }
    }

    private void CreateBackgroundRoofFixture(
        int segmentIndex,
        int side,
        float roofX,
        float roofZ,
        float roofLevel,
        float roofWidth,
        float roofDepth)
    {
        int variant = Math.Abs((segmentIndex + (side > 0 ? 1 : 0)) % 3);
        float x = roofX - side * Mathf.Min(0.7f, roofWidth * 0.18f);
        float z = roofZ + Mathf.Min(0.8f, roofDepth * 0.16f);

        if (variant == 0)
        {
            CreateTrackedCube(
                cityBlocks,
                "Background Water Tank",
                new Vector3(x, roofLevel + 0.55f, z),
                new Vector3(Mathf.Min(1.35f, roofWidth * 0.42f), 0.72f, Mathf.Min(1.08f, roofDepth * 0.28f)),
                utilityMaterial);
            for (int leg = -1; leg <= 1; leg += 2)
            {
                CreateTrackedCube(
                    cityBlocks,
                    "Water Tank Support",
                    new Vector3(x + leg * 0.32f, roofLevel + 0.16f, z),
                    new Vector3(0.12f, 0.32f, 0.12f),
                    edgeMaterial);
            }
            return;
        }

        if (variant == 1)
        {
            CreateTrackedCube(
                cityBlocks,
                "Background HVAC",
                new Vector3(x, roofLevel + 0.28f, z),
                new Vector3(1.08f, 0.54f, 0.92f),
                utilityMaterial);
            CreateTrackedCube(
                cityBlocks,
                "Background HVAC Top",
                new Vector3(x, roofLevel + 0.58f, z),
                new Vector3(0.72f, 0.08f, 0.58f),
                edgeMaterial);
            return;
        }

        CreateTrackedCube(
            cityBlocks,
            "Background Antenna Mast",
            new Vector3(x, roofLevel + 0.92f, z),
            new Vector3(0.08f, 1.84f, 0.08f),
            utilityMaterial);
        CreateTrackedCube(
            cityBlocks,
            "Background Antenna Crossbar",
            new Vector3(x, roofLevel + 1.52f, z),
            new Vector3(0.82f, 0.07f, 0.07f),
            utilityMaterial);
    }

    private void CreateObstacle(RunnerObstacleKind kind, int lane, float z)
    {
        float x = LaneX(lane);
        GameObject root = AcquireObstacleRoot(kind + " Obstacle", new Vector3(x, 0f, z));

        if (kind == RunnerObstacleKind.Blocker)
        {
            CreateObstaclePart(
                root,
                "Tall Blocker",
                new Vector3(x, 1.08f, z),
                new Vector3(1.34f, 2.16f, 1.05f),
                blockerMaterial);
            CreateObstaclePart(
                root,
                "Blocker Cap",
                new Vector3(x, 2.22f, z),
                new Vector3(1.52f, 0.14f, 1.2f),
                edgeMaterial);
            CreateObstaclePart(
                root,
                "Blocker Approach Stripe",
                new Vector3(x, 0.035f, z - 1.22f),
                new Vector3(1.3f, 0.04f, 0.2f),
                blockerMaterial);
        }
        else if (kind == RunnerObstacleKind.Hurdle)
        {
            CreateObstaclePart(
                root,
                "Low Hurdle",
                new Vector3(x, 0.38f, z),
                new Vector3(1.48f, 0.76f, 0.78f),
                hurdleMaterial);
            CreateObstaclePart(
                root,
                "Hurdle Stripe",
                new Vector3(x, 0.54f, z - 0.4f),
                new Vector3(1.18f, 0.14f, 0.05f),
                edgeMaterial);
            CreateObstaclePart(
                root,
                "Hurdle Approach Stripe Wide",
                new Vector3(x, 0.035f, z - 1.02f),
                new Vector3(1.3f, 0.04f, 0.14f),
                hurdleMaterial);
            CreateObstaclePart(
                root,
                "Hurdle Approach Stripe Short",
                new Vector3(x, 0.035f, z - 1.38f),
                new Vector3(0.82f, 0.04f, 0.14f),
                hurdleMaterial);
        }
        else
        {
            CreateObstaclePart(
                root,
                "Left Gate Post",
                new Vector3(x - 0.68f, 0.76f, z),
                new Vector3(0.15f, 1.52f, 0.72f),
                overheadMaterial);
            CreateObstaclePart(
                root,
                "Right Gate Post",
                new Vector3(x + 0.68f, 0.76f, z),
                new Vector3(0.15f, 1.52f, 0.72f),
                overheadMaterial);
            CreateObstaclePart(
                root,
                "Overhead Beam",
                new Vector3(x, 1.55f, z),
                new Vector3(1.52f, 0.46f, 0.82f),
                overheadMaterial);
            CreateObstaclePart(
                root,
                "Overhead Left Approach Rail",
                new Vector3(x - 0.34f, 0.035f, z - 1.12f),
                new Vector3(0.12f, 0.04f, 1.48f),
                overheadMaterial);
            CreateObstaclePart(
                root,
                "Overhead Right Approach Rail",
                new Vector3(x + 0.34f, 0.035f, z - 1.12f),
                new Vector3(0.12f, 0.04f, 1.48f),
                overheadMaterial);
        }

        obstacles.Add(new Obstacle
        {
            Body = root,
            Kind = kind,
            X = x,
            Z = z
        });
    }

    private void CreateObstaclePart(
        GameObject root,
        string partName,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject part = CreateCube(partName, position, scale, material);
        part.transform.SetParent(root.transform);
    }

    private void CheckObstacleHits()
    {
        Vector3 playerPosition = player.transform.position;

        for (int index = 0; index < obstacles.Count; index++)
        {
            Obstacle obstacle = obstacles[index];
            if (obstacle.Passed)
            {
                continue;
            }

            float dz = Mathf.Abs(obstacle.Z - playerPosition.z);
            float dx = Mathf.Abs(obstacle.X - playerPosition.x);
            bool isSameLane = dx < ObstacleLaneTolerance;

            if (dz < ObstacleDepthTolerance && isSameLane)
            {
                bool collision = RunnerObstacleRules.CausesCollision(
                    obstacle.Kind,
                    runnerMotor.State,
                    runnerMotor.FeetHeight);

                if (collision)
                {
                    HandleHit();
                    return;
                }

                if (RunnerObstacleRules.IsActionClear(
                    obstacle.Kind,
                    runnerMotor.State,
                    runnerMotor.FeetHeight))
                {
                    obstacle.ActionClearObserved = true;
                }
            }

            if (playerPosition.z > obstacle.Z + ObstacleDepthTolerance)
            {
                obstacle.Passed = true;
                if (obstacle.Reward.TryGrant(
                    obstacle.ActionClearObserved,
                    obstacle.ActionClearObserved,
                    obstacle.Kind))
                {
                    RegisterActionClear();
                }
            }
        }
    }

    private void RegisterActionClear()
    {
        actionClearCount++;
        actionFeedbackPoints = combo.RegisterActionClear();
        actionFeedbackTimer = 0.72f;
        cameraRig.PulseFieldOfView(0.9f + combo.Multiplier * 0.12f);
        actionParticles.transform.position = player.transform.position + new Vector3(0f, 0.9f, 0f);
        actionParticles.Emit(12 + combo.Multiplier * 3);
        soundEffects.PlayClear(combo.Multiplier);
        music.TriggerDuck();
    }

    private void HandleHit()
    {
        remainingLives--;
        combo.ClearActiveCombo();
        actionHintTimer = 0f;
        actionFeedbackTimer = 0f;
        playerMaterial.color = CrashColor;
        playerVisualRoot.transform.localRotation = Quaternion.Euler(72f, 0f, 18f);
        cameraRig.TriggerImpact(0.24f, 2f);
        soundEffects.PlayCrash();
        music.TriggerDuck();

        if (remainingLives <= 0)
        {
            FailLevel();
            return;
        }

        state = GameState.Recovering;
        runnerMotor.FreezeVisuals();
        music.SetState(RunnerMusicState.GameOver);
    }

    private void ContinueAfterHit()
    {
        if (state != GameState.Recovering)
        {
            return;
        }

        state = GameState.Playing;
        RestoreCheckpoint(checkpointIndex, true);
        UpdateMusicIntensity();
    }

    private void CompleteLevel()
    {
        RecordBestRun();
        bool completedCampaign = activeLevelIndex >= RunnerLevelCatalog.Levels.Count - 1;
        state = completedCampaign ? GameState.CampaignComplete : GameState.LevelComplete;
        music.SetState(RunnerMusicState.Menu);
    }

    private void FailLevel()
    {
        state = GameState.GameOver;
        RecordBestRun();

        playerMaterial.color = CrashColor;
        playerVisualRoot.transform.localRotation = Quaternion.Euler(72f, 0f, 18f);
        cameraRig.TriggerImpact(0.24f, 2f);
        soundEffects.PlayCrash();
        music.SetState(RunnerMusicState.GameOver);
        music.TriggerDuck();
    }

    private void RecordBestRun()
    {
        highScore = Mathf.Max(highScore, furthestDistance);
        bestScore = Mathf.Max(bestScore, CurrentScore);
        bestCombo = Mathf.Max(bestCombo, combo.HighestCombo);
        PlayerPrefs.SetFloat(BestDistanceKey, highScore);
        PlayerPrefs.SetInt(BestScoreKey, bestScore);
        PlayerPrefs.SetInt(BestComboKey, bestCombo);
        PlayerPrefs.Save();

    }

    private void PauseRun()
    {
        if (state != GameState.Playing)
        {
            return;
        }

        state = GameState.Paused;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void ResumeRun()
    {
        if (state != GameState.Paused)
        {
            return;
        }

        RestoreGlobalPauseState();
        state = GameState.Playing;
    }

    private bool HandleBackPressed()
    {
        if (state == GameState.Playing)
        {
            PauseRun();
            return true;
        }

        if (state == GameState.Paused)
        {
            ResumeRun();
            return true;
        }

        RequestExit();
        return true;
    }

    private void RequestExit()
    {
        ExitRequested = true;
        RestoreGlobalPauseState();
        if (!Application.isEditor)
        {
            Application.Quit();
        }
    }

    private void OnDisable()
    {
        RestoreGlobalPauseState();
    }

    private void RestoreGlobalPauseState()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void UpdateGameplayHints()
    {
        if (laneHintTimer > 0f || actionHintTimer > 0f)
        {
            return;
        }

        RunnerObstacleKind? nearestActionKind = null;
        float nearestDistance = float.PositiveInfinity;
        float playerZ = player.transform.position.z;

        for (int index = 0; index < obstacles.Count; index++)
        {
            Obstacle obstacle = obstacles[index];
            if (obstacle.Passed || obstacle.Kind == RunnerObstacleKind.Blocker)
            {
                continue;
            }

            float ahead = obstacle.Z - playerZ;
            if (ahead >= 5f && ahead <= 18f && ahead < nearestDistance)
            {
                nearestDistance = ahead;
                nearestActionKind = obstacle.Kind;
            }
        }

        if (nearestActionKind == RunnerObstacleKind.Hurdle && !jumpHintShown)
        {
            jumpHintShown = true;
            actionHintSymbol = "\u2191";
            actionHintTimer = Application.isMobilePlatform ? 2.8f : 2f;
        }
        else if (nearestActionKind == RunnerObstacleKind.Overhead && !slideHintShown)
        {
            slideHintShown = true;
            actionHintSymbol = "\u2193";
            actionHintTimer = Application.isMobilePlatform ? 2.8f : 2f;
        }
    }

    private void UpdateFeedbackTimers()
    {
        laneHintTimer = Mathf.Max(0f, laneHintTimer - Time.deltaTime);
        actionHintTimer = Mathf.Max(0f, actionHintTimer - Time.deltaTime);
        actionFeedbackTimer = Mathf.Max(0f, actionFeedbackTimer - Time.deltaTime);
        combo.Tick(Time.deltaTime);
    }

    private void CleanupWorldBehind()
    {
        float cutoff = player.transform.position.z - CleanupDistance;
        RemoveBehind(roadSegments, cutoff);
        RemoveBehind(cityBlocks, cutoff);

        for (int index = obstacles.Count - 1; index >= 0; index--)
        {
            if (obstacles[index].Z < cutoff)
            {
                ReleaseObstacleRoot(obstacles[index].Body);
                obstacles.RemoveAt(index);
            }
        }
    }

    private void RemoveBehind(List<GameObject> objects, float cutoff)
    {
        for (int index = objects.Count - 1; index >= 0; index--)
        {
            if (objects[index] == null || objects[index].transform.position.z < cutoff)
            {
                if (objects[index] != null)
                {
                    ReleaseCube(objects[index]);
                }

                objects.RemoveAt(index);
            }
        }
    }

    private void ClearWorld()
    {
        ReleaseObjects(roadSegments);
        ReleaseObjects(cityBlocks);

        for (int index = obstacles.Count - 1; index >= 0; index--)
        {
            ReleaseObstacleRoot(obstacles[index].Body);
        }

        obstacles.Clear();
    }

    private void ReleaseObjects(List<GameObject> objects)
    {
        for (int index = objects.Count - 1; index >= 0; index--)
        {
            if (objects[index] != null)
            {
                ReleaseCube(objects[index]);
            }
        }

        objects.Clear();
    }

    private void UpdateCamera(bool snap = false)
    {
        Vector3 playerPosition = player.transform.position;
        bool moving = state == GameState.Playing || state == GameState.Celebrating;
        float speedAmount = moving
            ? Mathf.InverseLerp(StartingSpeed, RunnerPatternCatalog.MaximumRunnerSpeed, currentSpeed)
            : 0f;
        float laneTargetX = runnerMotor == null ? playerPosition.x : LaneX(runnerMotor.Lane);
        cameraRig.Tick(
            playerPosition,
            laneTargetX,
            speedAmount,
            moving,
            snap);
    }

    private GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        return worldPool.AcquireCube(objectName, position, scale, material);
    }

    private GameObject CreateTrackedCube(
        List<GameObject> collection,
        string objectName,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject cube = CreateCube(objectName, position, scale, material);
        cube.transform.SetParent(worldRoot.transform);
        collection.Add(cube);
        return cube;
    }

    private GameObject AcquireObstacleRoot(string objectName, Vector3 position)
    {
        return worldPool.AcquireObstacleRoot(objectName, position);
    }

    private void ReleaseObstacleRoot(GameObject root)
    {
        worldPool.ReleaseObstacleRoot(root);
    }

    private void ReleaseCube(GameObject cube)
    {
        worldPool.ReleaseCube(cube);
    }

    private int CountActiveWorldCubes()
    {
        int count = roadSegments.Count + cityBlocks.Count;
        for (int index = 0; index < obstacles.Count; index++)
        {
            if (obstacles[index].Body != null)
            {
                count += obstacles[index].Body.transform.childCount;
            }
        }

        return count;
    }

    private void SetMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private float LaneX(int lane)
    {
        return (lane - 1) * LaneWidth;
    }

    private float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
