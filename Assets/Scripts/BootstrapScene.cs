using UnityEngine;

public sealed class BootstrapScene : MonoBehaviour
{
    private void Awake()
    {
        if (FindObjectOfType<PhysicsManager>() == null)
        {
            new GameObject("Physics Manager").AddComponent<PhysicsManager>();
        }

        if (FindObjectOfType<LevelLoader>() == null)
        {
            new GameObject("Level Loader").AddComponent<LevelLoader>();
        }
    }
}
