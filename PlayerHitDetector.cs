using UnityEngine;

public class PlayerHitDetector : MonoBehaviour
{
    [SerializeField] float minVelocityNeeded;
    [SerializeField] bool doesCheckCollisionOnY;
    [SerializeField] LayerMask unwantedLayers;

    private void OnTriggerEnter(Collider other)
    {
        // Prevent detecting collision with itself or if player has already fallen
        if (other.CompareTag("Player") == true || PlayerManager.instance.statePlayer == PlayerManager.StatePlayer.FALL) return;

        // Prevent colliding with an object that should not provide collision
        if (((1 << other.gameObject.layer) & unwantedLayers) != 0) return;

        Vector3 boardVelocity = PlayerManager.instance.Board.GetComponent<Rigidbody>().linearVelocity;

        // If player is going too fast
        if (Mathf.Abs(boardVelocity.x) >= minVelocityNeeded ||
            Mathf.Abs(boardVelocity.z) >= minVelocityNeeded ||
            (doesCheckCollisionOnY == true ? Mathf.Abs(boardVelocity.y) >= minVelocityNeeded : false))
        {
            PlayerManager.instance.SetState(PlayerManager.StatePlayer.FALL);
        }
    }
}