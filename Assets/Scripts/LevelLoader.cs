using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelLoader : MonoBehaviour
{
    private const float WallThickness = 0.35f;
    private readonly List<GameObject> spawned = new List<GameObject>();

    private PhysicsManager physicsManager;
    private BallController ball;
    private Material grassMaterial;
    private Material iceMaterial;
    private Material sandMaterial;
    private Material slopeMaterial;
    private Material airMaterial;
    private Material wallMaterial;
    private Material rubberMaterial;
    private Material foamMaterial;
    private Material goalMaterial;
    private Material ballMaterial;
    private int currentLevel;
    private Text levelText;

    private void Awake()
    {
        physicsManager = FindObjectOfType<PhysicsManager>();
        if (physicsManager == null)
        {
            physicsManager = new GameObject("Physics Manager").AddComponent<PhysicsManager>();
        }

        CreateMaterials();
    }

    private void Start()
    {
        SetupCameraAndLight();
        CreateBallAndUi();
        LoadLevel(0);
    }

    public void LoadNextLevel()
    {
        LoadLevel((currentLevel + 1) % 3);
    }

    public void LoadLevel(int index)
    {
        currentLevel = Mathf.Clamp(index, 0, 2);
        ClearLevel();

        physicsManager.courseBounds = new Rect(-7f, -6f, 14f, 24f);
        CreateBoundary(physicsManager.courseBounds);

        Vector3 spawn = new Vector3(0f, physicsManager.ballRadius + 0.08f, -4.8f);
        if (currentLevel == 0)
        {
            BuildLevelOne();
            spawn = new Vector3(0f, physicsManager.ballRadius + 0.08f, -4.5f);
        }
        else if (currentLevel == 1)
        {
            BuildLevelTwo();
            spawn = new Vector3(-4.8f, physicsManager.ballRadius + 0.08f, -4.6f);
        }
        else
        {
            BuildLevelThree();
            spawn = new Vector3(-5.2f, physicsManager.ballRadius + 0.08f, -4.7f);
        }

        if (ball != null)
        {
            ball.ResetForLevel(spawn);
        }

        if (levelText != null)
        {
            levelText.text = GetLevelTitle(currentLevel);
        }
    }

    private void BuildLevelOne()
    {
        CreateSurface("Grass Fairway", SurfaceKind.Grass, new Vector3(0f, 0f, -1f), new Vector3(12f, 0.18f, 10f), grassMaterial, 0.40f, 0.45f);
        CreateSurface("Ice Patch", SurfaceKind.Ice, new Vector3(-3.3f, 0.03f, -1.2f), new Vector3(2.2f, 0.12f, 3.8f), iceMaterial, 0.10f, 0.50f);
        CreateSurface("Sand Patch", SurfaceKind.Sand, new Vector3(3.2f, 0.04f, -0.8f), new Vector3(2.4f, 0.12f, 4.2f), sandMaterial, 0.60f, 0.20f);
        CreateGoal(new Vector3(0f, 0.08f, 3.85f));

        CreateObstacle("Rubber Block", ObstacleKind.Rubber, new Vector3(-1.9f, 0.36f, 1.45f), new Vector3(1.1f, 0.72f, 0.55f), rubberMaterial, 0.80f, 0.18f, false, 0.5f);
        CreateObstacle("Foam Block", ObstacleKind.Foam, new Vector3(2.15f, 0.34f, 1.3f), new Vector3(1.0f, 0.68f, 0.7f), foamMaterial, 0.20f, 0.55f, false, 0.5f);
        CreateLabel("Level1 Label", "一级：草地/冰面/沙地\nμ=0.4 / 0.1 / 0.6", new Vector3(0f, 0.2f, -5.5f));
    }

    private void BuildLevelTwo()
    {
        CreateSurface("Lower Grass", SurfaceKind.Grass, new Vector3(-2.8f, 0f, -2.8f), new Vector3(6.4f, 0.18f, 5.2f), grassMaterial, 0.38f, 0.45f);
        GameObject slope = CreateSurface("Tilted Ramp", SurfaceKind.Slope, new Vector3(0.6f, 0.42f, 1.2f), new Vector3(5.8f, 0.18f, 6.2f), slopeMaterial, 0.30f, 0.42f);
        slope.transform.rotation = Quaternion.Euler(9f, 0f, 0f);
        CreateSurface("Upper Grass", SurfaceKind.Grass, new Vector3(2.5f, 0.9f, 5.1f), new Vector3(6.2f, 0.18f, 4.4f), grassMaterial, 0.35f, 0.45f);

        PhysicsSurface air = CreateSurface("Air Resistance Zone", SurfaceKind.AirField, new Vector3(0.3f, 0.98f, 4.7f), new Vector3(4.5f, 0.12f, 2.6f), airMaterial, 0.22f, 0.45f).GetComponent<PhysicsSurface>();
        air.isAirField = true;
        air.airDirection = new Vector3(-0.2f, 0f, -1f);
        air.airSpeed = 6.5f;
        air.dragCoefficient = 0.65f;

        CreateGoal(new Vector3(4.45f, 1.03f, 6.15f));
        CreateObstacle("Rubber Post", ObstacleKind.Rubber, new Vector3(-0.9f, 0.85f, 3.2f), new Vector3(0.7f, 1.2f, 0.7f), rubberMaterial, 0.82f, 0.15f, true, 0.45f);
        CreateObstacle("Foam Gate Left", ObstacleKind.Foam, new Vector3(2.1f, 1.22f, 4.25f), new Vector3(0.6f, 0.7f, 1.4f), foamMaterial, 0.20f, 0.55f, false, 0.5f);
        CreateObstacle("Foam Gate Right", ObstacleKind.Foam, new Vector3(4.0f, 1.22f, 4.25f), new Vector3(0.6f, 0.7f, 1.4f), foamMaterial, 0.20f, 0.55f, false, 0.5f);
        CreateLabel("Level2 Label", "二级：斜坡 + 空气阻力区\nF_parallel=mg sin θ，F_drag=1/2ρv²CdA", new Vector3(0f, 0.25f, -5.5f));
    }

    private void BuildLevelThree()
    {
        CreateSurface("Expert Grass A", SurfaceKind.Grass, new Vector3(-3.1f, 0f, -2.5f), new Vector3(6.2f, 0.18f, 5.4f), grassMaterial, 0.40f, 0.45f);
        CreateSurface("Expert Ice Lane", SurfaceKind.Ice, new Vector3(1.8f, 0.03f, -0.5f), new Vector3(3.0f, 0.12f, 6.6f), iceMaterial, 0.10f, 0.55f);
        GameObject slope = CreateSurface("Expert Tilt", SurfaceKind.Slope, new Vector3(2.5f, 0.36f, 3.1f), new Vector3(5.6f, 0.18f, 4.2f), slopeMaterial, 0.28f, 0.42f);
        slope.transform.rotation = Quaternion.Euler(0f, 0f, -8f);
        CreateSurface("Expert Sand Trap", SurfaceKind.Sand, new Vector3(-2.0f, 0.05f, 3.6f), new Vector3(3.2f, 0.12f, 3.8f), sandMaterial, 0.60f, 0.20f);

        PhysicsSurface air = CreateSurface("Expert Wind", SurfaceKind.AirField, new Vector3(0.4f, 0.54f, 5.0f), new Vector3(5.8f, 0.12f, 2.5f), airMaterial, 0.25f, 0.40f).GetComponent<PhysicsSurface>();
        air.isAirField = true;
        air.airDirection = new Vector3(1f, 0f, -0.15f);
        air.airSpeed = 5.8f;
        air.dragCoefficient = 0.62f;

        CreateGoal(new Vector3(4.9f, 0.58f, 6.25f));
        CreateObstacle("Rubber Bumper A", ObstacleKind.Rubber, new Vector3(-1.0f, 0.38f, -0.2f), new Vector3(1.1f, 0.76f, 0.7f), rubberMaterial, 0.80f, 0.15f, false, 0.5f);
        CreateObstacle("Rubber Bumper B", ObstacleKind.Rubber, new Vector3(3.2f, 0.7f, 2.4f), new Vector3(0.9f, 0.95f, 0.9f), rubberMaterial, 0.80f, 0.15f, true, 0.52f);
        CreateObstacle("Foam Stopper", ObstacleKind.Foam, new Vector3(-2.9f, 0.38f, 4.9f), new Vector3(1.3f, 0.75f, 0.7f), foamMaterial, 0.20f, 0.55f, false, 0.5f);
        CreateObstacle("Sand Barrier", ObstacleKind.SandTrap, new Vector3(0.2f, 0.26f, 2.8f), new Vector3(2.0f, 0.5f, 0.5f), sandMaterial, 0.18f, 0.65f, false, 0.5f);
        CreateLabel("Level3 Label", "三级：弹性/非弹性障碍 + 混合区域\n胜利：入洞速度 < 0.5 m/s，软性碰撞 ≤ 2", new Vector3(0f, 0.25f, -5.5f));
    }

    private GameObject CreateSurface(string name, SurfaceKind kind, Vector3 position, Vector3 scale, Material material, float friction, float restitution)
    {
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = name;
        surface.transform.position = position;
        surface.transform.localScale = scale;
        surface.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(surface.GetComponent<Collider>());
        PhysicsSurface physicsSurface = surface.AddComponent<PhysicsSurface>();
        physicsSurface.surfaceKind = kind;
        physicsSurface.frictionCoefficient = friction;
        physicsSurface.restitution = restitution;
        physicsManager.RegisterSurface(physicsSurface);
        spawned.Add(surface);
        return surface;
    }

    private void CreateGoal(Vector3 position)
    {
        GameObject goal = CreateSurface("Goal Hole", SurfaceKind.Goal, position, new Vector3(1.05f, 0.08f, 1.05f), goalMaterial, 0.25f, 0.1f);
        PhysicsSurface surface = goal.GetComponent<PhysicsSurface>();
        surface.isGoal = true;

        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "Goal Rim";
        rim.transform.position = position + Vector3.up * 0.055f;
        rim.transform.localScale = new Vector3(1.25f, 0.035f, 1.25f);
        rim.GetComponent<Renderer>().sharedMaterial = goalMaterial;
        Destroy(rim.GetComponent<Collider>());
        spawned.Add(rim);
    }

    private void CreateObstacle(
        string name,
        ObstacleKind kind,
        Vector3 position,
        Vector3 scale,
        Material material,
        float restitution,
        float friction,
        bool circular,
        float radius)
    {
        GameObject obstacle = GameObject.CreatePrimitive(circular ? PrimitiveType.Cylinder : PrimitiveType.Cube);
        obstacle.name = name;
        obstacle.transform.position = position;
        obstacle.transform.localScale = scale;
        obstacle.GetComponent<Renderer>().sharedMaterial = material;
        Destroy(obstacle.GetComponent<Collider>());

        PhysicsObstacle physicsObstacle = obstacle.AddComponent<PhysicsObstacle>();
        physicsObstacle.obstacleKind = kind;
        physicsObstacle.restitution = restitution;
        physicsObstacle.frictionCoefficient = friction;
        physicsObstacle.circular = circular;
        physicsObstacle.radius = radius;
        physicsManager.RegisterObstacle(physicsObstacle);
        spawned.Add(obstacle);
    }

    private void CreateBoundary(Rect bounds)
    {
        float y = 0.38f;
        CreateWall("North Wall", new Vector3(0f, y, bounds.yMax), new Vector3(bounds.width + WallThickness * 2f, 0.76f, WallThickness));
        CreateWall("South Wall", new Vector3(0f, y, bounds.yMin), new Vector3(bounds.width + WallThickness * 2f, 0.76f, WallThickness));
        CreateWall("West Wall", new Vector3(bounds.xMin, y, bounds.center.y), new Vector3(WallThickness, 0.76f, bounds.height));
        CreateWall("East Wall", new Vector3(bounds.xMax, y, bounds.center.y), new Vector3(WallThickness, 0.76f, bounds.height));
    }

    private void CreateWall(string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        Destroy(wall.GetComponent<Collider>());
        spawned.Add(wall);
    }

    private void ClearLevel()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
            {
                Destroy(spawned[i]);
            }
        }

        spawned.Clear();
        physicsManager.ClearRegisteredCourse();
    }

    private void CreateBallAndUi()
    {
        GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballObject.name = "Custom Physics Golf Ball";
        ballObject.transform.localScale = Vector3.one * physicsManager.ballRadius * 2f;
        ballObject.GetComponent<Renderer>().sharedMaterial = ballMaterial;
        Destroy(ballObject.GetComponent<Collider>());

        ball = ballObject.AddComponent<BallController>();
        ball.gameplayCamera = Camera.main;

        GameObject aim = new GameObject("Aim Line");
        ball.aimLine = aim.AddComponent<LineRenderer>();
        GameObject prediction = new GameObject("Prediction Line");
        ball.predictionLine = prediction.AddComponent<LineRenderer>();

        Canvas canvas = new GameObject("HUD Canvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        levelText = CreateText(canvas.transform, "Level Text", new Vector2(18f, -18f), new Vector2(620f, 46f), 24, TextAnchor.UpperLeft);
        ball.statusText = CreateText(canvas.transform, "Status Text", new Vector2(18f, -66f), new Vector2(720f, 70f), 20, TextAnchor.UpperLeft);
        ball.physicsText = CreateText(canvas.transform, "Physics Text", new Vector2(18f, -150f), new Vector2(520f, 150f), 18, TextAnchor.UpperLeft);
    }

    private Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private void CreateLabel(string name, string message, Vector3 position)
    {
        GameObject labelObject = new GameObject(name);
        labelObject.transform.position = position;
        labelObject.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
        TextMesh mesh = labelObject.AddComponent<TextMesh>();
        mesh.text = message;
        mesh.fontSize = 34;
        mesh.characterSize = 0.13f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = new Color(0.05f, 0.08f, 0.1f);
        spawned.Add(labelObject);
    }

    private void SetupCameraAndLight()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 12.5f, -11.5f);
            camera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            camera.fieldOfView = 48f;
            camera.backgroundColor = new Color(0.48f, 0.68f, 0.82f);
        }

        Light light = FindObjectOfType<Light>();
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            light.intensity = 1.25f;
        }
    }

    private void CreateMaterials()
    {
        grassMaterial = CreateMaterial("Mat Grass", new Color(0.18f, 0.56f, 0.28f));
        iceMaterial = CreateMaterial("Mat Ice", new Color(0.48f, 0.86f, 1f));
        sandMaterial = CreateMaterial("Mat Sand", new Color(0.88f, 0.73f, 0.38f));
        slopeMaterial = CreateMaterial("Mat Slope", new Color(0.38f, 0.62f, 0.38f));
        airMaterial = CreateMaterial("Mat Air", new Color(0.25f, 0.45f, 0.95f, 0.55f));
        wallMaterial = CreateMaterial("Mat Wall", new Color(0.36f, 0.36f, 0.38f));
        rubberMaterial = CreateMaterial("Mat Rubber", new Color(0.05f, 0.05f, 0.06f));
        foamMaterial = CreateMaterial("Mat Foam", new Color(0.95f, 0.42f, 0.35f));
        goalMaterial = CreateMaterial("Mat Goal", new Color(0.02f, 0.02f, 0.025f));
        ballMaterial = CreateMaterial("Mat Ball", new Color(1f, 0.97f, 0.82f));
    }

    private Material CreateMaterial(string name, Color color)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.name = name;
        material.color = color;
        if (color.a < 0.99f)
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        return material;
    }

    private string GetLevelTitle(int index)
    {
        if (index == 0)
        {
            return "AA2 3D迷你高尔夫 - Level 1 / 混合摩擦区域";
        }

        if (index == 1)
        {
            return "AA2 3D迷你高尔夫 - Level 2 / 斜坡与空气阻力";
        }

        return "AA2 3D迷你高尔夫 - Level 3 / 障碍碰撞挑战";
    }
}
