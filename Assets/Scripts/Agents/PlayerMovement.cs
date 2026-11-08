using UnityEngine;
using UnityEngine.InputSystem;

// Movimiento WASD con giro progresivo y contacto resuelto por la física 2D.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] InputActionReference _move;
    [SerializeField, Min(0f)] float _speed = 5f;
    [SerializeField, Min(1f)] float _turnSpeed = 270f;
    [SerializeField, Tooltip("Dirección del frente del sprite con rotación cero: 0 = derecha, 90 = arriba.")]
    float _spriteForwardAngle = 90f;
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;

    Rigidbody2D _body;
    Animator _animator;
    PhysicsMaterial2D _contactMaterial;
    Vector2 _input;

    //memento de posición y rotación (el propio script es Originator y Caretaker)*
    Vector2 _savedPosition;
    float _savedRotation;
    bool _wasRunning;

    public LayerMask Walls => _walls;
    public float BodyRadius => _bodyRadius;

    //se mueve solo mientras la ronda corre (o si no hay BettingManager en la escena)*
    bool CanMove => BettingManager.Instance == null || BettingManager.Instance.IsRunning;

    void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _body.bodyType = RigidbodyType2D.Dynamic;
        _body.gravityScale = 0f;
        _body.freezeRotation = true;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _body.excludeLayers = ~_walls.value;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        Vector3 scale = transform.lossyScale;
        float worldScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        circle.radius = _bodyRadius / Mathf.Max(worldScale, 0.0001f);
        circle.isTrigger = false;
        _contactMaterial = new PhysicsMaterial2D("Player Wall Contact")
        {
            friction = 0f,
            bounciness = 0f
        };
        circle.sharedMaterial = _contactMaterial;
    }

    void OnEnable()
    {
        if (_move != null) _move.action?.Enable();
    }

    void OnDisable()
    {
        if (_move != null) _move.action?.Disable();
        _input = Vector2.zero;
        if (_body != null) _body.linearVelocity = Vector2.zero;
        if (_animator != null) _animator.speed = 0f;
    }

    void OnDestroy()
    {
        if (_contactMaterial != null) Destroy(_contactMaterial);
    }

    void Update()
    {
        //sin ronda en curso, el input se trata como cero*
        if (!CanMove)
        {
            _input = Vector2.zero;
            return;
        }

        _input = _move != null ? _move.action?.ReadValue<Vector2>() ?? Vector2.zero : Vector2.zero;
    }

    void FixedUpdate()
    {
        //detecta el inicio y el fin de la ronda*
        bool running = CanMove;
        if (running && !_wasRunning) SaveSnapshot();
        else if (!running && _wasRunning) RestoreSnapshot();
        _wasRunning = running;

        //frenado mientras la ronda no corre*
        if (!running)
        {
            _body.linearVelocity = Vector2.zero;
            return;
        }

        if (_input == Vector2.zero)
        {
            _body.linearVelocity = Vector2.zero;
            return;
        }

        float target = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg - _spriteForwardAngle;
        float angle = Mathf.MoveTowardsAngle(_body.rotation, target, _turnSpeed * Time.fixedDeltaTime);
        _body.rotation = angle;

        // El jugador pide avanzar de frente. El contacto con paredes lo resuelve el Rigidbody2D.
        float radians = (angle + _spriteForwardAngle) * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        _body.linearVelocity = forward * _speed;
    }

    void LateUpdate()
    {
        if (_animator != null)
            _animator.speed = CanMove && _body.linearVelocity.sqrMagnitude > 0.0001f ? 1f : 0f;
    }

    //memento (foto de posición y rotación)*
    void SaveSnapshot()
    {
        _savedPosition = _body.position;
        _savedRotation = _body.rotation;
    }

    void RestoreSnapshot()
    {
        _body.linearVelocity = Vector2.zero;
        _body.position = _savedPosition;
        _body.rotation = _savedRotation;
        _input = Vector2.zero;
    }
}
