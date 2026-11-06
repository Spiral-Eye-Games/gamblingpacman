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
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;

    Rigidbody2D _body;
    PhysicsMaterial2D _contactMaterial;
    Vector2 _input;

    public LayerMask Walls => _walls;
    public float BodyRadius => _bodyRadius;

    void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
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
    }

    void OnDestroy()
    {
        if (_contactMaterial != null) Destroy(_contactMaterial);
    }

    void Update()
    {
        _input = _move != null ? _move.action?.ReadValue<Vector2>() ?? Vector2.zero : Vector2.zero;
    }

    void FixedUpdate()
    {
        if (_input == Vector2.zero)
        {
            _body.linearVelocity = Vector2.zero;
            return;
        }

        float target = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg - 90f;
        float angle = Mathf.MoveTowardsAngle(_body.rotation, target, _turnSpeed * Time.fixedDeltaTime);
        _body.rotation = angle;

        // El jugador pide avanzar de frente. El contacto con paredes lo resuelve el Rigidbody2D.
        float radians = angle * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
        _body.linearVelocity = forward * _speed;
    }
}
