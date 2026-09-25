using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class Steering2D : MonoBehaviour
{
    [SerializeField] Transform _target;

    [SerializeField] float _maxSpeed = 5f;
    [SerializeField] float _maxForce = 10f;

    Vector2 _velocity;

    public Vector2 Velocity => _velocity;

    void Update()
    {
        AddForce(Seek(_target.position));
        transform.position += (Vector3)(_velocity * Time.deltaTime);

        //Equivalente 2D de transform.forward = _velocity
        //transform.up (o transform.right, según cómo mires tu sprite)
        //no depende de un "up" implícito como forward, por eso no flipea
        transform.up = _velocity;
    }

    public Vector2 Seek(Vector2 target)
    {
        //desired tiene que tener el tamaño de velocity
        Vector2 desiredVelocity = (target - (Vector2)transform.position).normalized * _maxSpeed;

        Vector2 steering = desiredVelocity - _velocity;
        steering = Vector2.ClampMagnitude(steering, _maxForce * Time.deltaTime);

        return steering;
    }

    public Vector2 Flee(Vector2 targetPos)
    {
        //Flee es Seek invertido, en vez de ir hacia el target, se aleja
        Vector2 desired = ((Vector2)transform.position - targetPos);
        desired = desired.normalized * _maxSpeed;
        return desired;
    }

    //Recorta el resultado al maxSpeed, la fuerza nunca es mayor que maxSpeed
    public void AddForce(Vector2 force)
    {
        _velocity = Vector2.ClampMagnitude(_velocity + force, _maxSpeed);
    }

    /*
    [SerializeField] float range;
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);
    }
    */

    /*
     [SerializeField] float _rotationSpeed = 720f; // grados/seg, para el giro "limpio"
     // Giro suave hacia donde vas, en vez de rotar instantaneo
    void RotateTowardsVelocity()
    {
        if (_velocity.sqrMagnitude < 0.0001f) return;

        float targetAngle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
    }
    */
}