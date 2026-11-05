using System.Collections.Generic;
using UnityEngine;

// Usa al Pac-Man de la escena como prototipo para las copias.
public class TeamSpawner2D : MonoBehaviour
{
    const int PlacementAttempts = 60;

    [SerializeField, Min(0)] int _extraBoids = 2;

    [Header("Zona relativa a AIManager")]
    [SerializeField] Vector2 _boidAreaCenter = new Vector2(-1.3f, -2f);
    [SerializeField] Vector2 _boidAreaSize = new Vector2(2f, 1.6f);

    [Header("Espacio libre")]
    [SerializeField, Min(0.01f)] float _spawnRadius = 0.45f;
    [SerializeField] LayerMask _walls;

    void Start()
    {
        if (_extraBoids <= 0) return;

        AIGameManager2D manager = AIGameManager2D.Instance;
        BoidAgent2D[] boids = FindObjectsByType<BoidAgent2D>(FindObjectsSortMode.None);
        if (manager == null || boids.Length == 0)
        {
            Debug.LogError("TeamSpawner2D necesita AIManager y un boid en la escena.", this);
            return;
        }

        // Incluye a los fantasmas de la escena para evitar solaparlos con las copias.
        HunterFSM2D[] hunters = FindObjectsByType<HunterFSM2D>(FindObjectsSortMode.None);
        List<Vector2> occupied = new List<Vector2>();
        for (int i = 0; i < boids.Length; i++) occupied.Add(boids[i].transform.position);
        for (int i = 0; i < hunters.Length; i++) occupied.Add(hunters[i].transform.position);

        var placement = new SpawnPlacement2D(manager, _walls, _spawnRadius, PlacementAttempts);
        BoidAgent2D prototype = boids[0];
        Vector2 center = (Vector2)transform.position + _boidAreaCenter;
        for (int i = 1; i <= _extraBoids; i++)
        {
            if (!placement.TryFindPosition(center, _boidAreaSize, out Vector2 position, occupied))
            {
                Debug.LogWarning("No quedó lugar para otro boid en su zona de aparición.", this);
                return;
            }

            BoidAgent2D copy = Instantiate(prototype, position, prototype.transform.rotation);
            copy.name = "Pacman boid " + (i + 1);
            occupied.Add(position);
        }
    }

    void OnValidate()
    {
        _boidAreaSize.x = Mathf.Max(0.01f, _boidAreaSize.x);
        _boidAreaSize.y = Mathf.Max(0.01f, _boidAreaSize.y);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube((Vector2)transform.position + _boidAreaCenter,
            _boidAreaSize);
    }
}
