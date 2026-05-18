using System.Collections.Generic;
using UnityEngine;

public enum SurfaceKind
{
    Grass,
    Ice,
    Sand,
    Slope,
    AirField,
    Goal
}

public enum ObstacleKind
{
    Rubber,
    Foam,
    SandTrap,
    BouncePad
}

public sealed class PhysicsSurface : MonoBehaviour
{
    public SurfaceKind surfaceKind = SurfaceKind.Grass;
    [Range(0f, 1f)] public float frictionCoefficient = 0.4f;
    [Range(0f, 1f)] public float restitution = 0.45f;
    public bool isAirField;
    public bool isGoal;
    public Vector3 airDirection = Vector3.forward;
    public float airSpeed = 4f;
    public float dragCoefficient = 0.48f;
    public float airDensity = 1.225f;

    public Vector3 Center => transform.position;
    public Vector3 HalfSize => Vector3.Scale(transform.lossyScale, Vector3.one) * 0.5f;

    public bool ContainsXZ(Vector3 point, float padding = 0f)
    {
        Vector3 center = Center;
        Vector3 half = HalfSize + new Vector3(padding, 0f, padding);
        return point.x >= center.x - half.x && point.x <= center.x + half.x
            && point.z >= center.z - half.z && point.z <= center.z + half.z;
    }

    public float GetSurfaceHeight(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        local.y = 0.5f;
        return transform.TransformPoint(local).y;
    }

    public Vector3 GetSurfaceNormal()
    {
        return transform.up.normalized;
    }
}

public sealed class PhysicsObstacle : MonoBehaviour
{
    public ObstacleKind obstacleKind = ObstacleKind.Rubber;
    [Range(0f, 1f)] public float restitution = 0.8f;
    [Range(0f, 1f)] public float frictionCoefficient = 0.25f;
    public bool circular;
    public float radius = 0.6f;

    public Vector3 Center => transform.position;
    public Vector3 HalfSize => Vector3.Scale(transform.lossyScale, Vector3.one) * 0.5f;
}

public sealed class PhysicsManager : MonoBehaviour
{
    public static PhysicsManager Instance { get; private set; }

    [Header("Ball constants")]
    public float ballRadius = 0.25f;
    public float ballMass = 0.18f;
    public float gravity = 9.81f;
    public float rollingStopSpeed = 0.04f;
    public float rollingResistanceScale = 0.12f;
    public float maxHoleSpeed = 0.5f;

    [Header("World limits")]
    public Rect courseBounds = new Rect(-8f, -7f, 16f, 28f);
    public float wallRestitution = 0.65f;

    [Header("Diagnostics")]
    public bool showForceDebug;
    public Vector3 lastGravityForce;
    public Vector3 lastRollingForce;
    public Vector3 lastAirDragForce;
    public Vector3 lastCollisionNormal;
    public float lastCollisionRestitution;

    private readonly List<PhysicsSurface> surfaces = new List<PhysicsSurface>();
    private readonly List<PhysicsObstacle> obstacles = new List<PhysicsObstacle>();

    public IReadOnlyList<PhysicsSurface> Surfaces => surfaces;
    public IReadOnlyList<PhysicsObstacle> Obstacles => obstacles;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RegisterSurface(PhysicsSurface surface)
    {
        if (surface != null && !surfaces.Contains(surface))
        {
            surfaces.Add(surface);
        }
    }

    public void RegisterObstacle(PhysicsObstacle obstacle)
    {
        if (obstacle != null && !obstacles.Contains(obstacle))
        {
            obstacles.Add(obstacle);
        }
    }

    public void ClearRegisteredCourse()
    {
        surfaces.Clear();
        obstacles.Clear();
    }

    public PhysicsSurface GetSurfaceAt(Vector3 position)
    {
        PhysicsSurface best = null;
        float bestHeight = float.NegativeInfinity;

        for (int i = 0; i < surfaces.Count; i++)
        {
            PhysicsSurface surface = surfaces[i];
            if (surface == null || !surface.ContainsXZ(position, ballRadius * 0.75f))
            {
                continue;
            }

            float height = surface.GetSurfaceHeight(position);
            if (height > bestHeight)
            {
                bestHeight = height;
                best = surface;
            }
        }

        return best;
    }

    public Vector3 GetSurfaceNormalAt(Vector3 position)
    {
        PhysicsSurface surface = GetSurfaceAt(position);
        return surface != null ? surface.GetSurfaceNormal() : Vector3.up;
    }

    public float GetGroundHeight(Vector3 position)
    {
        PhysicsSurface surface = GetSurfaceAt(position);
        return surface != null ? surface.GetSurfaceHeight(position) : 0f;
    }

    public Vector3 ComputeAcceleration(Vector3 position, Vector3 velocity)
    {
        PhysicsSurface surface = GetSurfaceAt(position);
        Vector3 normal = surface != null ? surface.GetSurfaceNormal() : Vector3.up;
        float mu = surface != null ? surface.frictionCoefficient : 0.4f;

        Vector3 gravityVector = Vector3.down * gravity;
        Vector3 gravityParallel = Vector3.ProjectOnPlane(gravityVector, normal);
        Vector3 rollingAcceleration = Vector3.zero;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            float normalForce = ballMass * gravity * Mathf.Clamp01(Vector3.Dot(normal, Vector3.up));
            float torque = mu * normalForce * ballRadius;
            float inertia = 0.4f * ballMass * ballRadius * ballRadius;
            float angularDeceleration = torque / inertia;
            float linearDeceleration = angularDeceleration * ballRadius * rollingResistanceScale;
            rollingAcceleration = -velocity.normalized * linearDeceleration;
        }

        Vector3 airAcceleration = Vector3.zero;
        if (surface != null && surface.isAirField)
        {
            Vector3 wind = surface.airDirection.sqrMagnitude > 0.001f
                ? surface.airDirection.normalized * surface.airSpeed
                : Vector3.forward * surface.airSpeed;
            Vector3 relativeVelocity = velocity - wind;
            float area = Mathf.PI * ballRadius * ballRadius;
            Vector3 dragForce = -0.5f * surface.airDensity * surface.dragCoefficient * area
                * relativeVelocity.magnitude * relativeVelocity;
            airAcceleration = dragForce / Mathf.Max(ballMass, 0.001f);
            lastAirDragForce = dragForce;
        }
        else
        {
            lastAirDragForce = Vector3.zero;
        }

        lastGravityForce = ballMass * gravityParallel;
        lastRollingForce = ballMass * rollingAcceleration;

        return gravityParallel + rollingAcceleration + airAcceleration;
    }

    public void ResolveWorldBounds(ref Vector3 position, ref Vector3 velocity)
    {
        lastCollisionNormal = Vector3.zero;
        float minX = courseBounds.xMin + ballRadius;
        float maxX = courseBounds.xMax - ballRadius;
        float minZ = courseBounds.yMin + ballRadius;
        float maxZ = courseBounds.yMax - ballRadius;

        if (position.x < minX)
        {
            position.x = minX;
            ReflectVelocity(ref velocity, Vector3.right, wallRestitution);
        }
        else if (position.x > maxX)
        {
            position.x = maxX;
            ReflectVelocity(ref velocity, Vector3.left, wallRestitution);
        }

        if (position.z < minZ)
        {
            position.z = minZ;
            ReflectVelocity(ref velocity, Vector3.forward, wallRestitution);
        }
        else if (position.z > maxZ)
        {
            position.z = maxZ;
            ReflectVelocity(ref velocity, Vector3.back, wallRestitution);
        }
    }

    public bool ResolveObstacleCollisions(ref Vector3 position, ref Vector3 velocity)
    {
        bool hit = false;
        for (int i = 0; i < obstacles.Count; i++)
        {
            PhysicsObstacle obstacle = obstacles[i];
            if (obstacle == null)
            {
                continue;
            }

            Vector3 normal;
            float penetration;
            if (obstacle.circular)
            {
                if (!SphereVsCylinderXZ(position, obstacle.Center, ballRadius, obstacle.radius, out normal, out penetration))
                {
                    continue;
                }
            }
            else if (!SphereVsBoxXZ(position, obstacle.Center, obstacle.HalfSize, ballRadius, out normal, out penetration))
            {
                continue;
            }

            position += normal * penetration;
            ReflectVelocity(ref velocity, normal, obstacle.restitution);
            velocity *= Mathf.Clamp01(1f - obstacle.frictionCoefficient * 0.18f);
            hit = true;
        }

        return hit;
    }

    public bool IsInGoal(Vector3 position, Vector3 velocity)
    {
        for (int i = 0; i < surfaces.Count; i++)
        {
            PhysicsSurface surface = surfaces[i];
            if (surface != null && surface.isGoal && surface.ContainsXZ(position, -ballRadius * 0.25f))
            {
                return velocity.magnitude < maxHoleSpeed;
            }
        }

        return false;
    }

    public bool IsOverGoal(Vector3 position)
    {
        for (int i = 0; i < surfaces.Count; i++)
        {
            PhysicsSurface surface = surfaces[i];
            if (surface != null && surface.isGoal && surface.ContainsXZ(position, -ballRadius * 0.25f))
            {
                return true;
            }
        }

        return false;
    }

    public float GetAngularSpeed(Vector3 velocity)
    {
        return velocity.magnitude / Mathf.Max(ballRadius, 0.001f);
    }

    private void ReflectVelocity(ref Vector3 velocity, Vector3 normal, float restitution)
    {
        normal.Normalize();
        float approachSpeed = Vector3.Dot(velocity, normal);
        if (approachSpeed >= 0f)
        {
            return;
        }

        velocity -= (1f + restitution) * approachSpeed * normal;
        lastCollisionNormal = normal;
        lastCollisionRestitution = restitution;
    }

    private static bool SphereVsCylinderXZ(
        Vector3 sphereCenter,
        Vector3 cylinderCenter,
        float sphereRadius,
        float cylinderRadius,
        out Vector3 normal,
        out float penetration)
    {
        Vector2 delta = new Vector2(sphereCenter.x - cylinderCenter.x, sphereCenter.z - cylinderCenter.z);
        float radiusSum = sphereRadius + cylinderRadius;
        float distance = delta.magnitude;

        if (distance >= radiusSum)
        {
            normal = Vector3.zero;
            penetration = 0f;
            return false;
        }

        if (distance < 0.0001f)
        {
            normal = Vector3.forward;
            penetration = radiusSum;
        }
        else
        {
            normal = new Vector3(delta.x / distance, 0f, delta.y / distance);
            penetration = radiusSum - distance;
        }

        return true;
    }

    private static bool SphereVsBoxXZ(
        Vector3 sphereCenter,
        Vector3 boxCenter,
        Vector3 halfSize,
        float sphereRadius,
        out Vector3 normal,
        out float penetration)
    {
        float closestX = Mathf.Clamp(sphereCenter.x, boxCenter.x - halfSize.x, boxCenter.x + halfSize.x);
        float closestZ = Mathf.Clamp(sphereCenter.z, boxCenter.z - halfSize.z, boxCenter.z + halfSize.z);
        Vector2 delta = new Vector2(sphereCenter.x - closestX, sphereCenter.z - closestZ);
        float sqrDistance = delta.sqrMagnitude;

        if (sqrDistance > sphereRadius * sphereRadius)
        {
            normal = Vector3.zero;
            penetration = 0f;
            return false;
        }

        if (sqrDistance > 0.0001f)
        {
            float distance = Mathf.Sqrt(sqrDistance);
            normal = new Vector3(delta.x / distance, 0f, delta.y / distance);
            penetration = sphereRadius - distance;
            return true;
        }

        float dxMin = Mathf.Abs(sphereCenter.x - (boxCenter.x - halfSize.x));
        float dxMax = Mathf.Abs((boxCenter.x + halfSize.x) - sphereCenter.x);
        float dzMin = Mathf.Abs(sphereCenter.z - (boxCenter.z - halfSize.z));
        float dzMax = Mathf.Abs((boxCenter.z + halfSize.z) - sphereCenter.z);
        float min = Mathf.Min(Mathf.Min(dxMin, dxMax), Mathf.Min(dzMin, dzMax));

        if (Mathf.Approximately(min, dxMin))
        {
            normal = Vector3.left;
            penetration = sphereRadius + dxMin;
        }
        else if (Mathf.Approximately(min, dxMax))
        {
            normal = Vector3.right;
            penetration = sphereRadius + dxMax;
        }
        else if (Mathf.Approximately(min, dzMin))
        {
            normal = Vector3.back;
            penetration = sphereRadius + dzMin;
        }
        else
        {
            normal = Vector3.forward;
            penetration = sphereRadius + dzMax;
        }

        return true;
    }
}
