using System.Collections.Generic;
using UnityEngine;

//copia los agentes de la escena y los coloca dentro de zonas configurables
public class TeamSpawner2D : MonoBehaviour
{
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

        for (int i = 1; i <= _extraBoids; i++)
        {
            Vector2 position;
            if (!TryFindSpawn(manager, _boidAreaCenter, _boidAreaSize,
                occupied, out position))
            {
                Debug.LogWarning("No quedó lugar para otro boid en su zona de aparición.", this);
                break;
            }

            BoidAgent2D copy = Instantiate(boids[0], position, boids[0].transform.rotation);
            copy.name = "Pacman boid " + (i + 1);
            occupied.Add(position);
        }

        for (int i = 1; i <= _extraHunters; i++)
        {
            Vector2 position;
            if (!TryFindSpawn(manager, _hunterAreaCenter, _hunterAreaSize,
                occupied, out position))
            {
                Debug.LogWarning("No quedó lugar para otro fantasma en su zona de aparición.", this);
                break;
            }

            HunterFSM2D copy = Instantiate(hunters[0], position, hunters[0].transform.rotation);
            copy.name = "Fantasma " + (i + 1);
            occupied.Add(position);
        }
    }

    bool TryFindSpawn(AIGameManager2D manager, Vector2 localCenter,
        Vector2 areaSize, List<Vector2> occupied, out Vector2 position)
    {
        Vector2 center = (Vector2)transform.position + localCenter;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            Vector2 candidate = center + new Vector2(
                Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f));
            if (!manager.IsInsideArena(candidate, _spawnRadius)) continue;
            if (_walls.value != 0 &&
                Physics2D.OverlapCircle(candidate, _spawnRadius, _walls) != null)
                continue;

            bool tooClose = false;
            for (int i = 0; i < occupied.Count; i++)
            {
                if (Vector2.Distance(candidate, occupied[i]) >= _spawnRadius * 2f)
                    continue;
                tooClose = true;
                break;
            }
            if (tooClose) continue;

            position = candidate;
            return true;
        }

        position = Vector2.zero;
        return false;
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
