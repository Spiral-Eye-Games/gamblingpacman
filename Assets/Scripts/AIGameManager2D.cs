using System.Collections.Generic;
using UnityEngine;

// Registro, puntaje y consigna visible en la build. No usa físicas dinámicas.
public class AIGameManager2D : MonoBehaviour
{
    public static AIGameManager2D Instance { get; private set; }

    [SerializeField] HunterFSM2D _hunter;
    [SerializeField] bool _showAssignmentInBuild = true;
    [SerializeField] bool _wrapAtBounds;
    [SerializeField] Vector2 _arenaSize = new Vector2(16f, 9f);

    readonly List<BoidAgent2D> _boids = new List<BoidAgent2D>();
    readonly List<FoodPickup2D> _food = new List<FoodPickup2D>();
    bool _hasFood;
    bool _finished;
    string _result;
    int _score;

    public List<BoidAgent2D> Boids => _boids;
    public HunterFSM2D Hunter => _hunter;
    public bool IsFinished => _finished;
    public int Score => _score;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Debe haber un solo AIGameManager2D en la escena.", this);
            enabled = false;
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RegisterBoid(BoidAgent2D boid)
    {
        if (boid != null && !_boids.Contains(boid)) _boids.Add(boid);
    }

    public void UnregisterBoid(BoidAgent2D boid)
    {
        _boids.Remove(boid);
    }

    public void RegisterHunter(HunterFSM2D hunter)
    {
        if (_hunter == null) _hunter = hunter;
    }

    public void RegisterFood(FoodPickup2D item)
    {
        if (item == null || _food.Contains(item)) return;
        _food.Add(item);
        _hasFood = true;
    }

    public void UnregisterFood(FoodPickup2D item)
    {
        _food.Remove(item);
    }

    public FoodPickup2D FindNearestFood(Vector2 position, float radius)
    {
        FoodPickup2D nearest = null;
        float bestDistance = radius;

        foreach (FoodPickup2D item in _food)
        {
            if (item == null || !item.IsAvailable) continue;
            float distance = Vector2.Distance(item.transform.position, position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = item;
        }
        return nearest;
    }

    // Es el ajuste de bordes de GameManager visto en clase, adaptado a 2D.
    public Vector2 AdjustPositionBounds(Vector2 position)
    {
        if (!_wrapAtBounds) return position;
        Vector2 center = transform.position;
        Vector2 half = _arenaSize * 0.5f;
        if (position.x > center.x + half.x) position.x = center.x - half.x;
        else if (position.x < center.x - half.x) position.x = center.x + half.x;
        if (position.y > center.y + half.y) position.y = center.y - half.y;
        else if (position.y < center.y - half.y) position.y = center.y + half.y;
        return position;
    }

    public void FoodConsumed(FoodPickup2D item)
    {
        if (_finished || item == null) return;
        _score += item.Points;

        if (!_hasFood) return;
        foreach (FoodPickup2D food in _food)
        {
            if (food != null && food.IsAvailable) return;
        }
        Finish("Pacman ganó: se terminó la comida.");
    }

    public void BoidCaught()
    {
        if (_finished) return;
        foreach (BoidAgent2D boid in _boids)
        {
            if (boid != null && boid.IsAlive) return;
        }
        Finish("Ganó el cazador: no quedan boids.");
    }

    void Finish(string result)
    {
        _finished = true;
        _result = result;
        Debug.Log(result, this);
    }

    void OnGUI()
    {
        if (!_showAssignmentInBuild) return;

        GUI.Box(new Rect(10, 10, 510, 154), "IA 1 - TP 1 | Pacman autónomo");
        GUI.Label(new Rect(22, 38, 485, 26),
            $"Puntos: {_score}    Boids: {_boids.Count}    " +
            $"Cazador: {(_hunter != null ? _hunter.CurrentState.ToString() : "sin asignar")}");

        GUIStyle text = new GUIStyle(GUI.skin.label) { wordWrap = true };
        GUI.Label(new Rect(22, 67, 485, 64),
            "Consigna: boids con Flocking (separación, alineación y cohesión); " +
            "árbol de decisión para comida/Arrive, cazador/Evade, grupo y Wander; " +
            "cazador con FSM Rest, Patrol y Hunting/Pursuit.", text);
        if (_finished)
            GUI.Label(new Rect(22, 132, 485, 24), _result);
    }

    void OnDrawGizmosSelected()
    {
        if (!_wrapAtBounds) return;
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, _arenaSize);
    }
}
