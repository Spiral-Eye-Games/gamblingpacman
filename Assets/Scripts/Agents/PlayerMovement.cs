using System;
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
    BettingManager _betting;
    Action _readInput;
    Action _fixedStep;
    Action _moveStep;
    Action _holdMovement;
    Action _moveForward;
    Action _animate;
    Action _finishPhysics;
    Action _enterRunning;
    Action _enterStopped;
    Action _connectOnEnable = delegate { };

    //memento de posición y rotación (el propio script es Originator y Caretaker)*
    Vector2 _savedPosition;
    float _savedRotation;

    public LayerMask Walls => _walls;
    public float BodyRadius => _bodyRadius;

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
        _holdMovement = HoldStill;
        _moveForward = MoveForward;
        _readInput = IgnoreInput;
        _fixedStep = HoldStill;
        _moveStep = HoldStill;
        _animate = PauseAnimation;
        _finishPhysics = HoldStill;
        _enterRunning = EnterRunning;
        _enterStopped = delegate { };
    }

    void Start()
    {
        _betting = BettingManager.Instance;
        _connectOnEnable = ConnectStateEvents;
        ConnectStateEvents();
    }

    void ConnectStateEvents()
    {
        if (_betting != null) _betting.StateChanged += SelectMovementState;
        SelectMovementState();
    }

    void SelectMovementState()
    {
        // La condición se evalúa al cambiar la ronda, no en cada Update/FixedUpdate.
        Action enterState = _betting == null || _betting.IsRunning ? _enterRunning : _enterStopped;
        enterState();
    }

    void EnterRunning()
    {
        _enterRunning = delegate { };
        _enterStopped = EnterStopped;
        _readInput = ReadInput;
        _fixedStep = BeginMovement;
        _animate = AnimateMovement;
    }

    void EnterStopped()
    {
        _enterRunning = EnterRunning;
        _enterStopped = delegate { };
        _readInput = IgnoreInput;
        IgnoreInput();
        _fixedStep = _finishPhysics;
        _animate = PauseAnimation;
    }

    void OnEnable()
    {
        if (_move != null) _move.action?.Enable();
        _connectOnEnable();
    }

    void OnDisable()
    {
        if (_move != null) _move.action?.Disable();
        if (_betting != null) _betting.StateChanged -= SelectMovementState;
        _input = Vector2.zero;
        _moveStep = HoldStill;
        if (_body != null) _body.linearVelocity = Vector2.zero;
        PauseAnimation();
    }

    void OnDestroy()
    {
        if (_contactMaterial != null) Destroy(_contactMaterial);
    }

    void Update() => _readInput();

    void ReadInput()
    {
        _input = _move != null ? _move.action?.ReadValue<Vector2>() ?? Vector2.zero : Vector2.zero;
        _moveStep = _input == Vector2.zero ? _holdMovement : _moveForward;
    }

    void IgnoreInput()
    {
        _input = Vector2.zero;
        _moveStep = HoldStill;
    }

    void FixedUpdate() => _fixedStep();

    void BeginMovement()
    {
        SaveSnapshot();
        _fixedStep = ApplyMovement;
        _finishPhysics = RestoreAndStop;
        ApplyMovement();
    }

    void ApplyMovement() => _moveStep();

    void RestoreAndStop()
    {
        RestoreSnapshot();
        _fixedStep = HoldStill;
        _finishPhysics = HoldStill;
    }

    void HoldStill() => _body.linearVelocity = Vector2.zero;

    void MoveForward()
    {
        float target = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg - _spriteForwardAngle;
        float angle = Mathf.MoveTowardsAngle(_body.rotation, target, _turnSpeed * Time.fixedDeltaTime);
        _body.rotation = angle;

        // El jugador pide avanzar de frente. El contacto con paredes lo resuelve el Rigidbody2D.
        float radians = (angle + _spriteForwardAngle) * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        _body.linearVelocity = forward * _speed;
    }

    void LateUpdate() => _animate();

    void AnimateMovement()
    {
        if (_animator != null)
            _animator.speed = _body.linearVelocity.sqrMagnitude > 0.0001f ? 1f : 0f;
    }

    void PauseAnimation()
    {
        if (_animator != null) _animator.speed = 0f;
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
