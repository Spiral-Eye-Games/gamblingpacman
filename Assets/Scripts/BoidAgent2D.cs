using UnityEngine;

// Conserva el componente del prefab de Pac-Man. La ruta determina su movimiento.
[RequireComponent(typeof(Steering))]
[DisallowMultipleComponent]
public class BoidAgent2D : MonoBehaviour
{
    [SerializeField] AIGameManager2D _manager;
    [SerializeField, Min(0.1f)] float _foodSenseRadius = 3.11f;
    [SerializeField, Min(0.1f)] float _huntSenseRadius = 20f;
    [SerializeField, Min(0.01f)] float _pickupRadius = 0.35f;
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;

    Steering _motor;
    AStarRoute2D _route;
    FoodPickup2D _foodTarget;
    int _patrolIndex;
    bool _alive = true;
    bool _wasHunter;

    public Steering Motor => _motor;
    public float CaptureRadius => _pickupRadius;
    public bool IsAlive => _alive && isActiveAndEnabled;

    void Awake()
    {
        _motor = GetComponent<Steering>();
        _motor.ConfigureWallCollision(_walls, _bodyRadius);
        _route = new AStarRoute2D(_motor, _walls, _bodyRadius);
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

        _manager.RegisterBoid(this);
    }

    void OnDisable()
    {
        _route?.Clear();
        if (_motor != null) _motor.Stop();
        if (_manager != null) _manager.UnregisterBoid(this);
    }

    void Update()
    {
        if (!_alive || _manager == null || !_manager.IsRunning)
        {
            _motor.Stop();
            return;
        }

        bool hunting = _manager.Roles.PacmanIsHunter;
        if (hunting != _wasHunter || (_foodTarget != null && !_foodTarget.IsAvailable))
        {
            _wasHunter = hunting;
            _foodTarget = null;
            _route.Clear();
        }

        if (_route.CanReplan)
        {
            _route.BeginPlan();
            if (hunting)
            {
                HunterFSM2D ghost = _manager.FindNearestHunter(_motor.Position, _huntSenseRadius);
                if (ghost == null || !_route.TryPlan(_manager, ghost.Motor.Position))
                    _route.PlanPatrolNodes(_manager, ref _patrolIndex);
            }
            else
            {
                PlanToFood();
                if (_foodTarget == null)
                    _route.PlanPatrolNodes(_manager, ref _patrolIndex);
            }
        }

        _route.Move();

        if (_foodTarget != null &&
            Vector2.Distance(_motor.Position, _foodTarget.transform.position) <= _pickupRadius &&
            _foodTarget.TryConsume(this))
        {
            _foodTarget = null;
            _route.Clear();
        }
    }

    void PlanToFood()
    {
        _foodTarget = null;
        float bestDistanceSqr = _foodSenseRadius * _foodSenseRadius;
        foreach (FoodPickup2D food in _manager.Food)
        {
            if (food == null || !food.IsAvailable) continue;

            Vector2 destination = food.transform.position;
            float distanceSqr = (destination - _motor.Position).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr || !_route.TryPlan(_manager, destination))
                continue;

            _foodTarget = food;
            bestDistanceSqr = distanceSqr;
        }
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
        _route?.Draw(Color.yellow);
    }
}
