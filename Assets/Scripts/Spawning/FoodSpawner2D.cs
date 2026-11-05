using UnityEngine;

// Coloca comida inicial y genera otra a intervalos durante la ronda.
public class FoodSpawner2D : MonoBehaviour
{
    [System.Serializable]
    public class SpawnArea
    {
        // Centro relativo a la posición del objeto que tiene foodspawner2d
        [SerializeField] Vector2 _localCenter;
        [SerializeField] Vector2 _size = new Vector2(6f, 2f);
        [SerializeField, Min(0)] int _amount = 1; //comida inicial de la zona

        public Vector2 LocalCenter => _localCenter;
        public Vector2 Size => _size;
        public int Amount => _amount;

        public void ClampSize()
        {
            _size.x = Mathf.Max(0.01f, _size.x);
            _size.y = Mathf.Max(0.01f, _size.y);
        }
    }

    [SerializeField] FoodPickup2D _foodPrefab;
    [SerializeField] SpawnArea[] _areas = { new SpawnArea() };
    [SerializeField, Min(0.01f)] float _clearance = 0.35f;
    [SerializeField] LayerMask _walls;

    [Header("Comida durante la ronda")]
    [SerializeField, Min(0.1f)] float _respawnSeconds = 6f;
    [SerializeField, Min(1)] int _placementAttempts = 50; //intentos de buscar un lugar libre

    AIGameManager2D _manager;
    float _timer;

    void Start()
    {
        _manager = AIGameManager2D.Instance;

        if (_foodPrefab == null)
        {
            Debug.LogWarning("FoodSpawner2D necesita un prefab de comida.", this);
            return;
        }

        for (int areaIndex = 0; areaIndex < _areas.Length; areaIndex++)
        {
            SpawnArea area = _areas[areaIndex];
            for (int i = 0; i < area.Amount; i++)
            {
                if (SpawnFood(area)) continue;
                Debug.LogWarning("No quedó espacio para toda la comida en el área " + (areaIndex + 1) + ".", this);
                break;
            }
        }
    }

    //el timer solo avanza mientras la ronda está en curso.
    void Update()
    {
        if (_foodPrefab == null || _areas.Length == 0) return;
        if (_manager == null || !_manager.IsRunning) return;

        _timer += Time.deltaTime;
        if (_timer < _respawnSeconds) return;

        _timer = 0f;
        SpawnFood(_areas[Random.Range(0, _areas.Length)]); //una zona al azar
    }

    //busca un lugar libre dentro de la zona y pone una comida. false si no encontró lugar.
    bool SpawnFood(SpawnArea area)
    {
        var placement = new SpawnPlacement2D(_manager, _walls, _clearance, _placementAttempts);
        Vector2 center = (Vector2)transform.position + area.LocalCenter;
        if (!placement.TryFindPosition(center, area.Size, out Vector2 position)) return false;

        Instantiate(_foodPrefab, position, Quaternion.identity, transform);
        return true;
    }

    void OnValidate()
    {
        foreach (SpawnArea area in _areas) area.ClampSize();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        foreach (SpawnArea area in _areas)
            Gizmos.DrawWireCube((Vector2)transform.position + area.LocalCenter, area.Size);
    }
}
