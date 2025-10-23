using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class PlayerBoard : MonoBehaviour
{
    [Header("Properties")]
    public float speed;
    public float speedPercentage;
    [SerializeField, Range(0, 200)] public int speedKmh; // Range is used for better readability in inspector
    public bool isGrounded;
    private bool lastIsGrounded = true; // Used to compare state changements
    public Grappling_Hook grappling;

    [Header("Sound")]
    private EventSound hoverBoardEngine;

    [Header("Drive Settings")]
    [SerializeField] float driveForce = 17f;
    [SerializeField] float jumpForce = 500;
    [SerializeField] float jumpMaxLoadTime = 3;
    [SerializeField, Tooltip("In seconds")] float jumpMaxPressTime = 2f;
    [SerializeField, Range(0, 1)] float slowingVelocityFactor = .997f;
    [SerializeField, Range(0, 1)] float brakingVelocityFactor = .75f;
    [SerializeField] float rotationTorqueMultiplier = 2f;
    [SerializeField] float driftAmount; // is 25f
    [SerializeField] float angleOfRoll = 30f;
    [SerializeField] float backwardVelocityDivider = 2f;

    [Header("Hover Settings")]
    [SerializeField, Range(0.5f, 5)] float hoverHeight = 1f;
    [SerializeField] float hoverForce = 60; // Was previously 45
    [SerializeField] LayerMask groundLayer;

    [Header("Physics Settings")]
    [SerializeField] Transform hoverboardRenderer;
    [SerializeField] float maximumVelocity = 100f;
    [SerializeField] float hoverGravity = 20f;
    [SerializeField] float fallGravity = 25f;
    [SerializeField] float fallInclineMultiplier = 0.9f;
    [SerializeField] float downhillGravity = 75f;

    [Header("Tricks' Boost Setting")]
    [SerializeField] float minTrickValue = 20; // Minimum trick value before giving a boost 
    [SerializeField] float minBoost = 5;
    [SerializeField] float maxBoost = 50;
    [SerializeField] float boostDivideFactor = 5f;

    [Header("PID Controller")]
    [SerializeField] float pCoeff = 0.8f;
    [SerializeField] float iCoeff = 0.0002f;
    [SerializeField] float dCoeff = 0.06f; // Was previously 0.2f
    [SerializeField] float minimum = -1;
    [SerializeField] float maximum = 1;
    float integral;
    float lastProportional;
    
    [Header("Animation")]
    [SerializeField] Animator animator;
    private float DirectionX = 0.5f; 
    
    Rigidbody rigidBody;
    Vector3 groundNormal;
    float drag;
    bool isThrusterBeingPushed = false; // Use to let button press override stick press
    int turningInput; // 0 = No inputs, 1 = Turning left, 2 = Turning right

    // Grappling
    private Quaternion desiredRotation;
    private float rotationSpeed = 1f;

    // Inputs
    float thruster;
    float rudder;
    float jumpPressTime;
    bool isBraking;
    bool isRegisteringJumpInput = false;

    #region Monobehavior
    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
        hoverBoardEngine = GetComponent<EventSound>();
    }

    void Start()
    {
        // Calculate drag value
        drag = driveForce / maximumVelocity;
        hoverBoardEngine.eventReferenceDico["HoverBoard Velocity"].value = GetFloat;
    }

    private void Update()
    { 
        animator.SetFloat("jumpMeter", Mathf.Clamp(jumpPressTime+0.01f,0.01f,2f));
        if (isRegisteringJumpInput)
        {
            jumpPressTime += Time.deltaTime;

            // Prevent player from jumping too high
            if (jumpPressTime > jumpMaxPressTime)
            {
                jumpPressTime = jumpMaxPressTime;
            }
        }
        
        AnimationTurn();
    }

    void FixedUpdate()
    {
        // Get current speed
        speed = Vector3.Dot(rigidBody.linearVelocity, transform.forward);

        // Apply movements only if not walk
        if (PlayerManager.instance.statePlayer != PlayerManager.StatePlayer.WALK)
        {
            ApplyHover();
            ApplyPropulsion();
        }

        Debug.DrawRay(transform.position, transform.forward, Color.blue, 10f);

        speedPercentage = GetSpeedPercentage();
        speedKmh = (int)GetSpeedInKmh();
        animator.SetFloat("Velocity Hoverboard",Mathf.Clamp( speedKmh/100f,0,1));

        lastIsGrounded = isGrounded;
    }
    #endregion

    #region Physic
    void ApplyHover()
    {
        Ray ray = new Ray(transform.position, -transform.up);
        RaycastHit hitInfo;

        isGrounded = Physics.Raycast(ray, out hitInfo, hoverHeight, groundLayer);

        if (isGrounded != lastIsGrounded) // If there was a changement in state
        {
            // Prevent switching states (thus tp-ing the player) if they have fallen
            if (PlayerManager.instance.statePlayer != PlayerManager.StatePlayer.FALL)
            {
                PlayerManager.instance.SetState(isGrounded == true ? PlayerManager.StatePlayer.SLIDE : PlayerManager.StatePlayer.IN_AIR);
            }
        }

        // If hoverboard is on the ground
        if (isGrounded)
        {
            // Add hover forces
            float height = hitInfo.distance; // Get distance from the ground
            groundNormal = hitInfo.normal.normalized;
            float forcePercent = SeekWithPIDcontroller(hoverHeight, height);

            Vector3 force = groundNormal * hoverForce * forcePercent;
            Vector3 gravity = -groundNormal * hoverGravity * height;

            rigidBody.AddForce(force, ForceMode.Acceleration);
            rigidBody.AddForce(gravity, ForceMode.Acceleration);

        }
        // If hoverboard is not on ground
        else
        {
            // Apply downward gravity
            Vector3 gravity = -Vector3.up * fallGravity;
            rigidBody.AddForce(gravity, ForceMode.Acceleration);

            // Apply rotation
            if (groundNormal != Vector3.zero)
            {
                // Set board rotation according to velocity
                float currentIncline = Mathf.Abs(rigidBody.linearVelocity.y * Time.fixedDeltaTime * fallInclineMultiplier);
                transform.rotation = Quaternion.AngleAxis(currentIncline, transform.right) * transform.rotation;
                // Prevent board from rotating too much
                Quaternion axedRotation = transform.rotation;
                axedRotation = Quaternion.AngleAxis(0, transform.forward) * axedRotation;
                transform.rotation = Quaternion.Lerp(transform.rotation, axedRotation, (1 - rigidBody.linearVelocity.y < 0 ? 0 : rigidBody.linearVelocity.y));
            }
        }

        // Calculate the amount of pitch and roll the board needs to match its orientation
        Vector3 projection = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        Quaternion rotation = Quaternion.LookRotation(projection, groundNormal);

        // Move the board over time to the desired rotation to match the ground
        rigidBody.MoveRotation(Quaternion.Lerp(rigidBody.rotation, rotation, Time.fixedDeltaTime * 10f));
        // Add this cool turn effect on Hoverboard renderer
        float angle = angleOfRoll * -rudder;
      
        Quaternion bodyRotation = transform.rotation * Quaternion.Euler(0f, 0f, angle);
        hoverboardRenderer.rotation = Quaternion.Lerp(hoverboardRenderer.rotation, bodyRotation, Time.deltaTime * 10f);
    }

    void ApplyPropulsion()
    {
        // If hoverboard is not on the ground
        if (!isGrounded)
        {
            // Forbids player from doing anything
            jumpPressTime = 0;
            return;
        }

        // Calculate the rotation torque based on the rudder and current angular velocity
        float rotationTorque = rotationTorqueMultiplier * rudder - rigidBody.angularVelocity.y;
        rigidBody.AddRelativeTorque(0f, rotationTorque, 0f, ForceMode.VelocityChange);

        // Get sideways friction
        float sidewaysSpeed = Vector3.Dot(rigidBody.linearVelocity, transform.right);
        Vector3 sideFriction = -transform.right * (sidewaysSpeed / Time.fixedDeltaTime / driftAmount);

        rigidBody.AddForce(sideFriction, ForceMode.Acceleration);

        // If hoverboard is not propelled
        if (thruster <= 0f)
        {
            rigidBody.linearVelocity *= slowingVelocityFactor;
        }

        // If hoverboard is braking
        if (isBraking)
        {
            // Apply braking velocity reduction
            rigidBody.linearVelocity *= brakingVelocityFactor;
        }

        // If hoverboard has to jump = JumpPression has beenRegistered and is not being registered
        if (jumpPressTime > 0 && !isRegisteringJumpInput)
        {
            // Prevent jump from being too weak (would give player impression of Jump not working) or jump being too strong (would be OP)
            jumpPressTime = Mathf.Clamp(jumpPressTime, 0.5f, jumpMaxLoadTime);
            
            rigidBody.AddForce(transform.up * jumpForce * jumpPressTime, ForceMode.Acceleration);
            jumpPressTime = 0;
            isRegisteringJumpInput = false;
        }

        // Calculate the amount of propulsion force
        float propulsion = driveForce * thruster - drag * Mathf.Clamp(speed, 0f, maximumVelocity);

        // If player has forward propulsion but tries to go backwards
        if (speed >= 0f && thruster < 0f)
        {
            propulsion *= backwardVelocityDivider;
        }
        // Prevent backward driving from being too fast
        else if (thruster < 0f)
        {
            propulsion /= backwardVelocityDivider;
        }

        // Apply force
        if (isGrounded != lastIsGrounded) // If player came back from jumping
        {
            rigidBody.AddForce(transform.forward * (propulsion + GetTricksBoost()), ForceMode.VelocityChange);
        }
        else
        {
            rigidBody.AddForce(transform.forward * propulsion, ForceMode.Acceleration);
        }

        // Apply downward gravity relative to ground's orientation
        float groundSteepness = Mathf.Abs(Mathf.Abs(groundNormal.x) > Mathf.Abs(groundNormal.z) ? groundNormal.x : groundNormal.z); // Get highest normal
        rigidBody.AddForce(Vector3.down * groundSteepness * downhillGravity, ForceMode.Acceleration);
    }

    private float GetTricksBoost()
    {
        // Prevent Player from gaining boost if they're braking or if they're not on board
        if (isBraking || PlayerManager.instance.statePlayer == PlayerManager.StatePlayer.FALL) return 0f;

        float trickAmount = PlayerTricks.instance.TrickAmount;
        hoverBoardEngine.PlaySoundOneShot("boost");
        // Prevent giving player a boost if they haven't tricked that much
        if (trickAmount <= minTrickValue) return 0f;

        float boostValue = Mathf.Clamp(trickAmount / boostDivideFactor, minBoost, maxBoost);

        PlayerTricks.instance.TrickAmount = 0f; // Reset Trick Amount
        return boostValue;
    }

    public float SeekWithPIDcontroller(float _seekValue, float _currentValue)
    {
        float deltaTime = Time.fixedDeltaTime;
        float proportional = _seekValue - _currentValue;

        float derivative = (proportional - lastProportional) / deltaTime;
        integral += proportional * deltaTime;
        lastProportional = proportional;

        // Actual PID formula
        float value = pCoeff * proportional + iCoeff * integral + dCoeff * derivative;
        value = Mathf.Clamp(value, minimum, maximum);

        return value;
    }
    #endregion

    #region Miscellaneous
    public void ResetKeys()
    {
        jumpPressTime = 0;
        isRegisteringJumpInput = false;

        isBraking = false;
    }

    //Returns the total percentage of speed the board is traveling
    public float GetSpeedPercentage()
    {
        return rigidBody.linearVelocity.magnitude / maximumVelocity;
    }

    // Returns speed in km/h
    public float GetSpeedInKmh()
    {
        return rigidBody.linearVelocity.magnitude * 1.60934f /* converts mp/h to km/h */;
    }

    public float GetFloat()
    {
        return rigidBody.linearVelocity.magnitude;
    }

    private void AnimationTurn()
    {
        //player is not turning
        if (rudder == 0)
        {
            //then player balance goes back to middle
            if (DirectionX != 0.5f)
            {
                if (DirectionX > 0.52f)
                {
                    DirectionX -= Time.deltaTime;
                }
                else if(DirectionX < 0.48f)
                {
                    DirectionX += Time.deltaTime;
                }
                else
                {
                    DirectionX = 0.5f;
                }
            }
        }
        //if player is turning 
        else
        {
            DirectionX += Time.deltaTime * rudder;
        }
        DirectionX = Mathf.Clamp(DirectionX, 0, 1);
        animator.SetFloat("DirectionX", DirectionX);
    }

    #endregion

    #region Getters / Setters
    public float DownhillGravity { get => downhillGravity; set => downhillGravity = value; }
    public float DriveForce { get => driveForce; set => driveForce = value; }
    public float JumpForce { get => jumpForce; set => jumpForce = value; }
    public Vector3 GroundNormal { get => groundNormal; }
    public Transform HoverboardRenderer { get => hoverboardRenderer; }
    public int TurningInput { get => turningInput; set => turningInput = value; }
    public Rigidbody RigidBody { get => rigidBody; }
    public bool IsRegisteringJumpInput { get => isRegisteringJumpInput; set => isRegisteringJumpInput = value; }
    #endregion

    #region Inputs
    public void Move(Vector2 _inputs)
    {
        if (!isThrusterBeingPushed)
            thruster = _inputs.y;

        rudder = _inputs.x;

        if (rudder < 0)
            turningInput = 1;
        else if (rudder > 0)
            turningInput = 2;
        else
            turningInput = 0;
    }
    public void SetBrake(bool _value) => isBraking = _value;
    public void SetThruster(float _value)
    {
        thruster = _value;
        isThrusterBeingPushed = _value >= 0.1f ? true : false;
    }

    public void SetJump(bool willJump)
    {
        isRegisteringJumpInput = willJump;
    }
    #endregion

}
