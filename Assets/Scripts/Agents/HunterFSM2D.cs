using System;
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

    [Header("Patrulla individual")]
    [SerializeField, InspectorName("Nodos de patrulla"),
        Tooltip("Índices de los nodos de AIManager, en orden. Vacío usa la patrulla global. Editar antes de Play.")]
    int[] _patrolNodes = Array.Empty<int>();
    [SerializeField, InspectorName("Nodo de reincorporación"), Min(-1),
        Tooltip("Debe ser un nodo de esta patrulla. Con -1 se usa el primero.")]
    int _patrolEntryNode = -1;

    HunterPatrol2D _patrol;
    SteeringGhost _motor;
    AStarRoute2D _route;
    BoidAgent2D _visibleTarget;
    Vector2 _lastPosition;
    bool _alive = true;

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
        _patrol = new HunterPatrol2D(_patrolNodes, _manager.NavigationNodes.Count, _patrolEntryNode,
            message => Debug.LogWarning(message, this));
        ReturnToPatrol();
    }

    void OnDisable()
    {
        _route?.Clear();
        if (_motor != null)
        {
            _motor.IsFleeing = false;
            _motor.Stop();
        }
    }

    void Update()
    {
        if (_manager == null || !_manager.IsRunning || !_alive)
        {
            _motor.Stop();
            return;
        }

        Vector2 position = _motor.Position;
        bool pacmanHunts = _manager.Roles.PacmanIsHunter;
        // El motor es dueño de su desplazamiento. Un cambio externo invalida la ruta.
        if (!position.Equals(_lastPosition) || (_motor.IsFleeing && !pacmanHunts))
            ReturnToPatrol();

        if (!_motor.IsFleeing && _visibleTarget == null)
            _patrol.TryResume(_manager, _route);

        BoidAgent2D target = UpdateTarget(position, pacmanHunts);
        if (!pacmanHunts && target != null && TryChaseDirectly(target))
        {
            _lastPosition = _motor.Position;
            return;
        }

        PlanRoute(target);
        _route.Move();
        _lastPosition = _motor.Position;
    }

    BoidAgent2D UpdateTarget(Vector2 position, bool pacmanHunts)
    {
        BoidAgent2D target = _manager.FindNearestBoid(position, _viewRadius, _walls);
        if (_motor.IsFleeing && target == null)
            ReturnToPatrol();

        if (pacmanHunts && target != null)
        {
            if (!_motor.IsFleeing) _route.Clear();
            _motor.IsFleeing = true;
        }

        if (!ReferenceEquals(_visibleTarget, target))
        {
            _visibleTarget = target;
            if (!_motor.IsFleeing) _route.Clear();
        }

        return target;
    }

    bool TryChaseDirectly(BoidAgent2D target)
    {
        if (!_manager.IsInsideArena(_motor.Position, _bodyRadius) ||
            !_manager.IsInsideArena(target.Motor.Position, _bodyRadius) ||
            !_motor.MoveToWaypoint(target.Motor.Position))
            return false;

        _route.Clear();
        return true;
    }

    void PlanRoute(BoidAgent2D target)
    {
        // Elegí el siguiente tramo al terminar el actual; la visión se comprueba cada frame.
        bool needsPlan = _motor.IsFleeing
            ? _route.NeedsRoute
            : !_patrol.IsIndividual || target != null || _route.NeedsRoute;
        if (!_route.CanReplan || !needsPlan) return;

        _motor.ClearShallowWallOverlap(_manager.ArenaBounds, _walls, _bodyRadius);
        _route.BeginPlan();
        if (_motor.IsFleeing)
            _route.PlanFlee(_manager, target.Motor.Position, _viewRadius);
        else if (target != null)
            _route.PlanChase(_manager, target.Motor.Position);
        else
            _patrol.Plan(_manager, _route);
    }

    void ReturnToPatrol()
    {
        _motor.IsFleeing = false;
        _visibleTarget = null;
        _patrol.Return();
        _route.Clear();
        _motor.Stop();
    }

    public void CaughtByPacman()
    {
        if (!_alive || _manager == null || _manager.Roles == null ||
            !_manager.Roles.PacmanIsHunter) return;
        _alive = false;
        _motor.Stop();
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
