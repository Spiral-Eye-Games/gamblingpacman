using UnityEngine;

// Opcional: también se pueden colocar FoodPickup2D manualmente en la escena.
public class FoodSpawner2D : MonoBehaviour
{
    [SerializeField] FoodPickup2D _foodPrefab;
    [SerializeField, Min(0)] int _amount = 20;
    [SerializeField] Vector2 _areaSize = new Vector2(12f, 7f);
    [SerializeField, Min(0.01f)] float _clearance = 0.35f;
    [SerializeField] LayerMask _walls;

    void Start()
    {
        if (_foodPrefab == null)
        {
            Debug.LogWarning("FoodSpawner2D necesita un prefab de comida.", this);
            return;
        }

        for (int i = 0; i < _amount; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                Vector2 offset = new Vector2(
                    Random.Range(-_areaSize.x * 0.5f, _areaSize.x * 0.5f),
                    Random.Range(-_areaSize.y * 0.5f, _areaSize.y * 0.5f));
                Vector2 position = (Vector2)transform.position + offset;
                if (_walls.value != 0 &&
                    Physics2D.OverlapCircle(position, _clearance, _walls) != null)
                    continue;

                Instantiate(_foodPrefab, position, Quaternion.identity, transform);
                placed = true;
                break;
            }

            if (!placed)
            {
                Debug.LogWarning("No quedó espacio para toda la comida.", this);
                break;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, _areaSize);
    }
}
