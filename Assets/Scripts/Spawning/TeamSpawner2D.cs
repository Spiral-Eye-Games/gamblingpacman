using System.Collections.Generic;
using UnityEngine;

// Usa los agentes de la escena como prototipos para las copias.
public class TeamSpawner2D : MonoBehaviour
{
    const int PlacementAttempts = 60;

    [SerializeField, Min(0)] int _extraBoids = 2;
    [SerializeField, Min(0)] int _extraHunters = 1;

    [Header("Zonas relativas a AIManager")]
    [SerializeField] Vector2 _boidAreaCenter = new Vector2(-1.3f, -2f);
    [SerializeField] Vector2 _boidAreaSize = new Vector2(2f, 1.6f);
    [SerializeField] Vector2 _hunterAreaCenter = new Vector2(1.3f, 2.3f);
    [SerializeField] Vector2 _hunterAreaSize = new Vector2(1.8f, 1.6f);

    [Header("Espacio libre")]
    [SerializeField, Min(0.01f)] float _spawnRadius = 0.45f;
    [SerializeField] LayerMask _walls;

    void Start()
    {
        AIGameManager2D manager = AIGameManager2D.Instance;
        BoidAgent2D[] boids = FindObjectsByType<BoidAgent2D>(FindObjectsSortMode.None);
        HunterFSM2D[] hunters = FindObjectsByType<HunterFSM2D>(FindObjectsSortMode.None);
        if (manager == null || boids.Length == 0 || hunters.Length == 0)
        {
            Debug.LogError("TeamSpawner2D necesita AIManager, un boid y un fantasma en la escena.", this);
            return;
        }

        //incluir los agentes originales evita que las copias nazcan encima
        List<Vector2> occupied = new List<Vector2>();
        for (int i = 0; i < boids.Length; i++) occupied.Add(boids[i].transform.position);
        for (int i = 0; i < hunters.Length; i++) occupied.Add(hunters[i].transform.position);

        var placement = new SpawnPlacement2D(manager, _walls, _spawnRadius, PlacementAttempts);
        SpawnCopies(boids[0], _extraBoids, _boidAreaCenter, _boidAreaSize, placement, occupied,
            "Pacman boid ", "No quedó lugar para otro boid en su zona de aparición.");
        SpawnCopies(hunters[0], _extraHunters, _hunterAreaCenter, _hunterAreaSize, placement, occupied,
            "Fantasma ", "No quedó lugar para otro fantasma en su zona de aparición.");
    }

    void SpawnCopies<T>(T prototype, int count, Vector2 localCenter, Vector2 size,
        SpawnPlacement2D placement, List<Vector2> occupied, string namePrefix, string warning)
        where T : Component
    {
        Vector2 center = (Vector2)transform.position + localCenter;
        for (int i = 1; i <= count; i++)
        {
            if (!placement.TryFindPosition(center, size, out Vector2 position, occupied))
            {
                Debug.LogWarning(warning, this);
                return;
            }

            T copy = Instantiate(prototype, position, prototype.transform.rotation);
            copy.name = namePrefix + (i + 1);
            occupied.Add(position);
        }
    }

    void OnValidate()
    {
        _boidAreaSize.x = Mathf.Max(0.01f, _boidAreaSize.x);
        _boidAreaSize.y = Mathf.Max(0.01f, _boidAreaSize.y);
        _hunterAreaSize.x = Mathf.Max(0.01f, _hunterAreaSize.x);
        _hunterAreaSize.y = Mathf.Max(0.01f, _hunterAreaSize.y);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube((Vector2)transform.position + _boidAreaCenter,
            _boidAreaSize);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube((Vector2)transform.position + _hunterAreaCenter,
            _hunterAreaSize);
    }
}
