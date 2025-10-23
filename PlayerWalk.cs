using UnityEngine;

public class PlayerWalk : MonoBehaviour
{
    // Components
    Rigidbody rb;
    Vector2 playerInputs;
    Vector3 velocity = Vector3.zero;
    Vector3 moveDirection;

    //animator
    [SerializeField] Animator animator;

    // Settings
    [SerializeField] Transform cam;
    [SerializeField] float movementSpeed;
    [SerializeField] float groundedRaycastLength;
    [SerializeField] float fallGravity;
    [SerializeField] float downhillGravity;

    // Things
    private bool isGrounded;
    Vector3 groundNormal;
    Transform classicCamera;

    public Rigidbody Rb { get => rb; }

    #region Monobehaviour
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        classicCamera = cam;
    }

    private void FixedUpdate()
    {
        // Apply movements only if in walk
        if (PlayerManager.instance.statePlayer == PlayerManager.StatePlayer.WALK)
        {
            MovementsRelativeToCam();
        }

        // Know if player is grounded
        Ray ray = new Ray(transform.position, -transform.up);
        RaycastHit hitInfo;

        isGrounded = Physics.Raycast(ray, out hitInfo, groundedRaycastLength);

        if (isGrounded)
        {
            // Ground normal
            groundNormal = hitInfo.normal.normalized;

            // Apply velocity (player can move)
            rb.linearVelocity = new Vector3(moveDirection.x, Rb.linearVelocity.y, moveDirection.z);

            animator.SetFloat("Velocity Walk", Mathf.Abs(rb.linearVelocity.z));
        }
        else
        {
            // Apply downward gravity (push player towards ground)
            rb.AddForce(/* gravity */ Vector3.down * fallGravity, ForceMode.Acceleration);
        }

        // Apply downward gravity relative to ground's orientation
        float groundSteepness = Mathf.Abs(Mathf.Abs(groundNormal.x) > Mathf.Abs(groundNormal.z) ? groundNormal.x : groundNormal.z); // Get highest normal
        rb.AddForce(Vector3.down * groundSteepness * downhillGravity, ForceMode.Acceleration);
    }
    #endregion

    #region Methods
    private void MovementsRelativeToCam()
    {
        // Get camera direction
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;

        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        // Apply inputs
        velocity.x = playerInputs.y * movementSpeed * Time.fixedDeltaTime;
        velocity.z = playerInputs.x * movementSpeed * Time.fixedDeltaTime;
        velocity.y = Rb.linearVelocity.y; // Keep y (falling) velocity

        // Set relative to camera direction
        moveDirection = (velocity.x * camForward) + (velocity.z * camRight);

        // If player is not using PhotoMode
        if (!PlayerManager.instance.IsUsingPhotoMode)
        {
            // Change player facing direction (according to camera)
            if (playerInputs != Vector2.zero) // When player is moving
            {
                Quaternion wantedRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                Quaternion newRotation = Quaternion.Lerp(Rb.rotation, wantedRotation, 7.5f * Time.fixedDeltaTime);
                transform.rotation = newRotation;
            }
        }
    }

    public void ChangeCamera(Transform newCamera) => cam = newCamera;
    public void ResetCamera() => cam = classicCamera;

    // Get inputs
    public void Move(Vector2 _inputs)
    {
        playerInputs = _inputs;
    }
    #endregion
}
