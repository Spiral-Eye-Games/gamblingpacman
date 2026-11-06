using UnityEngine;
using UnityEngine.InputSystem;

// Movimiento WASD con vista cenital. Gira de forma progresiva hacia donde apuntás y avanza en la dirección del frente (eje Y del sprite).
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] InputActionReference _move;    // Player/Move (WASD)
    [SerializeField, Min(0f)] float _speed = 5f;
    [SerializeField, Min(1f)] float _turnSpeed = 270f;  // grados por segundo: más bajo = giro más suave
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;

    public LayerMask Walls => _walls;
    public float BodyRadius => _bodyRadius;

    void OnEnable() => _move.action.Enable();
    void OnDisable() => _move.action.Disable();

    void Update()
    {
        Vector2 input = _move.action.ReadValue<Vector2>();
        if (input == Vector2.zero) return;

        // Girar de a poco hacia la dirección deseada
        float target = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg - 90f;
        float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, target, _turnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Avanzar hacia donde mira, no hacia donde apuntás
        Vector2 dir = transform.up;
        float distance = _speed * Time.deltaTime;
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, _bodyRadius, dir, distance, _walls);
        if (hit.collider != null) distance = Mathf.Max(0f, hit.distance - 0.01f);

        transform.position += (Vector3)(dir * distance);
    }
}