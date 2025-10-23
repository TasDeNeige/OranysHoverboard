using System.Collections.Generic;
using System;
using Unity.Cinemachine;
using UnityEngine;
using FMODUnity;
using Saver;

public class PlayerManager : MonoBehaviour
{
    #region Members
    public static PlayerManager instance;

    public enum StatePlayer { WALK, SLIDE, IN_AIR, FALL }
    public StatePlayer statePlayer;
    bool isUsingPhotoMode = false;

    [Header("Objects")]
    [SerializeField] GameObject character;
    [SerializeField] Transform getUpPos;
    [SerializeField] GameObject board;
    [SerializeField] GameObject boardPlayerHitColliders;
    [SerializeField] GameObject ragdoll;
    private Animator animator;
    private PlayerRagdoll playerRagdoll;
    [SerializeField] Transform charaPosOnBoard;
    [SerializeField] Transform BeltHeldRadio;
    [SerializeField] GameObject hoverboardInHand;
    [SerializeField] ParticleSystem waterParticle;

    [Header("Settings")]
    [SerializeField] Vector3 boardSpawnOffset;
    [SerializeField, Tooltip("In seconds")] float stateSwitchCooldown;
    public float stateSwitchCurrentCooldown;
    [SerializeField] float fallMaxStopSpeed; // Maximum speed to be able to wake up from ragdoll

    [Header("playerCrashSound")]
    [SerializeField] GameObject playerSoundBank;
    private PlayerSounds playerSounds;

    // Scripts
    PlayerWalk playerWalk;
    PlayerBoard playerBoard;
    Rigidbody charaRb;

    // Miscellaneous
    public event Action OnInteract;
    public event Action OnFall;
    public event Action<StatePlayer> OnSwitchState;
    #endregion

    #region Monobehavior
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        //Ragdoll
        playerRagdoll = ragdoll.GetComponent<PlayerRagdoll>();
        //animator
        animator = ragdoll.GetComponent<Animator>();
        //Radio / player methods binding
        OnSwitchState += BeltHeldRadio.GetComponent<BeltHeldRadio>().OnStateChangeRadio;

        playerWalk = character.GetComponent<PlayerWalk>();
        playerBoard = Board.GetComponent<PlayerBoard>();
        playerSounds = playerSoundBank.GetComponent<PlayerSounds>();
    }

    private void Start()
    {
        charaRb = Character.GetComponent<Rigidbody>();
        OnFall += BeltHeldRadio.GetComponent<BeltHeldRadio>().CrashedRadio;

        // Set player's state to 'walk' upon game launching
        playerRagdoll.ToggleRagdoll(false);
        SetState(StatePlayer.WALK);
    }

    private void Update()
    {
        // Decrease cooldown
        if (stateSwitchCurrentCooldown > 0)
        {
            stateSwitchCurrentCooldown -= Time.deltaTime;
        }
    }

    private void LateUpdate()
    {
        // Stick player on board
        if (statePlayer == StatePlayer.SLIDE || statePlayer == StatePlayer.IN_AIR)
        {
            Character.transform.position = charaPosOnBoard.position;
            Character.transform.rotation = charaPosOnBoard.rotation;
        }
    }
    #endregion

    #region Methods
    public void SetState(StatePlayer _state)
    {
        switch (_state)
        {
            case StatePlayer.WALK:
                EnableWalkOrSlide(true);
                animator.SetBool("isWalking", true);
                break;

            case StatePlayer.SLIDE:
                animator.SetBool("isGrounded", true);

                // If last state is something that get rids of the hoverboard
                if (statePlayer != StatePlayer.SLIDE && statePlayer != StatePlayer.IN_AIR)
                {
                    EnableWalkOrSlide(false);
                    animator.SetBool("isWalking", false);
                    playerRagdoll.ToggleRagdoll(false);
                }

                animator.SetBool("TricksTriggered", false);
                break;

            case StatePlayer.IN_AIR:
                animator.SetBool("isGrounded", false);
                int tricksChoice = UnityEngine.Random.Range(0, 2);
                if(tricksChoice == 0)
                    animator.SetBool("TricksRand", false);
                else
                    animator.SetBool("TricksRand", true);

                // If last state is something that get rids of the hoverboard
                if (statePlayer != StatePlayer.SLIDE && statePlayer != StatePlayer.IN_AIR)
                {
                    EnableWalkOrSlide(false);
                }
                break;

            case StatePlayer.FALL:
                OnFall?.Invoke();
                ToggleFall();
                animator.enabled = false;
                break;
        }

        statePlayer = _state;
        OnSwitchState?.Invoke(_state);
    }
    public StatePlayer GetState() => statePlayer;


    /// <param name="isWalkEnabled">True enables Walk, false enables Slide</param>
    private void EnableWalkOrSlide(bool isWalkEnabled)
    {
        if (!animator.enabled)
        {
            animator.enabled = true;
        }

        // Character components
        charaRb.useGravity = isWalkEnabled; // Enable gravity
        Character.GetComponent<PlayerWalk>().enabled = isWalkEnabled; // Enable walking
        Character.GetComponent<CapsuleCollider>().enabled = isWalkEnabled; // Enable collider
        hoverboardInHand.SetActive(isWalkEnabled);

        // Board components
        Board.GetComponent<PlayerBoard>().enabled = !isWalkEnabled;
        Board.GetComponent<EventSound>().enabled = !isWalkEnabled;

        Board.SetActive(!isWalkEnabled); // Activate board

        playerBoard.ResetKeys();
        // Walk state
        if (isWalkEnabled)
        {
            charaRb.linearVelocity = Vector3.zero; // Reset velocity to prevent player flying away
        }
        // Slide state
        else
        {
            // Reset velocity
            Board.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;

            // Set board position
            Board.transform.position = getUpPos.position + boardSpawnOffset;
            Board.transform.rotation = new Quaternion(0f, getUpPos.rotation.y, 0f, getUpPos.rotation.w);

            // Ensures that colliders are activated
            boardPlayerHitColliders.SetActive(true);
            character.GetComponent<CapsuleCollider>().enabled = false;
        }
    }

    private void ToggleFall()
    {
        // Character components
        charaRb.useGravity = true; // Enable gravity
        Character.GetComponent<PlayerWalk>().enabled = false; // Disable walking
        Character.GetComponent<CapsuleCollider>().enabled = true; // Enable collider

        // Board components
        Board.GetComponent<PlayerBoard>().enabled = true;
        Board.GetComponent<EventSound>().enabled = true;
        Board.SetActive(true); // Activate board
        boardPlayerHitColliders.SetActive(false);

        // Reset inputs
        playerWalk.Move(Vector2.zero);
        playerBoard.Move(Vector2.zero);
        PlayerTricks.instance.Move(Vector2.zero);
        //Character component
        charaRb.useGravity = false;
        charaRb.linearVelocity = Vector3.zero;
        Character.GetComponent<CapsuleCollider>().enabled = false;
        /// ACTIVATE RAGDOLL
        playerRagdoll.ToggleRagdoll(true);
    }
    #endregion

    #region Getters / Setters
    public GameObject Character { get => character; }
    public GameObject Board { get => board; }
    public PlayerWalk GetPlayerWalk { get => playerWalk; }
    public PlayerBoard GetPlayerBoard { get => playerBoard; }
    public ParticleSystem WaterParticle { get => waterParticle; }

    public bool IsUsingPhotoMode
    {
        get => isUsingPhotoMode;
        set => isUsingPhotoMode = value;
    }
    #endregion

    #region Input related
    public void Move(Vector2 _inputs)
    {
        // Prevent Player from moving if they're falling
        if (statePlayer == StatePlayer.FALL) return;

        // Reset every inputs before applying new ones
        playerWalk.Move(Vector2.zero);
        playerBoard.Move(Vector2.zero);
        PlayerTricks.instance.Move(Vector2.zero);

        playerWalk.Move(_inputs);
        playerBoard.Move(_inputs);
        PlayerTricks.instance.Move(_inputs);
    }

    public void DoTricks(Vector2 _inputs)
    {
        // Prevent Player from moving if they're falling
        if (statePlayer == StatePlayer.FALL) return;

        // Reset inputs before applying new ones
        playerWalk.Move(Vector2.zero);
        playerBoard.Move(Vector2.zero);
        PlayerTricks.instance.Move(Vector2.zero);

        PlayerTricks.instance.Move(_inputs);
    }

    #region Board inputs
    public void SetBoardBrake(bool isBrakeActive)
    {
        playerBoard.SetBrake(isBrakeActive);
    }

    public void SetBoardThruster(float thruster)
    {
        playerBoard.SetThruster(thruster);
    }

    public void SetBoardJump(bool willJump)
    {
        playerBoard.SetJump(willJump);
    }
    #endregion

    public void Interact() => OnInteract?.Invoke();

    // Rotate between Walk & Slide
    public void ChangeState()
    {
        // Add cooldown
        if (stateSwitchCurrentCooldown <= 0)
        {
            stateSwitchCurrentCooldown = stateSwitchCooldown;
            if (statePlayer == StatePlayer.WALK)
            {
                SetState(StatePlayer.SLIDE);
            }
            else if (statePlayer == StatePlayer.FALL)
            {

                // Prevent player from switching to another state if they're still moving/falling
                if (playerWalk.Rb.linearVelocity.magnitude >= fallMaxStopSpeed) return;

                //// Reset board position & velocity
                board.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;

                //radio comes back
                BeltHeldRadio.GetComponent<BeltHeldRadio>().RadioBackUp();

                SetState(StatePlayer.SLIDE);
            }
            else
            {
                // Prevent player from switching to Walk if they're going too fast
                if (playerBoard.speedKmh >= 25 || statePlayer == StatePlayer.IN_AIR) return;

                SetState(StatePlayer.WALK);
            }
        }
    }
    #endregion
}
