using UnityEngine;

// Se conserva el nombre para mantener la referencia serializada del prefab.
[RequireComponent(typeof(SteeringGhost))]
[DisallowMultipleComponent]
public class HunterFSM2D : MonoBehaviour
{
    [SerializeField] AIGameManager2D _manager;
    [SerializeField, Min(0.1f)] float _viewRadius = 5f;
    [SerializeField, Min(0.01f)] float _catchRadius = 0.45f;
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f), Tooltip("Radio de colisión contra paredes en unidades del mundo.")]
    float _bodyRadius = 0.22f;

    SteeringGhost _motor;
    AStarRoute2D _route;
    BoidAgent2D _visibleTarget;
    Vector2 _lastPosition;
    int _patrolIndex;
    int _patrolEntryIndex = -1;
    bool _alive = true;
    bool _returningToPatrol;

    public SteeringGhost Motor => _motor;
    public float CaptureRadius => _catchRadius;
    public bool IsAlive => _alive && isActiveAndEnabled;

    void Awake()
    {
        _motor = GetComponent<SteeringGhost>();
        _motor.ConfigureWallCollision(_walls, _bodyRadius);
        _route = new AStarRoute2D(_motor, _walls, _bodyRadius);
        _lastPosition = _motor.Position;
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

        _manager.RegisterHunter(this);
        ReturnToPatrol();
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

        Vector2 position = _motor.Position;
        // El motor es dueño de su desplazamiento. Un cambio externo invalida la ruta.
        if (!position.Equals(_lastPosition))
            ReturnToPatrol();

        if (_returningToPatrol && _patrolEntryIndex >= 0 &&
            _route.IsAtPatrolNode(_manager.NavigationNodes[_patrolEntryIndex]))
        {
            _returningToPatrol = false;
            _patrolIndex = (_patrolEntryIndex + 1) % _manager.NavigationNodes.Count;
            _patrolEntryIndex = -1;
            _route.Clear();
        }

        BoidAgent2D target = _returningToPatrol || _manager.Roles.PacmanIsHunter
            ? null : _manager.FindNearestBoid(position, _viewRadius, _walls);
        if (!ReferenceEquals(_visibleTarget, target))
        {
            _visibleTarget = target;
            _route.Clear();
        }

        if (_route.CanReplan)
        {
            _route.BeginPlan();
            if (_returningToPatrol)
            {
                if (_patrolEntryIndex < 0 ||
                    !_route.TryPlan(_manager, _manager.NavigationNodes[_patrolEntryIndex]))
                    _patrolEntryIndex = _route.PlanPatrolEntry(_manager);
            }
            else if (target == null || !_route.TryPlan(_manager, target.Motor.Position))
            {
                _route.PlanPatrolNodes(_manager, ref _patrolIndex);
            }
        }

        _route.Move();
        _lastPosition = _motor.Position;
    }

    void ReturnToPatrol()
    {
        _visibleTarget = null;
        _returningToPatrol = _manager.NavigationNodes.Count > 0;
        _patrolEntryIndex = -1;
        _route.Clear();
        _motor.Stop();
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _viewRadius);
        if (_visibleTarget != null)
            Gizmos.DrawLine(transform.position, _visibleTarget.transform.position);
    }
}
