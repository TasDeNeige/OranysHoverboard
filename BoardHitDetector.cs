using UnityEngine;
using System.Collections.Generic;

public class BoardHitDetector : MonoBehaviour
{
    [SerializeField] float ragdollThreshold = 1750;
    [SerializeField] float yCollisionImportance;
    [SerializeField] List<Transform> landingCheckers = new List<Transform>();
    [SerializeField] float firstLandingCheckerRaycastDistance;
    [SerializeField] float landingCheckerRaycastDistance;

    private void OnCollisionEnter(Collision collision)
    {
        // Prevent detecting collision with itself or if player has already fallen
        if (collision.gameObject.CompareTag("Player") == true || PlayerManager.instance.statePlayer == PlayerManager.StatePlayer.FALL) return;

        Vector3 collisionForce = new Vector3(collision.impulse.x / Time.fixedDeltaTime,
                                            (collision.impulse.y * yCollisionImportance) / Time.fixedDeltaTime,
                                             collision.impulse.z / Time.fixedDeltaTime);

        // Used to check if landing is correct when Y magnitude is too violent
        if (collisionForce.y > ragdollThreshold)
        {
            // Shoot three raycast to check if the board is oriented correctly
            int groundTouched = 0;
            Vector3 raycastDown = -landingCheckers[0].up; // Shoot down relative to hoverboard's underside

            for (int i = 0; i < landingCheckers.Count; i++)
            {
                // Check if ground was touched within reasonable distance
                Debug.DrawRay(landingCheckers[i].position, raycastDown, Color.red, 10f);

                RaycastHit hit;
                if (Physics.Raycast(landingCheckers[i].position, raycastDown, out hit, i == 0 ? firstLandingCheckerRaycastDistance : landingCheckerRaycastDistance))
                {
                    groundTouched++;
                }
            }

            // If board is not oriented correctly
            if (groundTouched < landingCheckers.Count)
            {
                // Fall
                PlayerManager.instance.SetState(PlayerManager.StatePlayer.FALL);
            }
        }

        // Check if the player is going too fast
        else if (collisionForce.magnitude > ragdollThreshold)
        {
            PlayerManager.instance.SetState(PlayerManager.StatePlayer.FALL);
        }
    }
}