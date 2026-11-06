using UnityEngine;

// Comportamiento de Pac-Man: come caramelitos y es cazable. El movimiento está en PlayerMovement2D.
[RequireComponent(typeof(Steering), typeof(PlayerMovement))]
[DisallowMultipleComponent]
public class BoidAgent2D : MonoBehaviour
{
    [SerializeField] AIGameManager2D _manager;
    [SerializeField, Min(0.01f)] float _pickupRadius = 0.35f;

    Steering _motor;
    bool _alive = true;

    public Steering Motor => _motor;
    public float CaptureRadius => _pickupRadius;
    public bool IsAlive => _alive && isActiveAndEnabled;

    void Awake()
    {
        _motor = GetComponent<Steering>();
        var movement = GetComponent<PlayerMovement>();
        _motor.ConfigureWallCollision(movement.Walls, movement.BodyRadius);
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
        if (_manager != null) _manager.UnregisterBoid(this);
    }

    void Update()
    {
        if (!_alive) return;

        foreach (FoodPickup2D food in _manager.Food)
        {
            if (food == null || !food.IsAvailable) continue;
            if (Vector2.Distance(food.transform.position, transform.position) > _pickupRadius) continue;
            if (food.TryConsume(this)) break;   // corta porque la lista puede cambiar
        }
    }

    public void Caught()
    {
        if (!_alive) return;
        _alive = false;
        _manager.BoidCaught();
        gameObject.SetActive(false);
    }
}