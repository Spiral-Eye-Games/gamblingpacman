using UnityEngine;

// Se conserva el nombre para mantener la referencia serializada del prefab.
[RequireComponent(typeof(SteeringGhost))]
[DisallowMultipleComponent]
public class HunterFSM2D : MonoBehaviour
{
    [SerializeField] AIGameManager2D _manager;
    [SerializeField] Transform[] _waypoints;
    [SerializeField, Min(0.1f)] float _viewRadius = 5f;
    [SerializeField, Min(0.01f)] float _catchRadius = 0.45f;
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;

    SteeringGhost _motor;
    AStarRoute2D _route;
    int _waypointIndex;
    int _patrolIndex;
    bool _alive = true;
    bool _pacmanWasHunter;

    public SteeringGhost Motor => _motor;
    public float CaptureRadius => _catchRadius;
    public bool IsAlive => _alive && isActiveAndEnabled;

    void Awake()
    {
        _motor = GetComponent<SteeringGhost>();
        _motor.ConfigureWallCollision(_walls, _bodyRadius);
        _route = new AStarRoute2D(_motor, _walls, _bodyRadius);
    }

    void Start()
    {
        if (_manager == null) _manager = AIGameManager2D.Instance;
        if (_manager == null)
        {
            Debug.LogError("HunterFSM2D necesita AIGameManager2D.", this);
            enabled = false;
            return;
        }

        if (_waypoints != null && _waypoints.Length > 0)
            _waypointIndex = _manager.Hunters.Count % _waypoints.Length;

        _manager.RegisterHunter(this);
    }

    void OnDisable()
    {
        _route?.Clear();
        if (_motor != null) _motor.Stop();
    }

    void Update()
    {
        if (_manager == null || !_manager.IsRunning || !_alive)
        {
            _motor.Stop();
            return;
        }

        bool pacmanHunts = _manager.Roles.PacmanIsHunter;
        if (_pacmanWasHunter != pacmanHunts)
        {
            _pacmanWasHunter = pacmanHunts;
            _route.Clear();
        }

        if (_route.CanReplan)
        {
            _route.BeginPlan();
            BoidAgent2D target = pacmanHunts ? null :
                _manager.FindNearestBoid(_motor.Position, _viewRadius);

            if (target == null || !_route.TryPlan(_manager, target.Motor.Position))
                PlanPatrol();
        }

        _route.Move();
    }

    void PlanPatrol()
    {
        int count = _waypoints?.Length ?? 0;
        for (int i = 0; i < count; i++)
        {
            Transform point = _waypoints[_waypointIndex];
            if (point != null &&
                ((Vector2)point.position - _motor.Position).sqrMagnitude > AStarRoute2D.PatrolArrivalSqr &&
                _route.TryPlan(_manager, point.position))
                return;

            _waypointIndex = (_waypointIndex + 1) % count;
        }

        _route.PlanPatrolNodes(_manager, ref _patrolIndex);
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

    void OnDrawGizmosSelected()
    {
        _route?.Draw(Color.cyan);
    }
}
