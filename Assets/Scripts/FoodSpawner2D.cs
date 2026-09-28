using UnityEngine;

// Coloca comida en las zonas definidas: la inicial al arrancar y una nueva
// cada _respawnSeconds mientras la ronda está en curso.
// También se pueden colocar FoodPickup2D a mano en la escena.
public class FoodSpawner2D : MonoBehaviour
{
    [System.Serializable]
    public class SpawnArea
    {
        // Centro relativo a la posición del objeto que tiene FoodSpawner2D.
        [SerializeField] Vector2 _localCenter;
        [SerializeField] Vector2 _size = new Vector2(6f, 2f);
        [SerializeField, Min(0)] int _amount = 1; // comida inicial de esta zona

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
    [SerializeField, Min(1)] int _placementAttempts = 50; // intentos de buscar un lugar libre

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

    // El timer solo avanza mientras la ronda está en curso.
    void Update()
    {
        if (_foodPrefab == null || _areas.Length == 0) return;
        if (_manager == null || !_manager.IsRunning) return;

        _timer += Time.deltaTime;
        if (_timer < _respawnSeconds) return;

        _timer = 0f;
        SpawnFood(_areas[Random.Range(0, _areas.Length)]); // una zona al azar
    }

    // Busca un lugar libre dentro de la zona y pone una comida. false si no encontró lugar.
    bool SpawnFood(SpawnArea area)
    {
        for (int attempt = 0; attempt < _placementAttempts; attempt++)
        {
            Vector2 offset = new Vector2(
                Random.Range(-area.Size.x * 0.5f, area.Size.x * 0.5f),
                Random.Range(-area.Size.y * 0.5f, area.Size.y * 0.5f));
            Vector2 position = (Vector2)transform.position + area.LocalCenter + offset;
            if (_walls.value != 0 &&
                Physics2D.OverlapCircle(position, _clearance, _walls) != null)
                continue;

            Instantiate(_foodPrefab, position, Quaternion.identity, transform);
            return true;
        }
        return false;
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