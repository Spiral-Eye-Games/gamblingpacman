using UnityEngine;

public class Agent2 : Steering
{
    [SerializeField] Transform _target;

    // Update is called once per frame
    void Update()
    {
        AddForce(Flee(_target.position));
        Move();
    }
}
