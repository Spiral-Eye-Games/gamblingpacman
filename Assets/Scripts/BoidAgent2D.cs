using System.Collections.Generic;
using UnityEngine;

// Asignar a Pacman (y a otros Pacman/boids si se quiere mostrar Flocking).
[RequireComponent(typeof(Steering))]
[DisallowMultipleComponent]
public class BoidAgent2D : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] AIGameManager2D _manager;
    [SerializeField] HunterFSM2D _hunter;
    [SerializeField] BoidNode _rootNode;

    [Header("Percepción y comida")]
    [SerializeField, Min(0.1f)] float _foodSenseRadius = 8f;
    [SerializeField, Min(0.1f)] float _hunterSenseRadius = 5f;
    [SerializeField, Min(0.01f)] float _pickupRadius = 0.35f;

    [Header("Flocking")]
    [SerializeField, Min(0.1f)] float _neighborRadius = 3f;
    [SerializeField, Min(0.01f)] float _separationRadius = 0.8f;
    [SerializeField, Min(0f)] float _separationWeight = 1.7f;
    [SerializeField, Min(0f)] float _alignmentWeight = 0.8f;
    [SerializeField, Min(0f)] float _cohesionWeight = 0.6f;

    [Header("Paseo y paredes")]
    [SerializeField, Min(0.01f)] float _wanderAhead = 1.5f;
    [SerializeField, Min(0.01f)] float _wanderRadius = 1f;
    [SerializeField, Min(0f)] float _wanderJitter = 2f;
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;
    [SerializeField, Min(0.01f)] float _wallLookAhead = 1.2f;

    readonly List<BoidAgent2D> _neighbors = new List<BoidAgent2D>();
    Steering _motor;
    FoodPickup2D _foodTarget;
    HunterFSM2D _visibleHunter;
    float _wanderAngle;
    bool _alive = true;

    public Steering Motor => _motor;
    public bool IsAlive => _alive && isActiveAndEnabled;
    public bool FoodNearby => _foodTarget != null;
    public bool HunterNearby => _visibleHunter != null;
    public bool NeighborsNearby => _neighbors.Count > 0;
    public BoidAction CurrentAction { get; private set; }

    void Awake()
    {
        _motor = GetComponent<Steering>();
        _wanderAngle = Random.Range(-Mathf.PI, Mathf.PI);
    }

    void Start()
    {
        if (_manager == null) _manager = AIGameManager2D.Instance;
        if (_manager == null)
        {
            Debug.LogError("BoidAgent2D necesita AIGameManager2D.", this);
            enabled = false;
            return;
        }
        if (_rootNode == null)
        {
            Debug.LogError("Asigná el nodo raíz del árbol a BoidAgent2D.", this);
            enabled = false;
            return;
        }
        _manager.RegisterBoid(this);
    }

    void OnDisable()
    {
        if (_manager != null) _manager.UnregisterBoid(this);
    }

    void Update()
    {
        if (!_alive || _manager == null || _manager.IsFinished)
        {
            if (_motor != null) _motor.Stop();
            return;
        }

        Perceive();
        _rootNode.Execute(this);
    }

    // ActionNode llama este método, igual que las acciones del NPC de clase.
    public void PerformAction(BoidAction action)
    {
        CurrentAction = action;
        Vector2 steering;

        switch (CurrentAction)
        {
            case BoidAction.GoToFood:
                steering = _motor.Arrive(_foodTarget.transform.position);
                break;
            case BoidAction.EvadeHunter:
                steering = _motor.Evade(_visibleHunter.Motor);
                break;
            case BoidAction.Flock:
                steering = Flock();
                break;
            default:
                steering = _motor.Wander(ref _wanderAngle,
                    _wanderAhead, _wanderRadius, _wanderJitter);
                break;
        }

        // Seguridad local por encima de la acción elegida.
        Vector2 avoid = _motor.AvoidObstacles(_walls, _bodyRadius, _wallLookAhead);
        if (avoid.sqrMagnitude > 0.0001f) steering = avoid;

        _motor.Move(steering);
        transform.position = _manager.AdjustPositionBounds(transform.position);

        if (CurrentAction == BoidAction.GoToFood && _foodTarget != null &&
            Vector2.Distance(_motor.Position, _foodTarget.transform.position)
            <= _pickupRadius)
            _foodTarget.TryConsume(this);
    }

    void Perceive()
    {
        _foodTarget = _manager.FindNearestFood(_motor.Position, _foodSenseRadius);
        if (_hunter == null) _hunter = _manager.Hunter;
        _visibleHunter = null;

        if (_hunter != null && _hunter.isActiveAndEnabled &&
            Vector2.Distance(_motor.Position, _hunter.Motor.Position)
            <= _hunterSenseRadius)
            _visibleHunter = _hunter;

        _neighbors.Clear();
        List<BoidAgent2D> all = _manager.Boids;
        foreach (BoidAgent2D other in all)
        {
            if (other == null || other == this || !other.IsAlive) continue;
            if (Vector2.Distance(other.Motor.Position, _motor.Position)
                <= _neighborRadius)
                _neighbors.Add(other);
        }
    }

    Vector2 Flock()
    {
        Vector2 separation = Vector2.zero;
        Vector2 velocitySum = Vector2.zero;
        Vector2 positionSum = Vector2.zero;
        foreach (BoidAgent2D other in _neighbors)
        {
            Vector2 away = _motor.Position - other.Motor.Position;
            float distance = away.magnitude;
            if (distance > 0.001f && distance < _separationRadius)
                separation += away.normalized / distance;

            velocitySum += other.Motor.Velocity;
            positionSum += other.Motor.Position;
        }

        Vector2 separate = separation.sqrMagnitude > 0.0001f
            ? separation.normalized * _motor.MaxSpeed - _motor.Velocity
            : Vector2.zero;
        Vector2 averageVelocity = velocitySum / _neighbors.Count;
        Vector2 align = averageVelocity.sqrMagnitude > 0.0001f
            ? averageVelocity.normalized * _motor.MaxSpeed - _motor.Velocity
            : Vector2.zero;
        Vector2 center = positionSum / _neighbors.Count;
        Vector2 cohere = _motor.Seek(center);

        return separate * _separationWeight
             + align * _alignmentWeight
             + cohere * _cohesionWeight;
    }

    public void Caught()
    {
        if (!_alive) return;
        _alive = false;
        _motor.Stop();
        if (_manager != null) _manager.BoidCaught();
        gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _foodSenseRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _hunterSenseRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _neighborRadius);
    }
}
