using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] InputActionReference _move;   
    [SerializeField, Min(0f)] float _speed = 5f;
    [SerializeField] LayerMask _walls;
    [SerializeField, Min(0.01f)] float _bodyRadius = 0.3f;

    public LayerMask Walls => _walls;
    public float BodyRadius => _bodyRadius;

    void OnEnable() => _move.action.Enable();
    void OnDisable() => _move.action.Disable();

    void Update()
    {
        Vector2 dir = _move.action.ReadValue<Vector2>().normalized;
        if (dir == Vector2.zero) return;

        transform.up = dir;

        float distance = _speed * Time.deltaTime;
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, _bodyRadius, dir, distance, _walls);
        if (hit.collider != null) distance = Mathf.Max(0f, hit.distance - 0.01f);

        transform.position += (Vector3)(dir * distance);
    }
}