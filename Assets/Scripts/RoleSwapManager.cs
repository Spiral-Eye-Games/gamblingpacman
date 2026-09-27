using UnityEngine;

// Una vez reunida la comida necesaria, todos los boids pasan a cazar.
public class RoleSwapManager : MonoBehaviour
{
    [SerializeField, Min(1)] int _foodNeeded = 2;

    public int FoodNeeded => _foodNeeded;
    public int FoodEaten { get; private set; }
    public bool PacmanIsHunter { get; private set; }

    public void AddFood(int units)
    {
        if (PacmanIsHunter) return;
        FoodEaten += Mathf.Max(1, units);
        if (FoodEaten < _foodNeeded) return;
        PacmanIsHunter = true;
        Debug.Log("Pacman comió suficiente: ahora caza a los fantasmas.", this);
    }
}
