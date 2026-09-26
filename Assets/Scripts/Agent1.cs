using UnityEngine;
using UnityEngine.UIElements;

public class Agent1 : Steering
{
    
    [SerializeField] Steering _target;

    // Update is called once per frame
    void Update()
    {
        AddForce(Pursuit(_target));
    }
}
