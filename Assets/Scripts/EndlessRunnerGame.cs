using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EndlessRunnerGame : MonoBehaviour
{
    private enum GameState
    {
        StartScreen,
        Playing,
        GameOver
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

    private const float LaneWidth = 2.2f;
    private const float SegmentLength = 14f;
    private const float LookAheadDistance = 90f;
    private const float CleanupDistance = 24f;
    private const float StartingSpeed = 9.4f;
    private const float ObstacleDepthTolerance = 0.72f;
    private const float ObstacleLaneTolerance = 0.82f;
    private const string BestDistanceKey = "EndlessRunner.HighScore";
    private const string BestScoreKey = "EndlessRunner.BestScore";

    private static readonly Color RunnerColor = new Color(0.05f, 0.72f, 0.8f);
    private static readonly Color CrashColor = new Color(1f, 0.24f, 0.12f);

    private readonly List<GameObject> roadSegments = new List<GameObject>();
    private readonly List<GameObject> cityBlocks = new List<GameObject>();
    private readonly List<Obstacle> obstacles = new List<Obstacle>();
    private readonly Vector3 cameraOffset = new Vector3(0f, 7.2f, -9.5f);

    private GameState state;
    private GameObject worldRoot;
    private GameObject player;
    private GameObject playerVisualRoot;
    private GameObject playerBody;
    private GameObject playerShadow;
    private RunnerMotor runnerMotor;
    private Camera gameCamera;
    private Light sun;
    private ParticleSystem actionParticles;
    private ProceduralRunnerSfx soundEffects;
    private RunnerPatternSequence patternSequence;

    private Material roadMaterial;
    private Material laneMaterial;
    private Material edgeMaterial;
    private Material playerMaterial;
    private Material playerAccentMaterial;
    private Material blockerMaterial;
    private Material hurdleMaterial;
    private Material overheadMaterial;
    private Material buildingMaterialA;
    private Material buildingMaterialB;
    private Material roofMaterial;

    private int nextSegmentIndex;
    private int actionClearCount;
    private int bestScore;
    private int? nextRunSeed;
    private float nextPatternZ;
    private float distance;
    private float highScore;
    private float currentSpeed;
    private float laneHintTimer;
    private float actionHintTimer;
    private float actionFeedbackTimer;
    private string actionHintSymbol;
    private bool jumpHintShown;
    private bool slideHintShown;
    private bool tutorialGenerated;

    public RunnerMotor Motor => runnerMotor;
    public int CurrentScore => RunnerScore.Calculate(distance, actionClearCount);
    public int ActionClearCount => actionClearCount;
    public float Distance => distance;

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
        CreateMaterials();
        ConfigureScene();
        ProceduralRunnerMusic.AttachTo(gameObject);
        soundEffects = ProceduralRunnerSfx.AttachTo(gameObject);
        CreatePlayer();
        CreateActionParticles();
        ResetRun(GameState.StartScreen);
    }

    private void Update()
    {
        if (state == GameState.StartScreen)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                ResetRun(GameState.Playing);
            }

            AnimateIdlePlayer();
            UpdateCamera();
            return;
        }

        if (state == GameState.GameOver)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.R))
            {
                ResetRun(GameState.Playing);
            }

            UpdateCamera();
            return;
        }

        AdvanceRunner();
        GenerateWorldAhead();
        CleanupWorldBehind();
        CheckObstacleHits();
        UpdateGameplayHints();
        UpdateFeedbackTimers();
        UpdateCamera();
    }

    private void OnGUI()
    {
        GUIStyle label = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            normal = { textColor = Color.white }
        };

        GUIStyle compact = new GUIStyle(label)
        {
            fontSize = 16
        };

        GUIStyle small = new GUIStyle(compact)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        GUI.Label(new Rect(22f, 18f, 320f, 38f), "Score  " + CurrentScore, label);
        GUI.Label(new Rect(22f, 54f, 320f, 28f), "Distance  " + Mathf.FloorToInt(distance) + " m", compact);
        GUI.Label(new Rect(22f, 80f, 320f, 28f), "Best  " + bestScore, compact);

        if (state == GameState.Playing)
        {
            DrawGameplayFeedback(label);
            return;
        }

        float panelWidth = Mathf.Min(520f, Screen.width - 44f);
        float panelHeight = state == GameState.StartScreen ? 248f : 272f;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.82f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle title = new GUIStyle(label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 22f, panel.width - 48f, panel.height - 44f));
        GUILayout.Label(state == GameState.StartScreen ? "ROOFTOP RUNNER" : "RUN ENDED", title, GUILayout.Height(48f));
        GUILayout.Space(10f);

        if (state == GameState.StartScreen)
        {
            GUILayout.Label("Dodge. Jump. Slide. Keep your flow.", small, GUILayout.Height(42f));
            GUILayout.Space(22f);

            if (GUILayout.Button("Start Run", GUILayout.Height(46f)))
            {
                ResetRun(GameState.Playing);
            }
        }
        else
        {
            GUILayout.Label("Score  " + CurrentScore, title, GUILayout.Height(46f));
            GUILayout.Label("Distance  " + Mathf.FloorToInt(distance) + " m", small, GUILayout.Height(28f));
            GUILayout.Space(14f);

            if (GUILayout.Button("Restart", GUILayout.Height(46f)))
            {
                ResetRun(GameState.Playing);
            }
        }

        GUILayout.EndArea();
    }

    public void StartRunForTests(int seed)
    {
        nextRunSeed = seed;
        ResetRun(GameState.Playing);
    }

    private void DrawGameplayFeedback(GUIStyle label)
    {
        if (laneHintTimer > 0f)
        {
            DrawCenteredSymbol("\u2190    \u2192", label);
        }
        else if (actionHintTimer > 0f && !string.IsNullOrEmpty(actionHintSymbol))
        {
            DrawCenteredSymbol(actionHintSymbol, label);
        }

        if (actionFeedbackTimer > 0f)
        {
            GUIStyle feedback = new GUIStyle(label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.86f, 0.24f, Mathf.Clamp01(actionFeedbackTimer * 2f)) }
            };

            GUI.Label(new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.28f, 180f, 46f), "+100", feedback);
        }
    }

    private void DrawCenteredSymbol(string symbol, GUIStyle baseStyle)
    {
        GUIStyle hint = new GUIStyle(baseStyle)
        {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.92f, 0.42f, 0.94f) }
        };

        GUI.Label(new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.62f, 200f, 60f), symbol, hint);
    }

    private void CreateMaterials()
    {
        roadMaterial = CreateMaterial("Road", new Color(0.18f, 0.2f, 0.22f));
        laneMaterial = CreateMaterial("Lane Markers", new Color(0.96f, 0.82f, 0.25f));
        edgeMaterial = CreateMaterial("Roof Edge", new Color(0.12f, 0.13f, 0.15f));
        playerMaterial = CreateMaterial("Player", RunnerColor);
        playerAccentMaterial = CreateMaterial("Player Accent", new Color(1f, 0.93f, 0.68f));
        blockerMaterial = CreateMaterial("Blocker", new Color(0.92f, 0.18f, 0.16f));
        hurdleMaterial = CreateMaterial("Hurdle", new Color(1f, 0.58f, 0.08f));
        overheadMaterial = CreateMaterial("Overhead", new Color(0.58f, 0.22f, 0.72f));
        buildingMaterialA = CreateMaterial("Building A", new Color(0.32f, 0.37f, 0.44f));
        buildingMaterialB = CreateMaterial("Building B", new Color(0.22f, 0.46f, 0.52f));
        roofMaterial = CreateMaterial("Roof Tops", new Color(0.11f, 0.16f, 0.18f));
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }

        return new Material(shader)
        {
            name = materialName,
            color = color
        };
    }

    private void ConfigureScene()
    {
        worldRoot = new GameObject("Generated Runner World");

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
        gameCamera.backgroundColor = new Color(0.47f, 0.72f, 0.9f);

        RenderSettings.ambientLight = new Color(0.58f, 0.64f, 0.72f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.47f, 0.72f, 0.9f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 170f;

        sun = FindObjectOfType<Light>();
        if (sun == null)
        {
            sun = new GameObject("Directional Light").AddComponent<Light>();
        }

        sun.type = LightType.Directional;
        sun.intensity = 1.25f;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 12f);
    }

    private void CreatePlayer()
    {
        player = new GameObject("Runner");
        playerVisualRoot = new GameObject("Runner Visual");
        playerVisualRoot.transform.SetParent(player.transform);

        playerBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerBody.name = "Runner Body";
        playerBody.transform.SetParent(playerVisualRoot.transform);
        playerBody.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        playerBody.transform.localScale = new Vector3(0.72f, 0.92f, 0.72f);
        SetMaterial(playerBody, playerMaterial);

        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Runner Visor";
        visor.transform.SetParent(playerVisualRoot.transform);
        visor.transform.localPosition = new Vector3(0f, 1.56f, 0.26f);
        visor.transform.localScale = new Vector3(0.56f, 0.18f, 0.18f);
        SetMaterial(visor, playerAccentMaterial);

        playerShadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        playerShadow.name = "Runner Shadow";
        playerShadow.transform.SetParent(player.transform);
        playerShadow.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        playerShadow.transform.localScale = new Vector3(0.82f, 0.025f, 0.82f);
        SetMaterial(playerShadow, edgeMaterial);

        runnerMotor = player.AddComponent<RunnerMotor>();
        runnerMotor.Configure(
            playerVisualRoot.transform,
            playerBody.transform,
            playerShadow.transform,
            LaneWidth);
    }

    private void CreateActionParticles()
    {
        GameObject particleObject = new GameObject("Action Clear Particles");
        actionParticles = particleObject.AddComponent<ParticleSystem>();

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
        state = nextState;
        distance = 0f;
        currentSpeed = StartingSpeed;
        nextSegmentIndex = -1;
        nextPatternZ = 96f;
        actionClearCount = 0;
        laneHintTimer = nextState == GameState.Playing ? 2.4f : 0f;
        actionHintTimer = 0f;
        actionFeedbackTimer = 0f;
        actionHintSymbol = null;
        jumpHintShown = false;
        slideHintShown = false;
        tutorialGenerated = false;

        int seed = nextRunSeed ?? unchecked(Environment.TickCount ^ DateTime.UtcNow.Millisecond * 397);
        nextRunSeed = null;
        patternSequence = new RunnerPatternSequence(seed);

        runnerMotor.ResetForRun();
        playerMaterial.color = RunnerColor;
        actionParticles.Clear();

        ClearWorld();
        GenerateWorldAhead();
        UpdateCamera(true);
    }

    private void AdvanceRunner()
    {
        currentSpeed = Mathf.Min(
            RunnerPatternCatalog.MaximumRunnerSpeed,
            currentSpeed + Time.deltaTime * 0.16f);

        runnerMotor.Tick(currentSpeed);

        if (runnerMotor.JumpStartedThisFrame)
        {
            soundEffects.PlayJump();
            if (actionHintSymbol == "\u2191")
            {
                actionHintTimer = 0f;
            }
        }

        if (runnerMotor.SlideStartedThisFrame)
        {
            soundEffects.PlaySlide();
            if (actionHintSymbol == "\u2193")
            {
                actionHintTimer = 0f;
            }
        }

        distance = Mathf.Max(distance, player.transform.position.z);
    }

    private void AnimateIdlePlayer()
    {
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
            CreateObstacleMask(RunnerObstacleKind.Blocker, 1 << 1, 28f);
            CreateObstacleMask(RunnerObstacleKind.Hurdle, 0b111, 48f);
            CreateObstacleMask(RunnerObstacleKind.Overhead, 0b111, 70f);
            tutorialGenerated = true;
        }

        while (nextPatternZ < playerZ + LookAheadDistance)
        {
            int tier = distance < 150f ? 0 : distance < 400f ? 1 : 2;
            RunnerPatternDefinition pattern = patternSequence.Next(tier);
            CreatePattern(pattern, nextPatternZ);
            nextPatternZ += pattern.Length + patternSequence.NextSpacing(currentSpeed);
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

        GameObject slab = CreateCube(
            "Road Segment " + segmentIndex,
            new Vector3(0f, -0.18f, centerZ),
            new Vector3(7.4f, 0.36f, SegmentLength),
            roadMaterial);
        slab.transform.SetParent(worldRoot.transform);
        roadSegments.Add(slab);

        GameObject leftEdge = CreateCube(
            "Left Roof Edge",
            new Vector3(-4.05f, 0.24f, centerZ),
            new Vector3(0.26f, 0.84f, SegmentLength),
            edgeMaterial);
        leftEdge.transform.SetParent(worldRoot.transform);
        roadSegments.Add(leftEdge);

        GameObject rightEdge = CreateCube(
            "Right Roof Edge",
            new Vector3(4.05f, 0.24f, centerZ),
            new Vector3(0.26f, 0.84f, SegmentLength),
            edgeMaterial);
        rightEdge.transform.SetParent(worldRoot.transform);
        roadSegments.Add(rightEdge);

        for (int laneMarker = 0; laneMarker < 2; laneMarker++)
        {
            float x = laneMarker == 0 ? -LaneWidth * 0.5f : LaneWidth * 0.5f;
            for (int dash = 0; dash < 4; dash++)
            {
                GameObject marker = CreateCube(
                    "Lane Dash",
                    new Vector3(x, 0.03f, segmentIndex * SegmentLength + 1.7f + dash * 3.4f),
                    new Vector3(0.08f, 0.05f, 1.42f),
                    laneMaterial);
                marker.transform.SetParent(worldRoot.transform);
                roadSegments.Add(marker);
            }
        }
    }

    private void CreateCitySlice(int segmentIndex)
    {
        System.Random random = new System.Random(segmentIndex * 92821 + 37);
        float centerZ = segmentIndex * SegmentLength + SegmentLength * 0.5f;

        for (int side = -1; side <= 1; side += 2)
        {
            for (int index = 0; index < 3; index++)
            {
                float width = RandomRange(random, 2.1f, 4.7f);
                float depth = RandomRange(random, 3.4f, 7.6f);
                float height = RandomRange(random, 2.2f, 9.2f);
                float x = side * RandomRange(random, 8.5f, 18.5f);
                float z = centerZ + RandomRange(random, -SegmentLength * 0.45f, SegmentLength * 0.45f);
                float y = -height * 0.5f - 0.65f;

                Material material = random.NextDouble() > 0.5 ? buildingMaterialA : buildingMaterialB;
                GameObject building = CreateCube(
                    "Background Building",
                    new Vector3(x, y, z),
                    new Vector3(width, height, depth),
                    material);
                building.transform.SetParent(worldRoot.transform);
                cityBlocks.Add(building);

                GameObject roof = CreateCube(
                    "Background Roof",
                    new Vector3(x, y + height * 0.5f + 0.04f, z),
                    new Vector3(width * 1.04f, 0.08f, depth * 1.04f),
                    roofMaterial);
                roof.transform.SetParent(worldRoot.transform);
                cityBlocks.Add(roof);
            }
        }
    }

    private void CreateObstacle(RunnerObstacleKind kind, int lane, float z)
    {
        float x = LaneX(lane);
        GameObject root = new GameObject(kind + " Obstacle");
        root.transform.position = new Vector3(x, 0f, z);
        root.transform.SetParent(worldRoot.transform);

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
                    EndRun();
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
        actionFeedbackTimer = 0.72f;
        actionParticles.transform.position = player.transform.position + new Vector3(0f, 0.9f, 0f);
        actionParticles.Emit(14);
        soundEffects.PlayClear();
    }

    private void EndRun()
    {
        state = GameState.GameOver;
        highScore = Mathf.Max(highScore, distance);
        bestScore = Mathf.Max(bestScore, CurrentScore);
        PlayerPrefs.SetFloat(BestDistanceKey, highScore);
        PlayerPrefs.SetInt(BestScoreKey, bestScore);
        PlayerPrefs.Save();

        playerMaterial.color = CrashColor;
        playerVisualRoot.transform.localRotation = Quaternion.Euler(72f, 0f, 18f);
        soundEffects.PlayCrash();
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
            actionHintTimer = 2f;
        }
        else if (nearestActionKind == RunnerObstacleKind.Overhead && !slideHintShown)
        {
            slideHintShown = true;
            actionHintSymbol = "\u2193";
            actionHintTimer = 2f;
        }
    }

    private void UpdateFeedbackTimers()
    {
        laneHintTimer = Mathf.Max(0f, laneHintTimer - Time.deltaTime);
        actionHintTimer = Mathf.Max(0f, actionHintTimer - Time.deltaTime);
        actionFeedbackTimer = Mathf.Max(0f, actionFeedbackTimer - Time.deltaTime);
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
                Destroy(obstacles[index].Body);
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
                Destroy(objects[index]);
                objects.RemoveAt(index);
            }
        }
    }

    private void ClearWorld()
    {
        DestroyObjects(roadSegments);
        DestroyObjects(cityBlocks);

        for (int index = obstacles.Count - 1; index >= 0; index--)
        {
            Destroy(obstacles[index].Body);
        }

        obstacles.Clear();
    }

    private void DestroyObjects(List<GameObject> objects)
    {
        for (int index = objects.Count - 1; index >= 0; index--)
        {
            Destroy(objects[index]);
        }

        objects.Clear();
    }

    private void UpdateCamera(bool snap = false)
    {
        Vector3 playerPosition = player.transform.position;
        Vector3 desiredPosition = playerPosition + cameraOffset;
        float positionBlend = snap ? 1f : 1f - Mathf.Exp(-8f * Time.deltaTime);
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, desiredPosition, positionBlend);
        gameCamera.transform.LookAt(playerPosition + new Vector3(0f, 1.1f, 8.5f));

        float speedAmount = state == GameState.Playing
            ? Mathf.InverseLerp(StartingSpeed, RunnerPatternCatalog.MaximumRunnerSpeed, currentSpeed)
            : 0f;
        float targetFieldOfView = Mathf.Lerp(58f, 64f, speedAmount);
        float fieldOfViewBlend = snap ? 1f : 1f - Mathf.Exp(-4f * Time.deltaTime);
        gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView, targetFieldOfView, fieldOfViewBlend);
    }

    private GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        SetMaterial(cube, material);
        return cube;
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
