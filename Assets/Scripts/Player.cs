using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float range, speed;
    void Update()
    {
        if (Vector3.Distance(transform.position, target.position) < range)
        {
            Vector3 dir = target.position - transform.position;
            transform.position += dir.normalized * speed * Time.deltaTime;


        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
