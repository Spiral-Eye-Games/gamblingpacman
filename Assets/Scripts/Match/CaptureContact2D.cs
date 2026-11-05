using UnityEngine;

internal static class CaptureContact2D
{
    const float MinRelativeMoveSqr = 0.000001f;
    const float TeleportMargin = 0.5f;

    public static bool PathsTouch(IMovementTrace2D first, IMovementTrace2D second,
        float radius, float deltaTime)
    {
        Vector2 firstEnd = first.Position;
        Vector2 secondEnd = second.Position;
        float radiusSqr = radius * radius;

        if ((firstEnd - secondEnd).sqrMagnitude <= radiusSqr) return true;

        // Distancia mínima entre agentes durante el mismo frame.
        Vector2 firstStart = FrameStart(first, deltaTime);
        Vector2 secondStart = FrameStart(second, deltaTime);
        Vector2 relativeStart = firstStart - secondStart;
        Vector2 relativeMove = (firstEnd - firstStart) - (secondEnd - secondStart);
        float moveSqr = relativeMove.sqrMagnitude;
        float t = moveSqr > MinRelativeMoveSqr
            ? Mathf.Clamp01(-Vector2.Dot(relativeStart, relativeMove) / moveSqr)
            : 0f;
        return (relativeStart + relativeMove * t).sqrMagnitude <= radiusSqr;
    }

    static Vector2 FrameStart(IMovementTrace2D agent, float deltaTime)
    {
        Vector2 end = agent.Position;
        Vector2 start = agent.FrameStartPosition;
        float maxStep = agent.MaxSpeed * deltaTime + TeleportMargin;

        // Un salto mayor que el movimiento posible se trata como teletransporte.
        return (end - start).sqrMagnitude > maxStep * maxStep ? end : start;
    }
}
