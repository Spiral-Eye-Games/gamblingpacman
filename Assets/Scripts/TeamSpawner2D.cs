using UnityEngine;

// Duplica los agentes de la escena para que se vea Flocking y haya fantasmas.
public class TeamSpawner2D : MonoBehaviour
{
    [SerializeField] BoidAgent2D _boidTemplate;
    [SerializeField, Min(0)] int _extraBoids = 2;
    [SerializeField] HunterFSM2D _hunterTemplate;
    [SerializeField, Min(0)] int _extraHunters = 1;

    void Start()
    {
        if (_boidTemplate == null || _hunterTemplate == null)
        {
            Debug.LogError("TeamSpawner2D necesita un boid y un fantasma de la escena.", this);
            return;
        }

        for (int i = 1; i <= _extraBoids; i++)
        {
            Vector2 offset = new Vector2(0.8f * i, -0.7f * i);
            BoidAgent2D copy = Instantiate(_boidTemplate,
                (Vector2)_boidTemplate.transform.position + offset,
                _boidTemplate.transform.rotation);
            copy.name = "Pacman boid " + (i + 1);
        }

        for (int i = 1; i <= _extraHunters; i++)
        {
            Vector2 offset = new Vector2(-0.9f * i, 0.8f * i);
            HunterFSM2D copy = Instantiate(_hunterTemplate,
                (Vector2)_hunterTemplate.transform.position + offset,
                _hunterTemplate.transform.rotation);
            copy.name = "Fantasma " + (i + 1);
        }
    }
}
