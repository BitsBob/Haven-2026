using UnityEngine;

public class BouncePad : MonoBehaviour
{
    [Tooltip("Upward speed given to the player. 16 reaches about 6 metres.")]
    public float launchSpeed = 16f;

    void OnTriggerEnter(Collider other) { Launch(other); }
    void OnTriggerStay(Collider other) { Launch(other); }

    void Launch(Collider other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player != null) player.SetVerticalVelocity(launchSpeed);
    }
}
