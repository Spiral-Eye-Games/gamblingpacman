using UnityEngine;

internal static class CaptureContact2D
{
    const float MinRelativeMoveSqr = 0.000001f;

    public static bool PathsTouch(IMovementTrace2D first, IMovementTrace2D second,
        float radius)
    {
        Vector2 firstEnd = first.Position;
        Vector2 secondEnd = second.Position;
        float radiusSqr = radius * radius;

        if ((firstEnd - secondEnd).sqrMagnitude <= radiusSqr) return true;

        // Distancia mínima entre agentes durante el mismo frame.
        Vector2 firstStart = first.FrameStartPosition;
        Vector2 secondStart = second.FrameStartPosition;
        Vector2 relativeStart = firstStart - secondStart;
        Vector2 relativeMove = (firstEnd - firstStart) - (secondEnd - secondStart);
        float moveSqr = relativeMove.sqrMagnitude;
        float t = moveSqr > MinRelativeMoveSqr
            ? Mathf.Clamp01(-Vector2.Dot(relativeStart, relativeMove) / moveSqr)
            : 0f;
        return (relativeStart + relativeMove * t).sqrMagnitude <= radiusSqr;
    }
}
