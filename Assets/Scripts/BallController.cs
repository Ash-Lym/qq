using UnityEngine;
using UnityEngine.UI;

public sealed class BallController : MonoBehaviour
{
    [Header("Input")]
    public Camera gameplayCamera;
    public LineRenderer aimLine;
    public LineRenderer predictionLine;
    public float maxLaunchSpeed = 8f;
    public float pixelsToLaunchSpeed = 0.03f;
    public float minLaunchSpeed = 0.35f;
    public int predictionSteps = 42;
    public float predictionTimeStep = 0.06f;

    [Header("Readout")]
    public Text statusText;
    public Text physicsText;

    public Vector3 Velocity { get; private set; }
    public int StrokeCount { get; private set; }
    public int SoftBounceCount { get; private set; }
    public bool HasWon { get; private set; }
    public bool IsMoving => physicsManager != null && Velocity.magnitude > physicsManager.rollingStopSpeed;

    private PhysicsManager physicsManager;
    private LevelLoader levelLoader;
    private Vector3 startPosition;
    private Vector3 dragWorldStart;
    private Vector3 dragScreenStart;
    private bool isDragging;
    private bool waitingForNextLevel;
    private float nextLevelAt;

    private void Start()
    {
        physicsManager = PhysicsManager.Instance;
        if (physicsManager == null)
        {
            physicsManager = FindObjectOfType<PhysicsManager>();
        }

        levelLoader = FindObjectOfType<LevelLoader>();
        startPosition = transform.position;
        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }

        ConfigureLine(aimLine, 0.07f, new Color(1f, 0.95f, 0.15f, 0.95f));
        ConfigureLine(predictionLine, 0.045f, new Color(0.15f, 0.9f, 1f, 0.75f));
        ResetForLevel(startPosition);
    }

    private void Update()
    {
        if (HasWon)
        {
            if (waitingForNextLevel && Time.time >= nextLevelAt)
            {
                waitingForNextLevel = false;
                if (levelLoader != null)
                {
                    levelLoader.LoadNextLevel();
                }
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetForLevel(startPosition);
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            levelLoader?.LoadLevel(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            levelLoader?.LoadLevel(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            levelLoader?.LoadLevel(2);
        }

        HandleMouseInput();
        UpdateReadout();
    }

    private void FixedUpdate()
    {
        if (HasWon || physicsManager == null)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        if (Velocity.sqrMagnitude > 0.000001f)
        {
            Velocity += physicsManager.ComputeAcceleration(transform.position, Velocity) * dt;
        }

        if (Velocity.magnitude < physicsManager.rollingStopSpeed)
        {
            Velocity = Vector3.zero;
        }

        Vector3 resolvedVelocity = Velocity;
        Vector3 nextPosition = transform.position + resolvedVelocity * dt;
        physicsManager.ResolveWorldBounds(ref nextPosition, ref resolvedVelocity);
        bool hitObstacle = physicsManager.ResolveObstacleCollisions(ref nextPosition, ref resolvedVelocity);
        Velocity = resolvedVelocity;
        if (hitObstacle && physicsManager.lastCollisionRestitution <= 0.25f)
        {
            SoftBounceCount++;
        }

        float groundHeight = physicsManager.GetGroundHeight(nextPosition);
        nextPosition.y = groundHeight + physicsManager.ballRadius;

        Vector3 travel = nextPosition - transform.position;
        transform.position = nextPosition;
        RollBall(travel);

        if (physicsManager.IsOverGoal(transform.position) && Velocity.magnitude >= physicsManager.maxHoleSpeed)
        {
            if (statusText != null)
            {
                statusText.text = "洞口速度过快，球擦洞而过";
            }
        }

        if (physicsManager.IsInGoal(transform.position, Velocity))
        {
            WinLevel();
        }
    }

    public void ResetForLevel(Vector3 spawnPosition)
    {
        startPosition = spawnPosition;
        Velocity = Vector3.zero;
        StrokeCount = 0;
        SoftBounceCount = 0;
        HasWon = false;
        waitingForNextLevel = false;
        transform.position = spawnPosition;
        transform.rotation = Quaternion.identity;
        HideLines();
        UpdateReadout();
    }

    private void HandleMouseInput()
    {
        if (gameplayCamera == null || IsMoving)
        {
            if (!isDragging)
            {
                HideLines();
            }

            return;
        }

        if (Input.GetMouseButtonDown(0) && TryMouseToGround(out dragWorldStart))
        {
            dragScreenStart = Input.mousePosition;
            isDragging = true;
        }

        if (isDragging && Input.GetMouseButton(0) && TryMouseToGround(out Vector3 current))
        {
            Vector3 launchVelocity = GetLaunchVelocity(current);
            DrawAim(launchVelocity);
            DrawPrediction(launchVelocity);
        }

        if (isDragging && Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            HideLines();
            if (TryMouseToGround(out Vector3 end))
            {
                Vector3 launchVelocity = GetLaunchVelocity(end);
                if (launchVelocity.magnitude >= minLaunchSpeed)
                {
                    Velocity = launchVelocity;
                    StrokeCount++;
                }
            }
        }
    }

    private Vector3 GetLaunchVelocity(Vector3 pointerWorld)
    {
        Vector3 worldPull = dragWorldStart - pointerWorld;
        worldPull.y = 0f;
        if (worldPull.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        float screenPull = Vector2.Distance(new Vector2(dragScreenStart.x, dragScreenStart.y), Input.mousePosition);
        float speed = Mathf.Clamp(screenPull * pixelsToLaunchSpeed, 0f, maxLaunchSpeed);
        return worldPull.normalized * speed;
    }

    private bool TryMouseToGround(out Vector3 world)
    {
        Ray ray = gameplayCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            world = ray.GetPoint(distance);
            return true;
        }

        world = Vector3.zero;
        return false;
    }

    private void RollBall(Vector3 travel)
    {
        Vector3 flatTravel = Vector3.ProjectOnPlane(travel, Vector3.up);
        float distance = flatTravel.magnitude;
        if (distance < 0.0001f)
        {
            return;
        }

        Vector3 axis = Vector3.Cross(Vector3.up, flatTravel.normalized);
        float degrees = distance / Mathf.Max(physicsManager.ballRadius, 0.001f) * Mathf.Rad2Deg;
        transform.Rotate(axis, degrees, Space.World);
    }

    private void DrawAim(Vector3 launchVelocity)
    {
        if (aimLine == null)
        {
            return;
        }

        aimLine.enabled = true;
        aimLine.positionCount = 2;
        aimLine.SetPosition(0, transform.position + Vector3.up * 0.08f);
        aimLine.SetPosition(1, transform.position + launchVelocity * 0.45f + Vector3.up * 0.08f);
    }

    private void DrawPrediction(Vector3 launchVelocity)
    {
        if (predictionLine == null || physicsManager == null)
        {
            return;
        }

        predictionLine.enabled = true;
        predictionLine.positionCount = predictionSteps;
        Vector3 simulatedPosition = transform.position;
        Vector3 simulatedVelocity = launchVelocity;

        for (int i = 0; i < predictionSteps; i++)
        {
            simulatedVelocity += physicsManager.ComputeAcceleration(simulatedPosition, simulatedVelocity) * predictionTimeStep;
            if (simulatedVelocity.magnitude < physicsManager.rollingStopSpeed)
            {
                simulatedVelocity = Vector3.zero;
            }

            simulatedPosition += simulatedVelocity * predictionTimeStep;
            physicsManager.ResolveWorldBounds(ref simulatedPosition, ref simulatedVelocity);
            physicsManager.ResolveObstacleCollisions(ref simulatedPosition, ref simulatedVelocity);
            simulatedPosition.y = physicsManager.GetGroundHeight(simulatedPosition) + physicsManager.ballRadius + 0.05f;
            predictionLine.SetPosition(i, simulatedPosition);
        }
    }

    private void HideLines()
    {
        if (aimLine != null)
        {
            aimLine.enabled = false;
        }

        if (predictionLine != null)
        {
            predictionLine.enabled = false;
        }
    }

    private void ConfigureLine(LineRenderer line, float width, Color color)
    {
        if (line == null)
        {
            return;
        }

        line.useWorldSpace = true;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 6;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;
        line.enabled = false;
    }

    private void UpdateReadout()
    {
        if (physicsManager == null)
        {
            return;
        }

        PhysicsSurface surface = physicsManager.GetSurfaceAt(transform.position);
        string surfaceName = surface != null ? surface.surfaceKind.ToString() : "None";
        float speed = Velocity.magnitude;
        float angularSpeed = physicsManager.GetAngularSpeed(Velocity);

        if (statusText != null && !HasWon)
        {
            statusText.text = "拖拽鼠标蓄力击球 | R 重置 | 1/2/3 切关\n"
                + $"击球 {StrokeCount}  速度 {speed:0.00} m/s  表面 {surfaceName}";
        }

        if (physicsText != null)
        {
            physicsText.text = "自定义牛顿物理：无内置刚体组件\n"
                + $"omega = v/r = {angularSpeed:0.00} rad/s\n"
                + $"F_roll = {physicsManager.lastRollingForce.magnitude:0.00} N\n"
                + $"F_parallel = {physicsManager.lastGravityForce.magnitude:0.00} N\n"
                + $"F_drag = {physicsManager.lastAirDragForce.magnitude:0.00} N\n"
                + $"软性碰撞次数 {SoftBounceCount}/2";
        }
    }

    private void WinLevel()
    {
        HasWon = true;
        Velocity = Vector3.zero;
        bool expertWin = transform.position != Vector3.zero
            && StrokeCount > 0
            && physicsManager.IsInGoal(transform.position, Velocity)
            && SoftBounceCount <= 2;

        if (statusText != null)
        {
            statusText.text = expertWin
                ? $"过关！速度 < {physicsManager.maxHoleSpeed:0.0} m/s，软性碰撞 {SoftBounceCount}/2"
                : "过关！";
        }

        waitingForNextLevel = true;
        nextLevelAt = Time.time + 1.75f;
    }
}
