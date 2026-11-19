using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float WalkSpeed = 2.0f;
    [SerializeField] private float BriskWalkSpeed = 4.0f;
    [SerializeField] private float Gravity = 20.0f;
    [SerializeField] private float LookSensitivity = 0.2f;
    [SerializeField] private float LookAngleLimit = 80f;

    private Camera mainCamera;
    private CharacterController characterController;

    private InputAction moveInput;
    private InputAction briskWalkInput;

    private float currentMoveSpeed = 0.0f;
    private Vector3 moveDirection = Vector3.zero;
    private float lookAngle = 0.0f;
    private bool _wasUiBlocked;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mainCamera = GetComponentInChildren<Camera>();
        characterController = GetComponent<CharacterController>();

        moveInput = InputSystem.actions.FindAction("Move");
        briskWalkInput = InputSystem.actions.FindAction("BriskWalk");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentMoveSpeed = WalkSpeed;
    }

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        _cachedSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        var session = Morsoul.Core.CoreRuntime.Instance?.Session;
        string sceneName = _cachedSceneName;
        if (session != null && session.HasPlayerPosition &&
            session.CurrentSceneMode == Morsoul.Core.SceneMode.Mode3D &&
            session.CurrentSceneId == sceneName)
        {
            characterController.enabled = false;
            transform.position = session.PlayerPosition;
            characterController.enabled = true;
        }
        else
        {
            var spawn = GameObject.Find("PlayerSpawn");
            if (spawn != null)
            {
                characterController.enabled = false;
                transform.position = spawn.transform.position;
                transform.rotation = spawn.transform.rotation;
                characterController.enabled = true;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        bool uiBlocked = Morsoul.Core.PauseMenuController.AnyModalOrPauseOpen ||
                         Morsoul.Dev.DevMenuOverlay.IsOpen;
        if (uiBlocked)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            _wasUiBlocked = true;
            return;
        }
        if (_wasUiBlocked || Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _wasUiBlocked = false;
        }

        Vector2 moveVector = moveInput.ReadValue<Vector2>();
        Vector2 mouseDelta = new Vector2(Mouse.current.delta.x.ReadValue(), Mouse.current.delta.y.ReadValue());

        currentMoveSpeed = briskWalkInput.IsPressed() ? BriskWalkSpeed : WalkSpeed;

        HandleMovement(moveVector);
        HandleLooking(mouseDelta);
    }

    private void HandleMovement(Vector2 moveVector)
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        if (characterController.isGrounded && moveDirection.y < 0)
        {
            moveDirection.y = -2f;
        }
        else
        {
            moveDirection.y -= Gravity * Time.deltaTime;
        }

        Vector3 newSpeed = new Vector2(moveVector.y * currentMoveSpeed, moveVector.x * currentMoveSpeed);

        Vector3 horizontalMovement = (forward * newSpeed.x) + (right * newSpeed.y);

        moveDirection.x = horizontalMovement.x;
        moveDirection.z = horizontalMovement.z;

        characterController.Move(moveDirection * Time.deltaTime);
        var session = Morsoul.Core.CoreRuntime.Instance?.Session;
        if (session != null &&
            session.CurrentSceneMode == Morsoul.Core.SceneMode.Mode3D &&
            session.CurrentSceneId == _cachedSceneName)
        {
            session.SetPlayerContext(
                Morsoul.Core.SceneMode.Mode3D,
                _cachedSceneName,
                transform.position,
                Morsoul.Core.PlayerFacing2D.Down);
        }
    }

    private string _cachedSceneName;

    private void HandleLooking(Vector2 mouseDelta)
    {
        lookAngle += -mouseDelta.y * LookSensitivity;
        lookAngle = Mathf.Clamp(lookAngle, -LookAngleLimit, LookAngleLimit);

        mainCamera.transform.localRotation = Quaternion.Euler(lookAngle, 0, 0);
        transform.rotation *= Quaternion.Euler(0, mouseDelta.x * LookSensitivity, 0);
    }
}
