using System;
using UnityEngine;

public class CollisionRelay : MonoBehaviour
{
    public event Action<ControllerColliderHit> OnCollision;

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        OnCollision?.Invoke(hit);
    }
}