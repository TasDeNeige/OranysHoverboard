using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

public class PlayerTricks : MonoBehaviour
{
    [SerializeField] GameObject hoverboard;
    [SerializeField] CapsuleCollider colliderPlayer1;
    [SerializeField] CapsuleCollider colliderPlayer2;
    [SerializeField] float trickExecutionSpeedFactor = 250;
    [SerializeField] float maxTrickSpeed = 30f;

    float colliderBaseHeight1;
    float colliderBaseHeight2;

    Vector2 isUsingTricks = Vector2.zero;
    Vector2 playerInputs = Vector2.zero;
    float trickAmount = 0f;
    public static PlayerTricks instance;
    Animator animator;

    public float TrickAmount { get => trickAmount; set => trickAmount = value; }

    private void Awake()
    {
        instance = this;
        animator = FindAnyObjectByType<Animator>();
        colliderBaseHeight1 = colliderPlayer1.height;
        colliderBaseHeight2 = colliderPlayer2.height;
    }

    // Update is called once per frame
    void Update()
    {
        // Apply movements only if in air
        if (PlayerManager.instance.statePlayer == PlayerManager.StatePlayer.IN_AIR)
        {
            if (playerInputs != Vector2.zero && isUsingTricks != Vector2.zero)
            {
                float speedPercentage = PlayerManager.instance.GetPlayerBoard.GetSpeedPercentage();
                float currentTrickSpeed = trickExecutionSpeedFactor * speedPercentage * Time.deltaTime;
                currentTrickSpeed = Mathf.Clamp(currentTrickSpeed, 0f, maxTrickSpeed);
                hoverboard.transform.Rotate(playerInputs.y * currentTrickSpeed, playerInputs.x * currentTrickSpeed, 0.0f);

                trickAmount += currentTrickSpeed * (speedPercentage * 0.5f); // Speed percentage is added to give more importance to the trick execution speed

                // Apply animations
                animator.SetInteger("dirTrick", 0);
                if (Mathf.Abs(playerInputs.y) >= Mathf.Abs(playerInputs.x) && Mathf.Abs(playerInputs.y) > 0.1f)
                {
                    animator.SetInteger("dirTrick", 1);
                }
                else if (Mathf.Abs(playerInputs.x) > 0.1f)
                {
                    animator.SetInteger("dirTrick", -1);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        // Ensure collider stays upright
        transform.rotation = Quaternion.Euler(Vector3.up);
    }

    // Inputs
    public void Move(Vector2 _inputs)
    {
        playerInputs = _inputs;
    }

    public void SetToggle(Vector2 _inputs)
    {
        isUsingTricks = _inputs;
        if (isUsingTricks != Vector2.zero)
        {
            colliderPlayer1.height = colliderBaseHeight1 / 2.0f;
            colliderPlayer2.height = colliderBaseHeight2 / 2.0f;
            animator.SetBool("TricksTriggered", true);
        }
        else
        {

            colliderPlayer1.height = colliderBaseHeight1;
            colliderPlayer2.height = colliderBaseHeight2;
            animator.SetBool("TricksTriggered", false);
        }
    }
}
