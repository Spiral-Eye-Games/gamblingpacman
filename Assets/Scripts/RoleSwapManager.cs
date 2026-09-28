using UnityEngine;

// Cuando Pacman come la comida necesaria, caza a los fantasmas durante _hunterDuration segundos.
public class RoleSwapManager : MonoBehaviour
{
    [SerializeField, Min(1)] int _foodNeeded = 1;
    [SerializeField, Min(0.01f)] float _hunterDuration = 3f;

    float _hunterUntil; // momento (Time.time) en que termina la caza

    public int FoodNeeded => _foodNeeded;
    public int FoodEaten { get; private set; }

    // Ya no hace falta un bool ni un Update: es cazador mientras no llegue _hunterUntil.
    public bool PacmanIsHunter => Time.time < _hunterUntil;

    public void AddFood(int units)
    {
        if (PacmanIsHunter) return;
        FoodEaten += Mathf.Max(1, units);
        if (FoodEaten < _foodNeeded) return;

        _hunterUntil = Time.time + _hunterDuration;
        FoodEaten = 0; // para el próximo ciclo hay que volver a comer
        Debug.Log("Pacman comió suficiente: caza a los fantasmas por " + _hunterDuration + "s.", this);
    }
}