using UnityEngine;

// La recogida se decide por distancia desde el boid: no necesita Rigidbody2D.
public class FoodPickup2D : MonoBehaviour
{
    [SerializeField, Min(1)] int _points = 1;
    AIGameManager2D _manager;
    bool _consumed;

    public int Points => _points;
    public bool IsAvailable => isActiveAndEnabled && !_consumed;

    void Start()
    {
        _manager = AIGameManager2D.Instance;
        if (_manager == null)
        {
            Debug.LogError("FoodPickup2D necesita AIGameManager2D.", this);
            return;
        }
        _manager.RegisterFood(this);
    }

    void OnDisable()
    {
        if (_manager != null) _manager.UnregisterFood(this);
    }

    public bool TryConsume(BoidAgent2D boid)
    {
        if (!IsAvailable || boid == null || !boid.IsAlive) return false;
        _consumed = true;
        if (_manager != null) _manager.FoodConsumed(this);
        gameObject.SetActive(false);
        return true;
    }
}
