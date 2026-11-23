using System;
using UnityEngine;

//Cuando Pacman come la comida necesaria, caza a los fantasmas durante _hunterDuration segundos
[DefaultExecutionOrder(-100)]
public class RoleSwapManager : MonoBehaviour
{
    [SerializeField, Min(1)] int _foodNeeded = 1;
    [SerializeField, Min(0.01f)] float _hunterDuration = 3f;

    float _hunterUntil; // momento (Time.time) en que termina la caza
    Action _updateState = delegate { };
    Action<int> _addFood;

    internal event Action<bool> RoleChanged;

    public int FoodNeeded => _foodNeeded;
    public int FoodEaten { get; private set; }

    // El efecto permanece activo hasta que vence el tiempo de caza.
    public bool PacmanIsHunter => Time.time < _hunterUntil;

    void Awake() => _addFood = AccumulateFood;

    void Update() => _updateState();

    public void AddFood(int units)
    {
        // Sincroniza el vencimiento también si la comida llega antes del Update.
        _updateState();
        _addFood(units);
    }

    void AccumulateFood(int units)
    {
        FoodEaten += Mathf.Max(1, units);
        if (FoodEaten < _foodNeeded) return;

        _hunterUntil = Time.time + _hunterDuration;
        FoodEaten = 0; // para el próximo ciclo hay que volver a comer
        _addFood = delegate { };
        _updateState = WaitForHuntEnd;
        RoleChanged?.Invoke(true);
        Debug.Log("Pacman comió suficiente: caza a los fantasmas por " + _hunterDuration + "s.", this);
    }

    void WaitForHuntEnd()
    {
        if (Time.time < _hunterUntil) return;
        _addFood = AccumulateFood;
        _updateState = delegate { };
        RoleChanged?.Invoke(false);
    }
}
