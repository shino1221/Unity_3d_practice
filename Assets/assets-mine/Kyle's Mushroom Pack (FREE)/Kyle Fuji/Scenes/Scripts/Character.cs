using UnityEngine;
using UnityEngine.InputSystem;

namespace Character
{
    public class Character : MonoBehaviour
    {
        public float playerSpeed = 15.0f;
        public float jumpHeight = 4f;
        public float gravityValue = -75f;
        public float mouseSensitivity = 5.0f;
        public float rotationSmoothing = 15f; // Higher = snappier, Lower = smoother
        private bool groundedPlayer;
        private float xRotation = 0;
        private Vector2 currentMouseDelta;
        private Vector2 appliedMouseDelta;

        public Transform characterCamera;
        public CharacterController controller;
        private Vector3 playerVelocity;

        [Header("Input Actions")]
        public InputActionReference moveAction;
        public InputActionReference jumpAction;
        public InputActionReference lookAction;

        private void OnEnable()
        {
            moveAction.action.Enable();
            jumpAction.action.Enable();
        }

        private void OnDisable()
        {
            moveAction.action.Disable();
            jumpAction.action.Disable();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            groundedPlayer = controller.isGrounded;

            if (groundedPlayer)
            {
                // Slight downward velocity to keep grounded stable
                if (playerVelocity.y < -2f)
                    playerVelocity.y = -2f;
            }

            // Read inputs
            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            Vector3 move = (transform.right * moveInput.x) + (transform.forward * moveInput.y);
            if (move.magnitude > 1f)
            {
                move.Normalize();
            }

            // Jump using WasPressedThisFrame()
            if (groundedPlayer && jumpAction.action.WasPressedThisFrame())
            {
                playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravityValue);
            }

            // Apply gravity
            playerVelocity.y += gravityValue * Time.deltaTime;

            // Move
            Vector3 finalMove = move * playerSpeed + Vector3.up * playerVelocity.y;
            controller.Move(finalMove * Time.deltaTime);
        }

        private void LateUpdate()
        {
            Vector2 lookInput = lookAction.action.ReadValue<Vector2>();

            appliedMouseDelta = Vector2.Lerp(appliedMouseDelta, lookInput, rotationSmoothing * Time.deltaTime);
            float mouseX = appliedMouseDelta.x * mouseSensitivity * Time.deltaTime;
            float mouseY = appliedMouseDelta.y * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            characterCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }
    }
}