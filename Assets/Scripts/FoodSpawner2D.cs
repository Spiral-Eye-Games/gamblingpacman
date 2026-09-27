using UnityEngine;

// Opcional: también se pueden colocar FoodPickup2D manualmente en la escena.
public class FoodSpawner2D : MonoBehaviour
{
    [System.Serializable]
    public class SpawnArea
    {
        // Centro relativo a la posición del objeto que tiene FoodSpawner2D.
        [SerializeField] Vector2 _localCenter;
        [SerializeField] Vector2 _size = new Vector2(6f, 2f);
        [SerializeField, Min(0)] int _amount = 1;

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

    void Start()
    {
        if (_foodPrefab == null)
        {
            Debug.LogWarning("FoodSpawner2D necesita un prefab de comida.", this);
            return;
        }

        if (_areas == null) return;

        for (int areaIndex = 0; areaIndex < _areas.Length; areaIndex++)
        {
            SpawnArea area = _areas[areaIndex];
            if (area == null) continue;

            for (int i = 0; i < area.Amount; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    Vector2 offset = new Vector2(
                        Random.Range(-area.Size.x * 0.5f, area.Size.x * 0.5f),
                        Random.Range(-area.Size.y * 0.5f, area.Size.y * 0.5f));
                    Vector2 position = (Vector2)transform.position + area.LocalCenter + offset;
                    if (_walls.value != 0 &&
                        Physics2D.OverlapCircle(position, _clearance, _walls) != null)
                        continue;

                    Instantiate(_foodPrefab, position, Quaternion.identity, transform);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    Debug.LogWarning("No quedó espacio para toda la comida en el área " + (areaIndex + 1) + ".", this);
                    break;
                }
            }
        }
    }

    void OnValidate()
    {
        if (_areas == null) return;

        for (int i = 0; i < _areas.Length; i++)
        {
            if (_areas[i] != null) _areas[i].ClampSize();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (_areas == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < _areas.Length; i++)
        {
            SpawnArea area = _areas[i];
            if (area == null) continue;
            Gizmos.DrawWireCube((Vector2)transform.position + area.LocalCenter, area.Size);
        }
    }
}
