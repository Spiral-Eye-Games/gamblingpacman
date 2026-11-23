using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
[DisallowMultipleComponent]
public sealed class RoundExit2D : MonoBehaviour
{
    readonly StarCollection2D _stars = new StarCollection2D();
    SpriteRenderer _sprite;
    Animator _portalAnimator;
    Color _unlockedColor;

    void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _portalAnimator = GetComponent<Animator>();
        _unlockedColor = _sprite.color;
        Lock();
        _stars.Completed += Unlock;
    }

    void OnDestroy()
    {
        _stars.Completed -= Unlock;
    }

    internal void BeginRound()
    {
        Lock();
        _stars.Begin(FindSceneStars());
    }

    void Lock()
    {
        _portalAnimator.speed = 0f;
        if (_portalAnimator.runtimeAnimatorController != null)
        {
            _portalAnimator.Play(0, 0, 0f);
            _portalAnimator.Update(0f);
        }
        _sprite.color = new Color(_unlockedColor.r * 0.4f, _unlockedColor.g * 0.4f,
            _unlockedColor.b * 0.4f, _unlockedColor.a);
    }

    IEnumerable<StarPickup2D> FindSceneStars()
    {
        foreach (StarPickup2D star in FindObjectsByType<StarPickup2D>(FindObjectsSortMode.None))
        {
            if (star.isActiveAndEnabled && star.gameObject.scene == gameObject.scene)
                yield return star;
        }
    }

    void Unlock()
    {
        _sprite.color = _unlockedColor;
        _portalAnimator.speed = 1f;
    }

    internal bool TryFinish(IReadOnlyList<BoidAgent2D> players)
    {
        if (!isActiveAndEnabled) return false;

        _stars.CollectNearby(players);
        if (!_stars.IsComplete) return false;

        Bounds area = _sprite.bounds;
        foreach (BoidAgent2D player in players)
        {
            if (player == null || !player.IsAlive) continue;
            Vector3 position = player.transform.position;
            position.z = area.center.z;
            if (area.Contains(position)) return true;
        }
        return false;
    }
}
