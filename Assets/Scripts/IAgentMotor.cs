using UnityEngine;

// Lo mínimo que el manager y los comportamientos necesitan de cualquier motor.
public interface IAgentMotor
{
    Vector2 Position { get; }
    Vector2 FrameStartPosition { get; }
    Vector2 Velocity { get; }
    float MaxSpeed { get; }
    void PushOut(Vector2 displacement);
    void MarkFrameEnd();
}