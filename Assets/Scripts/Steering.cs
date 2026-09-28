using UnityEngine;

//Motor cinemático compartido por hunter/boids y los fantasmas
//No usa rigidbody2d
[DisallowMultipleComponent]
public class Steering : MonoBehaviour
{
    const float MinSpeedSqr = 0.0001f;   //prácticamente detenido
    const float MinDistance = 0.001f;    //prácticamente en el mismo punto
    const float WallSkin = 0.01f;        //margen para no rozar la pared
    const int WallSlidePasses = 2;       //1 pasada frena, la 2da desliza

    const int AvoidDirections = 8;       //prueba una vuelta completa en pasos de 45 grados

    const float AvoidCommitSeconds = 0.6f; //evita cambiar de lado en cada frame

    const float StuckSeconds = 0.3f;

    const int OverlapRepairPasses = 6;

    [SerializeField, Min(0.01f)] float _maxSpeed = 5f;
    [SerializeField, Min(0.01f)] float _maxForce = 10f;
    [SerializeField, Min(0.01f)] float _arrivalRadius = 1.5f;
    [SerializeField] bool _faceVelocity = true;
    [SerializeField] float _spriteAngleOffset;

    Vector2 _velocity;
    Vector2 _pendingSteering;
    bool _hasPendingSteering;

    float _boostMultiplier = 1f;
    float _boostUntil;

    LayerMask _collisionWalls;
    float _collisionRadius;
    Vector2 _avoidDirection;
    float _avoidUntil;
    float _blockedSeconds;

    public Vector2 Position => transform.position;
    public Vector2 FrameStartPosition { get; private set; }
    public Vector2 Velocity => _velocity;
    public Vector2 Heading => _velocity.sqrMagnitude > MinSpeedSqr
        ? _velocity.normalized
        : (Vector2)transform.up;

    public float MaxSpeed => Time.time < _boostUntil
        ? _maxSpeed * _boostMultiplier
        : _maxSpeed;

    public void ConfigureWallCollision(LayerMask walls, float bodyRadius)
    {
        _collisionWalls = walls;
        _collisionRadius = Mathf.Max(0.01f, bodyRadius);
        FrameStartPosition = Position;
    }

    // --- Comportamientos: cada uno devuelve el cambio de velocidad deseado ---

    public Vector2 Seek(Vector2 target)
    {
        Vector2 offset = target - Position;
        if (offset.sqrMagnitude < MinSpeedSqr) return -_velocity;
        return offset.normalized * MaxSpeed - _velocity;
    }

    public Vector2 Flee(Vector2 danger)
    {
        Vector2 away = Position - danger;
        if (away.sqrMagnitude < MinSpeedSqr) away = Heading;
        return away.normalized * MaxSpeed - _velocity;
    }

    public Vector2 Arrive(Vector2 target)
    {
        Vector2 offset = target - Position;
        float distance = offset.magnitude;
        if (distance < MinDistance) return -_velocity;

        //recortamos el resultado del vector al maxspeed, la fuerza nunca seria mayor que maxspeed
        float speed = MaxSpeed * Mathf.Min(1f, distance / _arrivalRadius);
        return offset / distance * speed - _velocity;
    }

    public Vector2 Pursuit(Steering target, float predictionSeconds = 1f)
    {
        if (target == null) return Vector2.zero;
        return Seek(target.Position + target.Velocity * predictionSeconds);
    }

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

    // Busca una salida libre alrededor del agente cuando su movimiento choca con una pared.
    // El LayerMask debe contener SOLO paredes.
    public Vector2 AvoidObstacles(LayerMask walls, float bodyRadius, float lookAhead,
        Vector2 steering)
    {
        if (walls.value == 0) return Vector2.zero;

        float radius = Mathf.Max(0.01f, bodyRadius);
        float distance = Mathf.Max(0.01f, lookAhead);
        Vector2 nextVelocity = _velocity +
            Vector2.ClampMagnitude(steering, _maxForce * Time.deltaTime);
        if (nextVelocity.sqrMagnitude < MinSpeedSqr) return Vector2.zero;
        Vector2 desired = nextVelocity.normalized;

        // Seguimos un momento por la salida elegida para no oscilar en una esquina.
        if (Time.time < _avoidUntil && _avoidDirection.sqrMagnitude > MinSpeedSqr)
        {
            RaycastHit2D committedHit = Physics2D.CircleCast(Position, radius,
                _avoidDirection, distance * 0.4f, walls);
            if (committedHit.collider == null)
                return _avoidDirection * MaxSpeed - _velocity;
        }

        RaycastHit2D forwardHit = Physics2D.CircleCast(Position, radius,
            desired, distance, walls);
        if (forwardHit.collider == null)
        {
            _avoidUntil = 0f;
            return Vector2.zero;
        }

        Vector2 bestDirection = Vector2.zero;
        float bestScore = float.NegativeInfinity;
        float bestClearance = 0f;

        for (int i = 0; i < AvoidDirections; i++)
        {
            float angle = i * Mathf.PI * 2f / AvoidDirections;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            Vector2 candidate = new Vector2(
                desired.x * cosine - desired.y * sine,
                desired.x * sine + desired.y * cosine);
            RaycastHit2D hit = Physics2D.CircleCast(Position, radius,
                candidate, distance, walls);
            float clearance = hit.collider == null ? distance : hit.distance;
            float score = clearance +
                Vector2.Dot(candidate, desired) * distance * 0.15f +
                Vector2.Dot(candidate, _avoidDirection) * distance * 0.1f;
            if (score <= bestScore) continue;
            bestScore = score;
            bestClearance = clearance;
            bestDirection = candidate;
        }

        // Si ya está rozando dos paredes, la normal del choque indica cómo salir.
        if (bestClearance < WallSkin * 2f && forwardHit.normal.sqrMagnitude > MinSpeedSqr)
            bestDirection = forwardHit.normal;

        _avoidDirection = bestDirection.normalized;
        _avoidUntil = Time.time + AvoidCommitSeconds;
        return _avoidDirection * MaxSpeed - _velocity;
    }

    // --- Movimiento ---

    public void Move(Vector2 steering)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        FrameStartPosition = Position;
        Vector2 deltaVelocity = Vector2.ClampMagnitude(steering, _maxForce * dt);
        _velocity = Vector2.ClampMagnitude(_velocity + deltaVelocity, MaxSpeed);
        Vector2 intendedMovement = _velocity * dt;

        // Sin Rigidbody2D: el cast de MoveWithWalls es lo que impide atravesar paredes.
        Vector2 movement = MoveWithWalls(intendedMovement);
        _velocity = Vector2.ClampMagnitude(movement / dt, MaxSpeed);

        // Si queda presionado contra una esquina, retrocede un momento para salir.
        if (intendedMovement.sqrMagnitude > 0.0001f &&
            movement.sqrMagnitude < intendedMovement.sqrMagnitude * 0.04f)
            _blockedSeconds += dt;
        else
            _blockedSeconds = 0f;

        if (_blockedSeconds >= StuckSeconds && _collisionWalls.value != 0)
        {
            Vector2 retreat = -intendedMovement.normalized;
            RaycastHit2D hit = Physics2D.CircleCast(Position, _collisionRadius,
                retreat, _collisionRadius * 2f, _collisionWalls);
            if (hit.collider == null)
            {
                _avoidDirection = retreat;
                _avoidUntil = Time.time + AvoidCommitSeconds * 2f;
            }
            _velocity = Vector2.zero;
            _blockedSeconds = 0f;
        }

        FaceVelocity();
    }

    void FaceVelocity()
    {
        if (!_faceVelocity || _velocity.sqrMagnitude < MinSpeedSqr) return;
        float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f + _spriteAngleOffset);
    }

    public void Stop()
    {
        _velocity = Vector2.zero;
        FrameStartPosition = Position;
        _pendingSteering = Vector2.zero;
        _hasPendingSteering = false;
        _avoidDirection = Vector2.zero;
        _avoidUntil = 0f;
        _blockedSeconds = 0f;
    }

    public void MarkFrameEnd()
    {
        FrameStartPosition = Position;
    }

    // Separa dos agentes que se solapan, sin Rigidbody2D.
    public void PushOut(Vector2 displacement)
    {
        MoveWithWalls(displacement); // tampoco debe empujar hacia una pared
        if (displacement.sqrMagnitude < MinSpeedSqr) return;

        Vector2 direction = displacement.normalized;
        float inwardSpeed = Vector2.Dot(_velocity, direction);
        if (inwardSpeed < 0f) _velocity -= direction * inwardSpeed;
    }

    // Mueve el objeto frenando o deslizando contra las paredes detectadas.
    Vector2 MoveWithWalls(Vector2 displacement)
    {
        Vector2 start = Position;
        Vector2 position = RepairWallOverlap(start);
        Vector2 remaining = displacement;

        for (int pass = 0; pass < WallSlidePasses; pass++)
        {
            float distance = remaining.magnitude;
            if (distance < 0.00001f) break;

            if (_collisionWalls.value == 0)
            {
                position += remaining;
                break;
            }

            Vector2 direction = remaining / distance;
            RaycastHit2D hit = Physics2D.CircleCast(position, _collisionRadius,
                direction, distance + WallSkin, _collisionWalls);

            if (hit.collider == null)
            {
                position += remaining;
                break;
            }

            float allowed = Mathf.Clamp(hit.distance - WallSkin, 0f, distance);
            position += direction * allowed;
            remaining -= direction * allowed;

            float intoWall = Vector2.Dot(remaining, hit.normal);
            if (intoWall < 0f) remaining -= hit.normal * intoWall;
        }

        transform.position = new Vector3(position.x, position.y, transform.position.z);
        return position - start;
    }

    // Si el círculo empezó ligeramente dentro de una pared, un CircleCast normal
    // informa choque a distancia cero en todas las direcciones y no puede salir.
    Vector2 RepairWallOverlap(Vector2 position)
    {
        if (_collisionWalls.value == 0) return position;

        for (int pass = 0; pass < OverlapRepairPasses; pass++)
        {
            Collider2D wall = Physics2D.OverlapCircle(position, _collisionRadius,
                _collisionWalls);
            if (wall == null) break;

            Vector2 closest = wall.ClosestPoint(position);
            Vector2 away = position - closest;
            float separation = away.magnitude;
            if (separation > 0.0001f)
            {
                position += away / separation *
                    (_collisionRadius + WallSkin - separation);
                continue;
            }

            // Centro dentro del collider: escoger la cara más cercana.
            Bounds bounds = wall.bounds;
            float left = position.x - bounds.min.x;
            float right = bounds.max.x - position.x;
            float bottom = position.y - bounds.min.y;
            float top = bounds.max.y - position.y;
            float shortest = Mathf.Min(left, right, bottom, top);
            if (shortest == left)
                position.x -= left + _collisionRadius + WallSkin;
            else if (shortest == right)
                position.x += right + _collisionRadius + WallSkin;
            else if (shortest == bottom)
                position.y -= bottom + _collisionRadius + WallSkin;
            else
                position.y += top + _collisionRadius + WallSkin;
        }
        return position;
    }

    // Boost temporal comprado con la moneda de la apuesta. Se apaga solo al vencer el tiempo.
    public void BoostSpeed(float multiplier, float seconds)
    {
        _boostMultiplier = Mathf.Max(1f, multiplier);
        _boostUntil = Time.time + Mathf.Max(0f, seconds);
    }

    //Compatibilidad con el estilo de las pruebas iniciales (Seek + AddForce).
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
