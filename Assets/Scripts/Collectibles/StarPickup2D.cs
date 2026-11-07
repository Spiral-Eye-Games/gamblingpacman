using UnityEngine;

[DisallowMultipleComponent]
public sealed class StarPickup2D : MonoBehaviour
{
    internal bool TryCollect(BoidAgent2D player)
    {
        if (!isActiveAndEnabled || !player.IsInPickupRange(transform.position))
            return false;

        gameObject.SetActive(false);
        return true;
    }
}
