using System;
using System.Collections;
using UnityEngine;

// Se conserva el nombre para mantener la referencia serializada del prefab.
[RequireComponent(typeof(SteeringGhost))]
[DisallowMultipleComponent]
public class HunterFSM2D : MonoBehaviour
{
    static readonly int IsHuntingParameter = Animator.StringToHash("IsHunting");
    static readonly int DieParameter = Animator.StringToHash("Die");
    static readonly int ReviveParameter = Animator.StringToHash("Revive");
    static readonly Action Idle = delegate { };
    const float SightingInterval = 0.25f;

    [SerializeField] AIGameManager2D _manager;
    [SerializeField, Min(0.1f)] float _viewRadius = 5f;
    [SerializeField, Min(0.01f)] float _catchRadius = 0.45f;
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f), Tooltip("Radio de colisión contra paredes en unidades del mundo.")]
    float _bodyRadius = 0.22f;

    [Header("Alertas del equipo")]
    [SerializeField, Min(0.1f), InspectorName("Memoria de alerta"),
        Tooltip("Segundos durante los que recuerda la última posición que vio cualquier fantasma.")]
    float _alertMemorySeconds = 4f;

    [Header("Reaparición")]
    [SerializeField, Min(0.1f), InspectorName("Tiempo para revivir"),
        Tooltip("Segundos desde la captura. El fantasma permanece en el lugar con su animación de muerte.")]
    float _reviveDelay = 5f;

    [Header("Patrulla individual")]
    [SerializeField, InspectorName("Nodos de patrulla"),
        Tooltip("Índices de los nodos de AIManager, en orden. Vacío usa la patrulla global. Editar antes de Play.")]
    int[] _patrolNodes = Array.Empty<int>();
    [SerializeField, InspectorName("Nodo de reincorporación"), Min(-1),
        Tooltip("Debe ser un nodo de esta patrulla. Con -1 se usa el primero.")]
    int _patrolEntryNode = -1;

    HunterPatrol2D _patrol;
    Animator _animator;
    SteeringGhost _motor;
    AStarRoute2D _route;
    BoidAgent2D _visibleTarget;
    Vector2 _lastPosition;
    bool _alive = true;
    bool _pacmanHunts;
    BoidAgent2D _alertTarget;
    Vector2 _alertPosition;
    float _alertUntil;
    float _nextSightingAt;
    bool _hasAlert;
    Action _updateState = delegate { };
    Action _caughtAction = delegate { };
    Action _onRoundStarted = delegate { };
    Action _connectOnEnable = delegate { };
    Action<bool> _roleChanged = delegate { };
    Action _behavior;
    Action _resumePatrol;
    Action _patrolBehavior;
    Action _chaseBehavior;
    Action _fleeBehavior;
    Action _resumePatrolBehavior;
    Action _investigateBehavior;
    Action _evadeAlertBehavior;
    Action<BoidAgent2D, Vector2> _receiveAlert = delegate { };

    public SteeringGhost Motor => _motor;
    public float CaptureRadius => _catchRadius;
    public bool IsAlive => _alive && isActiveAndEnabled;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _motor = GetComponent<SteeringGhost>();
        _motor.ConfigureWallCollision(_walls, _bodyRadius);
        _route = new AStarRoute2D(_motor, _walls, _bodyRadius);
        // Se reutilizan los delegados al cambiar de estado, sin crearlos por frame.
        _patrolBehavior = Patrol;
        _chaseBehavior = Chase;
        _fleeBehavior = Flee;
        _resumePatrolBehavior = ResumePatrol;
        _investigateBehavior = Investigate;
        _evadeAlertBehavior = EvadeAlert;
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
        EnterAliveState();
        _connectOnEnable = ConnectStateEvents;
        ConnectStateEvents();
    }

    void OnEnable() => _connectOnEnable();

    void ConnectStateEvents()
    {
        _manager.MatchStarted += OnMatchStarted;
        _manager.MatchFinished += OnMatchFinished;
        _manager.PacmanSpotted += OnPacmanSpotted;
        if (_manager.Roles != null) _manager.Roles.RoleChanged += OnRoleChanged;
        OnRoleChanged(_manager.Roles != null && _manager.Roles.PacmanIsHunter);
        Action enterRound = _manager.IsRunning ? _onRoundStarted : StopSimulation;
        enterRound();
    }

    void EnterAliveState()
    {
        _alive = true;
        _caughtAction = TryCapture;
        _onRoundStarted = StartSimulation;
        _roleChanged = ApplyRole;
        _receiveAlert = ReceiveAlert;
    }

    void OnMatchStarted() => _onRoundStarted();
    void OnMatchFinished(MatchSide winner) => StopSimulation();
    void OnRoleChanged(bool pacmanHunts) => _roleChanged(pacmanHunts);
    void OnPacmanSpotted(BoidAgent2D target, Vector2 position) => _receiveAlert(target, position);
    void StartSimulation() => _updateState = Simulate;

    void StopSimulation()
    {
        _updateState = delegate { };
        _motor.Stop();
        ClearAlert();
        _nextSightingAt = 0f;
    }

    void ApplyRole(bool pacmanHunts)
    {
        _pacmanHunts = pacmanHunts;
        if (_animator != null) _animator.SetBool(IsHuntingParameter, !pacmanHunts);
        if (_motor.IsFleeing && !pacmanHunts) ReturnToPatrol();
    }

    void OnDisable()
    {
        if (_manager != null)
        {
            _manager.MatchStarted -= OnMatchStarted;
            _manager.MatchFinished -= OnMatchFinished;
            _manager.PacmanSpotted -= OnPacmanSpotted;
            if (_manager.Roles != null) _manager.Roles.RoleChanged -= OnRoleChanged;
        }
        _route?.Clear();
        ClearAlert();
        _nextSightingAt = 0f;
        if (_motor != null)
        {
            _motor.IsFleeing = false;
            _motor.Stop();
        }
    }

    void Update() => _updateState();

    void Simulate()
    {
        Vector2 position = _motor.Position;
        // El motor es dueño de su desplazamiento. Un cambio externo invalida la ruta.
        if (!position.Equals(_lastPosition))
            ReturnToPatrol();

        _resumePatrol();
        UpdateTarget(position, _pacmanHunts);
        _behavior();
        _lastPosition = _motor.Position;
    }

    void UpdateTarget(Vector2 position, bool pacmanHunts)
    {
        BoidAgent2D target = _manager.FindNearestBoid(position, _viewRadius, _walls);
        // Solo un avistamiento propio emite alertas; recibir una nunca la retransmite.
        if (target != null && (!ReferenceEquals(_visibleTarget, target) || Time.time >= _nextSightingAt))
        {
            _nextSightingAt = Time.time + SightingInterval;
            _manager.ReportPacmanSighting(this, target, target.Motor.Position);
        }
        UpdateAlert(target);
        if (_motor.IsFleeing && target == null && !_hasAlert)
            ReturnToPatrol();

        if (pacmanHunts && (target != null || _hasAlert))
        {
            if (!_motor.IsFleeing) _route.Clear();
            _motor.IsFleeing = true;
        }

        if (!ReferenceEquals(_visibleTarget, target))
        {
            _visibleTarget = target;
            if (!_motor.IsFleeing) _route.Clear();
        }

        _behavior = _motor.IsFleeing
            ? (target != null ? _fleeBehavior : _evadeAlertBehavior)
            : (target != null ? _chaseBehavior : _hasAlert ? _investigateBehavior : _patrolBehavior);
        _resumePatrol = _motor.IsFleeing || _visibleTarget != null || _hasAlert ? Idle : _resumePatrolBehavior;
    }

    void ReceiveAlert(BoidAgent2D target, Vector2 position)
    {
        bool newTarget = !_hasAlert || !ReferenceEquals(_alertTarget, target);
        _alertTarget = target;
        _alertPosition = position;
        _alertUntil = Time.time + _alertMemorySeconds;
        _hasAlert = true;
        if (_visibleTarget != null) return; // La visión propia tiene prioridad.
        _resumePatrol = Idle;
        if (newTarget) _route.Clear();
    }

    void UpdateAlert(BoidAgent2D visibleTarget)
    {
        if (!_hasAlert) return;
        bool expired = Time.time >= _alertUntil || _alertTarget == null || !_alertTarget.IsAlive;
        bool arrived = visibleTarget == null && !_pacmanHunts && _route.IsAtPatrolNode(_alertPosition);
        if (!expired && !arrived) return;
        ClearAlert();
        if (visibleTarget == null) ReturnToPatrol();
    }

    void ClearAlert()
    {
        _hasAlert = false;
        _alertTarget = null;
        _alertUntil = 0f;
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

    void ResumePatrol() => _patrol.TryResume(_manager, _route);

    void Patrol()
    {
        if (_route.CanReplan && (!_patrol.IsIndividual || _route.NeedsRoute))
        {
            BeginPlan();
            _patrol.Plan(_manager, _route);
        }
        _route.Move();
    }

    void Chase()
    {
        if (TryChaseDirectly(_visibleTarget)) return;
        if (_route.CanReplan)
        {
            BeginPlan();
            _route.PlanChase(_manager, _visibleTarget.Motor.Position);
        }
        _route.Move();
    }

    void Flee()
    {
        if (_route.CanReplan && _route.NeedsRoute)
        {
            BeginPlan();
            _route.PlanFlee(_manager, _visibleTarget.Motor.Position, _viewRadius);
        }
        _route.Move();
    }

    void Investigate()
    {
        if (_route.CanReplan)
        {
            BeginPlan();
            _route.PlanChase(_manager, _alertPosition);
        }
        _route.Move();
    }

    void EvadeAlert()
    {
        if (_route.CanReplan && _route.NeedsRoute)
        {
            BeginPlan();
            _route.PlanFleeFromPosition(_manager, _alertPosition, _viewRadius);
        }
        _route.Move();
    }

    void BeginPlan()
    {
        _motor.ClearShallowWallOverlap(_manager.ArenaBounds, _walls, _bodyRadius);
        _route.BeginPlan();
    }

    void ReturnToPatrol()
    {
        ClearAlert();
        _motor.IsFleeing = false;
        _visibleTarget = null;
        _patrol.Return();
        _route.Clear();
        _motor.Stop();
        _behavior = _patrolBehavior;
        _resumePatrol = _resumePatrolBehavior;
    }

    public void CaughtByPacman() => _caughtAction();

    void TryCapture()
    {
        if (_manager == null || _manager.Roles == null ||
            !_manager.Roles.PacmanIsHunter) return;
        _alive = false;
        _updateState = delegate { };
        _caughtAction = delegate { };
        _onRoundStarted = delegate { };
        _roleChanged = delegate { };
        _receiveAlert = delegate { };
        ClearAlert();
        _nextSightingAt = 0f;
        _route.Clear();
        _motor.IsFleeing = false;
        _motor.Stop();
        StartCoroutine(DieAndRevive());
    }

    IEnumerator DieAndRevive()
    {
        float reviveAt = Time.time + _reviveDelay;
        yield return AgentDeathAnimation2D.Play(_animator);
        while (Time.time < reviveAt || !isActiveAndEnabled)
        {
            if (_manager == null || _manager.IsFinished) yield break;
            yield return null;
        }
        if (_manager == null || !_manager.IsRunning) yield break;

        // Reutilizá la reincorporación por el grafo, sin cambiar la posición ni los nodos configurados.
        ReturnToPatrol();
        _lastPosition = _motor.Position;
        EnterAliveState();
        OnRoleChanged(_manager.Roles.PacmanIsHunter);
        OnMatchStarted();
        if (_animator != null)
        {
            _animator.ResetTrigger(DieParameter);
            _animator.SetTrigger(ReviveParameter);
        }
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
