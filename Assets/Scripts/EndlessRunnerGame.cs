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
        public float X;
        public float Z;
    }

    private const float LaneWidth = 2.2f;
    private const float SegmentLength = 14f;
    private const float LookAheadDistance = 90f;
    private const float CleanupDistance = 24f;

    private readonly List<GameObject> roadSegments = new List<GameObject>();
    private readonly List<GameObject> cityBlocks = new List<GameObject>();
    private readonly List<Obstacle> obstacles = new List<Obstacle>();

    private readonly Vector3 cameraOffset = new Vector3(0f, 7.2f, -9.5f);

    private GameState state;
    private GameObject worldRoot;
    private GameObject player;
    private GameObject playerBody;
    private Camera gameCamera;
    private Light sun;

    private Material roadMaterial;
    private Material laneMaterial;
    private Material edgeMaterial;
    private Material playerMaterial;
    private Material playerAccentMaterial;
    private Material obstacleMaterial;
    private Material buildingMaterialA;
    private Material buildingMaterialB;
    private Material roofMaterial;

    private int targetLane = 1;
    private int nextSegmentIndex;
    private float nextObstacleZ;
    private float distance;
    private float highScore;
    private float currentSpeed;

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
        highScore = PlayerPrefs.GetFloat("EndlessRunner.HighScore", 0f);
        CreateMaterials();
        ConfigureScene();
        CreatePlayer();
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

            AnimateIdlePlayer();
            UpdateCamera();
            return;
        }

        HandleLaneInput();
        AdvanceRunner();
        GenerateWorldAhead();
        CleanupWorldBehind();
        CheckObstacleHits();
        UpdateCamera();
    }

    private void OnGUI()
    {
        GUIStyle label = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            normal = { textColor = Color.white }
        };

        GUIStyle small = new GUIStyle(label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        GUI.Label(new Rect(22f, 18f, 300f, 42f), "Distance  " + Mathf.FloorToInt(distance) + " m", label);
        GUI.Label(new Rect(22f, 54f, 300f, 32f), "Best  " + Mathf.FloorToInt(highScore) + " m", new GUIStyle(label) { fontSize = 16 });

        if (state == GameState.Playing)
        {
            return;
        }

        float panelWidth = Mathf.Min(520f, Screen.width - 44f);
        float panelHeight = state == GameState.StartScreen ? 270f : 250f;
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.04f, 0.06f, 0.08f, 0.78f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle title = new GUIStyle(label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 24f, panel.width - 48f, panel.height - 48f));
        GUILayout.Label(state == GameState.StartScreen ? "ROOFTOP RUNNER" : "RUN ENDED", title, GUILayout.Height(48f));
        GUILayout.Space(12f);

        if (state == GameState.StartScreen)
        {
            GUILayout.Label("Switch lanes, dodge rooftop barriers, and stay alive as long as you can.", small, GUILayout.Height(48f));
            GUILayout.Space(8f);
            GUILayout.Label("A / Left Arrow: move left    D / Right Arrow: move right", small, GUILayout.Height(30f));
            GUILayout.Space(18f);

            if (GUILayout.Button("Start  (Space)", GUILayout.Height(44f)))
            {
                ResetRun(GameState.Playing);
            }
        }
        else
        {
            GUILayout.Label("Distance: " + Mathf.FloorToInt(distance) + " m", title, GUILayout.Height(46f));
            GUILayout.Space(8f);
            GUILayout.Label("Press R or Space to restart", small, GUILayout.Height(30f));
            GUILayout.Space(18f);

            if (GUILayout.Button("Restart", GUILayout.Height(44f)))
            {
                ResetRun(GameState.Playing);
            }
        }

        GUILayout.EndArea();
    }

    private void CreateMaterials()
    {
        roadMaterial = CreateMaterial("Road", new Color(0.18f, 0.2f, 0.22f));
        laneMaterial = CreateMaterial("Lane Markers", new Color(0.96f, 0.82f, 0.25f));
        edgeMaterial = CreateMaterial("Roof Edge", new Color(0.12f, 0.13f, 0.15f));
        playerMaterial = CreateMaterial("Player", new Color(0.05f, 0.72f, 0.8f));
        playerAccentMaterial = CreateMaterial("Player Accent", new Color(1f, 0.93f, 0.68f));
        obstacleMaterial = CreateMaterial("Obstacle", new Color(0.92f, 0.18f, 0.16f));
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

        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };

        return material;
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

        playerBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerBody.name = "Runner Body";
        playerBody.transform.SetParent(player.transform);
        playerBody.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        playerBody.transform.localScale = new Vector3(0.72f, 0.92f, 0.72f);
        SetMaterial(playerBody, playerMaterial);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Runner Visor";
        head.transform.SetParent(player.transform);
        head.transform.localPosition = new Vector3(0f, 1.56f, 0.26f);
        head.transform.localScale = new Vector3(0.56f, 0.18f, 0.18f);
        SetMaterial(head, playerAccentMaterial);

        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shadow.name = "Runner Shadow";
        shadow.transform.SetParent(player.transform);
        shadow.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        shadow.transform.localScale = new Vector3(0.82f, 0.025f, 0.82f);
        SetMaterial(shadow, edgeMaterial);
    }

    private void ResetRun(GameState nextState)
    {
        state = nextState;
        targetLane = 1;
        distance = 0f;
        currentSpeed = 9.4f;
        nextSegmentIndex = -1;
        nextObstacleZ = 23f;

        player.transform.position = new Vector3(LaneX(targetLane), 0f, 0f);
        player.transform.rotation = Quaternion.identity;
        playerBody.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        playerBody.transform.localRotation = Quaternion.identity;

        ClearWorld();
        GenerateWorldAhead();
        UpdateCamera();
    }

    private void HandleLaneInput()
    {
        bool moveLeft = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool moveRight = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);

        if (moveLeft)
        {
            targetLane = Mathf.Max(0, targetLane - 1);
        }
        else if (moveRight)
        {
            targetLane = Mathf.Min(2, targetLane + 1);
        }
    }

    private void AdvanceRunner()
    {
        currentSpeed = Mathf.Min(16.5f, currentSpeed + Time.deltaTime * 0.16f);

        Vector3 position = player.transform.position;
        position.z += currentSpeed * Time.deltaTime;
        position.x = Mathf.MoveTowards(position.x, LaneX(targetLane), 13.5f * Time.deltaTime);
        player.transform.position = position;

        float tilt = Mathf.Clamp((LaneX(targetLane) - position.x) * 8f, -12f, 12f);
        player.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        playerBody.transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 14f) * 3f, 0f, 0f);

        distance = Mathf.Max(distance, position.z);
    }

    private void AnimateIdlePlayer()
    {
        playerBody.transform.localPosition = new Vector3(0f, 0.8f + Mathf.Sin(Time.time * 3f) * 0.03f, 0f);
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

        while (state == GameState.Playing && nextObstacleZ < playerZ + LookAheadDistance)
        {
            CreateObstacleSet(nextObstacleZ);
            float progress = Mathf.Clamp01(distance / 700f);
            float interval = Mathf.Lerp(8.6f, 4.5f, progress);
            nextObstacleZ += Random.Range(interval * 0.78f, interval * 1.22f);
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
            for (int i = 0; i < 3; i++)
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

    private void CreateObstacleSet(float z)
    {
        int firstLane = Random.Range(0, 3);
        CreateObstacle(firstLane, z);

        float progress = Mathf.Clamp01(distance / 450f);
        if (Random.value < Mathf.Lerp(0.04f, 0.32f, progress))
        {
            int secondLane = Random.Range(0, 3);
            if (secondLane == firstLane)
            {
                secondLane = (secondLane + 1 + Random.Range(0, 2)) % 3;
            }

            CreateObstacle(secondLane, z + Random.Range(-0.35f, 0.35f));
        }
    }

    private void CreateObstacle(int lane, float z)
    {
        float x = LaneX(lane);
        GameObject body = CreateCube(
            "Barrier",
            new Vector3(x, 0.55f, z),
            new Vector3(1.25f, 1.1f, 1.05f),
            obstacleMaterial);
        body.transform.SetParent(worldRoot.transform);

        GameObject cap = CreateCube(
            "Barrier Cap",
            new Vector3(x, 1.18f, z),
            new Vector3(1.45f, 0.16f, 1.2f),
            edgeMaterial);
        cap.transform.SetParent(body.transform);

        obstacles.Add(new Obstacle
        {
            Body = body,
            X = x,
            Z = z
        });
    }

    private void CheckObstacleHits()
    {
        Vector3 playerPosition = player.transform.position;

        for (int i = 0; i < obstacles.Count; i++)
        {
            Obstacle obstacle = obstacles[i];
            float dz = Mathf.Abs(obstacle.Z - playerPosition.z);
            float dx = Mathf.Abs(obstacle.X - playerPosition.x);

            if (dz < 0.88f && dx < 0.88f)
            {
                EndRun();
                return;
            }
        }
    }

    private void EndRun()
    {
        state = GameState.GameOver;
        highScore = Mathf.Max(highScore, distance);
        PlayerPrefs.SetFloat("EndlessRunner.HighScore", highScore);
        PlayerPrefs.Save();
    }

    private void CleanupWorldBehind()
    {
        float cutoff = player.transform.position.z - CleanupDistance;
        RemoveBehind(roadSegments, cutoff);
        RemoveBehind(cityBlocks, cutoff);

        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            if (obstacles[i].Z < cutoff)
            {
                Destroy(obstacles[i].Body);
                obstacles.RemoveAt(i);
            }
        }
    }

    private void RemoveBehind(List<GameObject> objects, float cutoff)
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (objects[i] == null || objects[i].transform.position.z < cutoff)
            {
                Destroy(objects[i]);
                objects.RemoveAt(i);
            }
        }
    }

    private void ClearWorld()
    {
        DestroyObjects(roadSegments);
        DestroyObjects(cityBlocks);

        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            Destroy(obstacles[i].Body);
        }

        obstacles.Clear();
    }

    private void DestroyObjects(List<GameObject> objects)
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            Destroy(objects[i]);
        }

        objects.Clear();
    }

    private void UpdateCamera()
    {
        Vector3 playerPosition = player.transform.position;
        Vector3 desiredPosition = playerPosition + cameraOffset;
        gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, desiredPosition, Time.deltaTime * 8f);
        gameCamera.transform.LookAt(playerPosition + new Vector3(0f, 1.1f, 8.5f));
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
