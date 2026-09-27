using UnityEngine;

// Motor cinemático compartido por Pacman/boids y el cazador. No requiere Rigidbody2D.
[DisallowMultipleComponent]
public class Steering : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float _maxSpeed = 5f;
    [SerializeField, Min(0.01f)] float _maxForce = 10f;
    [SerializeField, Min(0.01f)] float _arrivalRadius = 1.5f;
    [SerializeField] bool _faceVelocity = true;
    [SerializeField] float _spriteAngleOffset;

    Vector2 _velocity;
    Vector2 _pendingSteering;
    bool _hasPendingSteering;

    public Vector2 Position => transform.position;
    public Vector2 Velocity => _velocity;
    public float MaxSpeed => _maxSpeed;
    public Vector2 Heading => _velocity.sqrMagnitude > 0.0001f
        ? _velocity.normalized
        : (Vector2)transform.up;

    // Los comportamientos devuelven un cambio deseado de velocidad.
    // Move limita la aceleración y se llama una sola vez por fotograma.
    public Vector2 Seek(Vector2 target)
    {
        Vector2 offset = target - Position;
        if (offset.sqrMagnitude < 0.0001f) return -_velocity;
        return offset.normalized * _maxSpeed - _velocity;
    }

    public Vector2 Flee(Vector2 danger)
    {
        Vector2 away = Position - danger;
        if (away.sqrMagnitude < 0.0001f) away = Heading;
        return away.normalized * _maxSpeed - _velocity;
    }

    public Vector2 Arrive(Vector2 target)
    {
        Vector2 offset = target - Position;
        float distance = offset.magnitude;
        if (distance < 0.001f) return -_velocity;

        float speed = _maxSpeed * Mathf.Min(1f, distance / _arrivalRadius);
        return offset / distance * speed - _velocity;
    }

    // Predecir la posición futura y aplicar Seek, como en la clase.
    public Vector2 Pursuit(Steering target, float predictionSeconds = 1f)
    {
        if (target == null) return Vector2.zero;
        return Seek(target.Position + target.Velocity * predictionSeconds);
    }

    // La misma predicción, pero con Flee.
    public Vector2 Evade(Steering threat, float predictionSeconds = 1f)
    {
        if (threat == null) return Vector2.zero;
        return Flee(threat.Position + threat.Velocity * predictionSeconds);
    }

    public Vector2 Wander(ref float angle, float lookAhead, float radius, float jitter)
    {
        angle += Random.Range(-jitter, jitter) * Time.deltaTime;
        Vector2 circleCenter = Position + Heading * lookAhead;
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        return Seek(circleCenter + offset);
    }

    // Dos rayos laterales, igual que el ejemplo de evasión de obstáculos.
    // El LayerMask debe contener SOLO paredes.
    public Vector2 AvoidObstacles(LayerMask walls, float bodyRadius, float lookAhead)
    {
        if (walls.value == 0) return Vector2.zero;
        Vector2 forward = Heading;
        Vector2 left = new Vector2(-forward.y, forward.x);
        RaycastHit2D leftHit = Physics2D.Raycast(Position + left * bodyRadius,
            forward, lookAhead, walls);
        RaycastHit2D rightHit = Physics2D.Raycast(Position - left * bodyRadius,
            forward, lookAhead, walls);
        if (leftHit.collider == null && rightHit.collider == null)
            return Vector2.zero;
        if (leftHit.collider != null && rightHit.collider != null)
            return Flee(Position + forward * lookAhead);
        if (leftHit.collider != null)
            return Seek(Position - left * lookAhead);
        return Seek(Position + left * lookAhead);
    }

    public void Move(Vector2 steering)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector2 deltaVelocity = Vector2.ClampMagnitude(steering,
            _maxForce * dt);
        _velocity = Vector2.ClampMagnitude(_velocity + deltaVelocity, _maxSpeed);
        transform.position += (Vector3)(_velocity * dt);

        if (_faceVelocity && _velocity.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f,
                angle - 90f + _spriteAngleOffset);
        }
    }

    public void Stop()
    {
        _velocity = Vector2.zero;
        _pendingSteering = Vector2.zero;
        _hasPendingSteering = false;
    }

    // Compatibilidad con Agent1, Agent2 y RoleController del prototipo.
    public void AddForce(Vector2 steering)
    {
        _pendingSteering += steering;
        _hasPendingSteering = true;
    }

    public void Move()
    {
        Vector2 steering = _pendingSteering;
        _pendingSteering = Vector2.zero;
        _hasPendingSteering = false;
        Move(steering);
    }

    protected virtual void LateUpdate()
    {
        if (_hasPendingSteering) Move();
    }
}
