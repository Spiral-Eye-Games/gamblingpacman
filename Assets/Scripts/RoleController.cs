using UnityEngine;

//Va sobre cada fantasma (y opcionalmente sobre Pacman también,
//si queremos que también pueda ser "presa" en algún momento).
//Decide cada frame si el objeto debe perseguir o huir del target,
//y le pasa esa fuerza al Steering2D.
public class RoleController : MonoBehaviour
{
    public enum Role { Hunter, Prey }

    [SerializeField] Steering2D _steering;
    [SerializeField] Transform _target;
    [SerializeField] Role _startingRole = Role.Prey;
    [SerializeField] float _roleDuration = 8f; // segundos antes de invertir, ajustable
    [SerializeField] bool _autoSwapRoles = true;

    Role _currentRole;
    float _timer;
    
    public Role CurrentRole => _currentRole;

    void Start()
    {
        _currentRole = _startingRole;
        _timer = _roleDuration;
    }

    void Update()
    {
        if (_autoSwapRoles)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                SwapRole();
                _timer = _roleDuration;
            }
        }

        Vector2 force = _currentRole == Role.Hunter
            ? _steering.Seek(_target.position)
            : _steering.Flee(_target.position);

        _steering.AddForce(force);
    }

    public void SwapRole()
    {
        _currentRole = _currentRole == Role.Hunter ? Role.Prey : Role.Hunter;
        // Acá podés disparar un evento para cambiar de sprite/color
        // cuando el fantasma pasa a ser vulnerable, por ejemplo.
    }

    public void SetRole(Role role)
    {
        _currentRole = role;
        _timer = _roleDuration;
    }
}