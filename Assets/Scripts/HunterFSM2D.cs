using System.Collections.Generic;
using UnityEngine;

// Fantasma cazador del TP: Rest, Patrol y Hunting. Cada estado cambia al siguiente.
[RequireComponent(typeof(Steering))]
[DisallowMultipleComponent]
public class HunterFSM2D : MonoBehaviour
{
    public enum HunterState { Rest, Patrol, Hunting }

    [Header("Referencias")]
    [SerializeField] AIGameManager2D _manager;
    [SerializeField] Transform[] _waypoints;

    [Header("Energía")]
    [SerializeField, Min(0.01f)] float _maximumEnergy = 15f;
    [SerializeField, Min(0.01f)] float _restSeconds = 4f;
    [SerializeField, Min(0f)] float _patrolEnergyPerSecond = 1f;
    [SerializeField, Min(0f)] float _huntingEnergyPerSecond = 2f;

    [Header("Percepción y persecución")]
    [SerializeField, Min(0.1f)] float _viewRadius = 5f;
    [SerializeField, Min(0.01f)] float _catchRadius = 0.45f;
    [SerializeField, Min(0.01f)] float _waypointRadius = 0.35f;
    [SerializeField, Min(0f)] float _predictionSeconds = 1f;

    [Header("Paredes")]
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;
    [SerializeField, Min(0.01f)] float _wallLookAhead = 1.2f;

    Steering _motor;
    readonly Dictionary<HunterState, State> _states = new Dictionary<HunterState, State>();
    State _state;
    BoidAgent2D _target;
    int _waypointIndex;
    float _wanderAngle;

    public Steering Motor => _motor;
    public float Energy { get; private set; }
    public HunterState CurrentState { get; private set; }

    void Awake()
    {
        _motor = GetComponent<Steering>();
        Energy = _maximumEnergy;
        _wanderAngle = Random.Range(-Mathf.PI, Mathf.PI);
        _states.Add(HunterState.Rest, new RestState(this));
        _states.Add(HunterState.Patrol, new PatrolState(this));
        _states.Add(HunterState.Hunting, new HuntingState(this));
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
        ChangeState(HunterState.Patrol);
    }

    void Update()
    {
        if (_manager == null || _manager.IsFinished)
        {
            _motor.Stop();
            return;
        }
        _state?.OnUpdate();
    }

    void OnDisable()
    {
        if (_motor != null) _motor.Stop();
    }

    void ChangeState(HunterState next)
    {
        if (_state != null && CurrentState == next) return;
        _state?.OnExit();
        CurrentState = next;
        _state = _states[next];
        _state.OnEnter();
    }

    BoidAgent2D FindVisibleBoid()
    {
        BoidAgent2D closest = null;
        float bestDistance = _viewRadius;
        List<BoidAgent2D> boids = _manager.Boids;

        foreach (BoidAgent2D boid in boids)
        {
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
        if (Vector2.Distance(_motor.Position, boid.Motor.Position)
            > _viewRadius) return false;
        return true;
    }

    void MoveWithSafety(Vector2 steering)
    {
        Vector2 avoid = _motor.AvoidObstacles(_walls, _bodyRadius, _wallLookAhead);
        if (avoid.sqrMagnitude > 0.0001f) steering = avoid;
        _motor.Move(steering);
        transform.position = _manager.AdjustPositionBounds(transform.position);
    }

    abstract class State
    {
        protected readonly HunterFSM2D Hunter;
        protected State(HunterFSM2D hunter) { Hunter = hunter; }
        public virtual void OnEnter() { }
        public abstract void OnUpdate();
        public virtual void OnExit() { }
    }

    sealed class RestState : State
    {
        float _elapsed;
        public RestState(HunterFSM2D hunter) : base(hunter) { }

        public override void OnEnter()
        {
            _elapsed = 0f;
            Hunter._motor.Stop();
        }

        public override void OnUpdate()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed < Hunter._restSeconds) return;
            Hunter.Energy = Hunter._maximumEnergy;
            Hunter.ChangeState(HunterState.Patrol);
        }
    }

    sealed class PatrolState : State
    {
        public PatrolState(HunterFSM2D hunter) : base(hunter) { }

        public override void OnUpdate()
        {
            Hunter.Energy = Mathf.Max(0f, Hunter.Energy -
                Hunter._patrolEnergyPerSecond * Time.deltaTime);
            if (Hunter.Energy <= 0f)
            {
                Hunter.ChangeState(HunterState.Rest);
                return;
            }

            Hunter._target = Hunter.FindVisibleBoid();
            if (Hunter._target != null)
            {
                Hunter.ChangeState(HunterState.Hunting);
                return;
            }

            Vector2 steering;
            if (Hunter._waypoints != null && Hunter._waypoints.Length > 0 &&
                Hunter._waypoints[Hunter._waypointIndex] != null)
            {
                Vector2 point = Hunter._waypoints[Hunter._waypointIndex].position;
                steering = Hunter._motor.Arrive(point);
                if (Vector2.Distance(Hunter._motor.Position, point)
                    <= Hunter._waypointRadius)
                    Hunter._waypointIndex = (Hunter._waypointIndex + 1)
                        % Hunter._waypoints.Length;
            }
            else
            {
                steering = Hunter._motor.Wander(ref Hunter._wanderAngle,
                    1.5f, 1f, 2f);
            }

            Hunter.MoveWithSafety(steering);
        }
    }

    sealed class HuntingState : State
    {
        public HuntingState(HunterFSM2D hunter) : base(hunter) { }

        public override void OnUpdate()
        {
            Hunter.Energy = Mathf.Max(0f, Hunter.Energy -
                Hunter._huntingEnergyPerSecond * Time.deltaTime);
            if (Hunter.Energy <= 0f)
            {
                Hunter.ChangeState(HunterState.Rest);
                return;
            }

            if (!Hunter.CanSee(Hunter._target))
            {
                Hunter._target = null;
                Hunter.ChangeState(HunterState.Patrol);
                return;
            }

            Hunter.MoveWithSafety(Hunter._motor.Pursuit(
                Hunter._target.Motor, Hunter._predictionSeconds));

            if (Vector2.Distance(Hunter._motor.Position,
                Hunter._target.Motor.Position) <= Hunter._catchRadius)
            {
                Hunter._target.Caught();
                Hunter._target = null;
                Hunter.ChangeState(HunterState.Patrol);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _viewRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _catchRadius);
    }
}
