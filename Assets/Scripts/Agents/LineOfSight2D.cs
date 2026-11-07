using UnityEngine;

internal static class LineOfSight2D
{
    public static bool CanSee(Vector2 observer, Vector2 center, float targetRadius, LayerMask walls)
    {
        if (walls.value == 0 || Physics2D.Linecast(observer, center, walls).collider == null)
            return true;

        // Una parte del objetivo puede asomar aunque el centro esté detrás de una esquina.
        Vector2 direction = (center - observer).normalized;
        Vector2 side = new Vector2(-direction.y, direction.x) * (targetRadius * 0.8f);
        return Physics2D.Linecast(observer, center + side, walls).collider == null ||
               Physics2D.Linecast(observer, center - side, walls).collider == null;
    }
}
