using UnityEngine;

//FSM maquina de estados del fantasma rest, patrol, hunting y fleeing
//Misma lógica que la versión con patrón State, pero como switch simple:
//para 4 estados no hace falta una clase por estado.
[RequireComponent(typeof(SteeringGhost))]
[DisallowMultipleComponent]
public class HunterFSM2D : MonoBehaviour
{
    public enum HunterState { Rest, Patrol, Hunting, Fleeing }

    [Header("Referencias")]
    [SerializeField] AIGameManager2D _manager;
    [SerializeField] Transform[] _waypoints;

    [Header("Energía")]
    [SerializeField, Min(0.01f)] float _maximumEnergy = 15f;
    [SerializeField, Min(0.01f)] float _restTime = 4f;
    [SerializeField, Min(0f)] float _patrolEnergyPerSecond = 1f;
    [SerializeField, Min(0f)] float _huntingEnergyPerSecond = 2f;

    [Header("Percepción y persecución")]
    [SerializeField, Min(0.1f)] float _viewRadius = 5f;
    [SerializeField, Min(0.01f)] float _catchRadius = 0.45f;
    [SerializeField, Min(0.01f)] float _waypointRadius = 0.35f;
    [SerializeField, Min(0f)] float _predictionTime = 1f;

    [Header("Paredes")]
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;
    [SerializeField, Min(0.01f)] float _wallLookAhead = 1.2f;

    [Header("Paseo sin objetivo")]
    [SerializeField, Min(0.01f)] float _wanderAhead = 1.5f;
    [SerializeField, Min(0.01f)] float _wanderRadius = 1f;
    [SerializeField, Min(0f)] float _wanderJitter = 2f;

    [Header("Colisión con fantasmas")]
    [SerializeField, Min(0.01f)] float _teamCollisionRadius = 0.45f;

    SteeringGhost _motor;
    BoidAgent2D _target;
    int _waypointIndex;
    float _wanderAngle;
    float _restElapsed;
    bool _stateStarted;
    bool _alive = true;

    public SteeringGhost Motor => _motor;
    public float CaptureRadius => _catchRadius;
    public float TeamCollisionRadius => _teamCollisionRadius;
    public bool IsAlive => _alive && isActiveAndEnabled;
    public float Energy { get; private set; }
    public HunterState CurrentState { get; private set; }

    void Awake()
    {
        _motor = GetComponent<SteeringGhost>();
        _motor.ConfigureWallCollision(_walls, _bodyRadius);
        Energy = _maximumEnergy;
        _wanderAngle = Random.Range(-Mathf.PI, Mathf.PI);
    }

    void Start()
    {
        if (_manager == null) _manager = AIGameManager2D.Instance;
        if (_manager == null)
        {
            Debug.LogError("HunterFSM necesita AIGameManager", this);
            enabled = false;
            return;
        }

        _manager.RegisterHunter(this);
        ChangeState(HunterState.Patrol);
    }

    void OnDisable()
    {
        if (_motor != null) _motor.Stop();
    }

    void Update()
    {
        if (_manager == null || !_manager.IsRunning || !_alive)
        {
            _motor.Stop();
            return;
        }

        switch (CurrentState)
        {
            case HunterState.Rest: UpdateRest(); break;
            case HunterState.Patrol: UpdatePatrol(); break;
            case HunterState.Hunting: UpdateHunting(); break;
            case HunterState.Fleeing: UpdateFleeing(); break;
        }
    }

    void ChangeState(HunterState next)
    {
        if (_stateStarted && CurrentState == next) return;
        _stateStarted = true;
        CurrentState = next;
        if (next == HunterState.Rest)
        {
            _restElapsed = 0f;
            _motor.Stop();
        }
    }

    void UpdateRest()
    {
        _restElapsed += Time.deltaTime;
        if (_restElapsed < _restTime) return;
        Energy = _maximumEnergy;
        ChangeState(_manager.Roles.PacmanIsHunter
            ? HunterState.Fleeing : HunterState.Patrol);
    }

    void UpdatePatrol()
    {
        if (_manager.Roles.PacmanIsHunter)
        {
            ChangeState(HunterState.Fleeing);
            return;
        }

        Energy = Mathf.Max(0f, Energy - _patrolEnergyPerSecond * Time.deltaTime);
        if (Energy <= 0f)
        {
            ChangeState(HunterState.Rest);
            return;
        }

        _target = FindVisibleBoid();
        if (_target != null)
        {
            ChangeState(HunterState.Hunting);
            return;
        }

        Vector2 steering;
        if (_waypoints != null && _waypoints.Length > 0 &&
            _waypoints[_waypointIndex] != null)
        {
            Vector2 point = _waypoints[_waypointIndex].position;
            steering = _motor.Arrive(point);
            if (Vector2.Distance(_motor.Position, point) <= _waypointRadius)
                _waypointIndex = (_waypointIndex + 1) % _waypoints.Length;
        }
        else
        {
            steering = _motor.Wander(ref _wanderAngle,
                _wanderAhead, _wanderRadius, _wanderJitter);
        }

        SafetyMove(steering);
    }

    void UpdateHunting()
    {
        if (_manager.Roles.PacmanIsHunter)
        {
            _target = null;
            ChangeState(HunterState.Fleeing);
            return;
        }

        Energy = Mathf.Max(0f, Energy - _huntingEnergyPerSecond * Time.deltaTime);
        if (Energy <= 0f)
        {
            ChangeState(HunterState.Rest);
            return;
        }

        if (!CanSee(_target))
        {
            _target = null;
            ChangeState(HunterState.Patrol);
            return;
        }

        SafetyMove(_motor.Pursuit(_target.Motor, _predictionTime));
    }

    void UpdateFleeing()
    {
        Energy = Mathf.Max(0f, Energy - _huntingEnergyPerSecond * Time.deltaTime);
        if (Energy <= 0f)
        {
            ChangeState(HunterState.Rest);
            return;
        }

        BoidAgent2D pursuer = _manager.FindNearestBoid(_motor.Position, _viewRadius);
        Vector2 steering = pursuer != null
            ? _motor.Evade(pursuer.Motor, _predictionTime)
            : _motor.Wander(ref _wanderAngle,
                _wanderAhead, _wanderRadius, _wanderJitter);
        SafetyMove(steering);
    }

    BoidAgent2D FindVisibleBoid()
    {
        BoidAgent2D closest = null;
        float bestDistance = _viewRadius;
        var boids = _manager.Boids;

        for (int i = 0; i < boids.Count; i++)
        {
            BoidAgent2D boid = boids[i];
            if (!CanSee(boid)) continue;
            float distance = Vector2.Distance(boid.Motor.Position, _motor.Position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            closest = boid;
        }
        return closest;
    }

    bool CanSee(BoidAgent2D boid)
    {
        if (boid == null || !boid.IsAlive) return false;
        return Vector2.Distance(_motor.Position, boid.Motor.Position) <= _viewRadius;
    }

    public void CaughtByPacman()
    {
        if (!_alive || _manager == null || _manager.Roles == null ||
            !_manager.Roles.PacmanIsHunter) return;
        _alive = false;
        _motor.Stop();
        _manager.GhostCaught();
        gameObject.SetActive(false);
    }

    void SafetyMove(Vector2 steering)
    {
        Vector2 avoid = _motor.AvoidObstacles(_walls, _bodyRadius, _wallLookAhead,
            steering);
        if (avoid.sqrMagnitude > 0.0001f) steering = avoid;
        Vector2 beforeMove = _motor.Position;
        _motor.Move(steering);
        transform.position = _manager.ApplyWrapAreas(beforeMove, transform.position);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _viewRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _catchRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _teamCollisionRadius);
    }
}
