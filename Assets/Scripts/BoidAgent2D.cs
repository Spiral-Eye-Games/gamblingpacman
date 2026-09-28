using System.Collections.Generic;
using UnityEngine;

// Asignar a Pacman (y a otros Pacman/boids si se quiere mostrar Flocking).
[RequireComponent(typeof(Steering))]
[DisallowMultipleComponent]
public class BoidAgent2D : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] AIGameManager2D _manager;

    [Header("Percepción y comida")]
    [SerializeField, Min(0.1f)] float _foodSenseRadius = 8f;
    [SerializeField, Min(0.1f)] float _hunterSenseRadius = 5f;
    [SerializeField, Min(0.1f)] float _huntSenseRadius = 20f; //al cazar, ve a todo el mapa
    [SerializeField, Min(0.01f)] float _pickupRadius = 0.35f;

    [Header("Flocking")]
    [SerializeField, Min(0.1f)] float _neighborRadius = 3f;
    [SerializeField, Min(0.01f)] float _separationRadius = 0.8f;
    [SerializeField, Min(0f)] float _separationWeight = 1.7f;
    [SerializeField, Min(0f)] float _alignmentWeight = 0.8f;
    [SerializeField, Min(0f)] float _cohesionWeight = 0.6f;
    [SerializeField, Min(0f)] float _flockWhileSeekingWeight = 0.25f;

    [Header("Colisión con boids")]
    [SerializeField, Min(0.01f)] float _teamCollisionRadius = 0.45f;

    [Header("Paseo y paredes")]
    [SerializeField, Min(0.01f)] float _wanderAhead = 1.5f;
    [SerializeField, Min(0.01f)] float _wanderRadius = 1f;
    [SerializeField, Min(0f)] float _wanderJitter = 2f;
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;
    [SerializeField, Min(0.01f)] float _wallLookAhead = 1.2f;

    readonly List<BoidAgent2D> _neighbors = new List<BoidAgent2D>();
    BoidNode _rootNode;
    Steering _motor;
    FoodPickup2D _foodTarget;
    HunterFSM2D _visibleHunter;
    HunterFSM2D _ghostTarget;
    float _wanderAngle;
    bool _alive = true;
    bool _wasHunter;

    public Steering Motor => _motor;
    public float CaptureRadius => _pickupRadius;
    public float TeamCollisionRadius => _teamCollisionRadius;
    public bool IsAlive => _alive && isActiveAndEnabled;
    public bool FoodNearby => _foodTarget != null;
    public bool HunterNearby => _visibleHunter != null;
    public bool NeighborsNearby => _neighbors.Count > 0;
    public bool PacmanIsHunter => _manager != null && _manager.Roles != null &&
        _manager.Roles.PacmanIsHunter;
    public BoidAction CurrentAction { get; private set; }

    void Awake()
    {
        _motor = GetComponent<Steering>();
        _motor.ConfigureWallCollision(_walls, _bodyRadius);
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
        // El prefab no puede guardar una referencia a un nodo de la escena.
        // Buscamos la pregunta raíz una vez, al iniciar cada agente.
        BoidQuestionNode[] questions = FindObjectsByType<BoidQuestionNode>(FindObjectsSortMode.None);
        for (int i = 0; i < questions.Length; i++)
        {
            if (questions[i].QuestionType != BoidQuestionNode.Question.PacmanHunter)
                continue;
            _rootNode = questions[i];
            break;
        }

        if (_rootNode == null)
        {
            Debug.LogError("No se encontró la pregunta raíz PacmanHunter del árbol de decisión en la escena.", this);
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
        if (!_alive || _manager == null || !_manager.IsRunning)
        {
            if (_motor != null) _motor.Stop();
            return;
        }

        //al cambiar de rol se corta la inercia para no seguir con el movimiento anterior.
        bool hunting = PacmanIsHunter;
        if (hunting != _wasHunter)
        {
            _wasHunter = hunting;
            _motor.Stop();
        }

        Perceive();
        _rootNode.Execute(this);
    }

    //actionNode llama este método, igual que las acciones del NPC de clase.
    public void PerformAction(BoidAction action)
    {
        CurrentAction = action;
        Vector2 steering;

        switch (CurrentAction)
        {
            case BoidAction.GoToFood:
                steering = _foodTarget != null
                    ? _motor.Arrive(_foodTarget.transform.position) : Vector2.zero;
                if (_neighbors.Count > 0)
                    steering += Flock() * _flockWhileSeekingWeight;
                break;
            case BoidAction.EvadeHunter:
                steering = _visibleHunter != null
                    ? _motor.Evade(_visibleHunter.Motor) : Vector2.zero;
                break;
            case BoidAction.Flock:
                steering = _neighbors.Count > 0 ? Flock() : Vector2.zero;
                break;
            case BoidAction.PursueGhost:
                steering = _ghostTarget != null
                    ? _motor.Pursuit(_ghostTarget.Motor) : Vector2.zero;
                break;
            default:
                steering = _motor.Wander(ref _wanderAngle,
                    _wanderAhead, _wanderRadius, _wanderJitter);
                break;
        }

        //seguridad local por encima de la acción elegida
        Vector2 avoid = _motor.AvoidObstacles(_walls, _bodyRadius, _wallLookAhead,
            steering);
        if (avoid.sqrMagnitude > 0.0001f) steering = avoid;

        Vector2 beforeMove = _motor.Position;
        _motor.Move(steering);
        transform.position = _manager.ApplyWrapAreas(beforeMove, transform.position);

        if (CurrentAction == BoidAction.GoToFood && _foodTarget != null &&
            Vector2.Distance(_motor.Position, _foodTarget.transform.position)
            <= _pickupRadius)
            _foodTarget.TryConsume(this);
    }

    void Perceive()
    {
        _foodTarget = _manager.FindNearestFood(_motor.Position, _foodSenseRadius);
        if (PacmanIsHunter)
        {
            _visibleHunter = null; //eno huye
            _ghostTarget = _manager.FindNearestHunter(_motor.Position, _huntSenseRadius);
        }
        else
        {
            _visibleHunter = _manager.FindNearestHunter(_motor.Position, _hunterSenseRadius);
            _ghostTarget = null;
        }

        _neighbors.Clear();
        List<BoidAgent2D> all = _manager.Boids;
        for (int i = 0; i < all.Count; i++)
        {
            BoidAgent2D other = all[i];
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
        for (int i = 0; i < _neighbors.Count; i++)
        {
            BoidAgent2D other = _neighbors[i];
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
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _pickupRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _neighborRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _teamCollisionRadius);
    }
}