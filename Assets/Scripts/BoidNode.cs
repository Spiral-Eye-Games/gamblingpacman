using UnityEngine;

//Igual que Node en la clase, cada nodo sabe ejecutar su parte del árbol
public abstract class BoidNode : MonoBehaviour
{
    public abstract void Execute(BoidAgent2D boid);
}
