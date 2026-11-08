using System.Collections;
using UnityEngine;

// Recogida de comida y captura de Pac-Man. PlayerMovement controla su movimiento.
[RequireComponent(typeof(Steering), typeof(PlayerMovement))]
[DisallowMultipleComponent]
public class BoidAgent2D : MonoBehaviour
{
    [SerializeField] AIGameManager2D _manager;
    [SerializeField, Min(0.01f)] float _pickupRadius = 0.35f;

    Steering _motor;
    Animator _animator;
    PlayerMovement _movement;
    Rigidbody2D _body;
    bool _alive = true;

    public Steering Motor => _motor;
    public float CaptureRadius => _pickupRadius;
    internal float BodyRadius { get; private set; }
    public bool IsAlive => _alive && isActiveAndEnabled;

    void Awake()
    {
        _motor = GetComponent<Steering>();
        _animator = GetComponent<Animator>();
        _movement = GetComponent<PlayerMovement>();
        _body = GetComponent<Rigidbody2D>();
        BodyRadius = _movement.BodyRadius;
        _motor.ConfigureWallCollision(_movement.Walls, BodyRadius);
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
            if (!IsInPickupRange(food.transform.position)) continue;
            if (food.TryConsume(this)) break;   // corta porque la lista puede cambiar
        }
    }

    internal bool IsInPickupRange(Vector2 point) =>
        (point - (Vector2)transform.position).sqrMagnitude <= _pickupRadius * _pickupRadius;

    public void Caught()
    {
        if (!_alive) return;
        _alive = false;
        _movement.enabled = false;
        _motor.Stop();
        _body.linearVelocity = Vector2.zero;
        _body.simulated = false;
        StartCoroutine(Die());
    }

    IEnumerator Die()
    {
        yield return AgentDeathAnimation2D.Play(_animator);
        // El resultado y la pantalla de continuar se habilitan después de la animación.
        if (_manager != null) _manager.BoidCaught();
        gameObject.SetActive(false);
    }
}
