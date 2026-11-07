using UnityEngine;

// Motor cinemático compartido por Pac-Man y los fantasmas.
[DisallowMultipleComponent]
public class Steering : MonoBehaviour, IAgentMotor, IMovementTrace2D
{
    [SerializeField, Min(0.01f)] float _maxSpeed = 5f;
    [SerializeField] bool _faceVelocity = true;
    [SerializeField] float _spriteAngleOffset;

    LayerMask _walls;
    float _radius;
    float _boostMultiplier = 1f;
    float _boostUntil;

    public Vector2 Position => transform.position;
    public Vector2 FrameStartPosition { get; private set; }
    public Vector2 Velocity { get; private set; }
    public virtual float MaxSpeed => Time.time < _boostUntil
        ? _maxSpeed * _boostMultiplier : _maxSpeed;

    public void ConfigureWallCollision(LayerMask walls, float bodyRadius)
    {
        _walls = walls;
        _radius = bodyRadius;
        FrameStartPosition = Position;
    }

    public bool MoveToWaypoint(Vector2 waypoint)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return true;

        FrameStartPosition = Position;
        if (Physics2D.OverlapCircle(FrameStartPosition, _radius, _walls) != null ||
            !AStarPathfinding2D.SegmentIsClear(FrameStartPosition, waypoint, _radius, _walls))
        {
            Stop();
            return false;
        }

        Vector2 next = Vector2.MoveTowards(FrameStartPosition, waypoint, MaxSpeed * dt);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        Velocity = (next - FrameStartPosition) / dt;

        if (_faceVelocity && Velocity.sqrMagnitude > 0.0001f)
            FaceVelocity();
        return true;
    }

    protected virtual void FaceVelocity()
    {
        float angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f + _spriteAngleOffset);
    }

    public void Stop()
    {
        Velocity = Vector2.zero;
        FrameStartPosition = Position;
    }

    public void MarkFrameEnd()
    {
        FrameStartPosition = Position;
    }

    public void BoostSpeed(float multiplier, float seconds)
    {
        _boostMultiplier = Mathf.Max(1f, multiplier);
        _boostUntil = Time.time + Mathf.Max(0f, seconds);
    }

    // El controlador de roles del prototipo todavía consulta estas fuerzas.
    public Vector2 Seek(Vector2 target)
    {
        return (target - Position).normalized * MaxSpeed - Velocity;
    }

    public Vector2 Flee(Vector2 danger)
    {
        Vector2 direction = Position - danger;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Velocity.sqrMagnitude > 0.0001f ? Velocity : (Vector2)transform.up;
        return direction.normalized * MaxSpeed - Velocity;
    }
}
