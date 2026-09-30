using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TapTapGameJam.Graybox
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class GrayboxController2D : MonoBehaviour
    {
        public enum ViewMode { TopDown, SideView }

        [Header("Movement")]
        [SerializeField] private float topDownSpeed = 5f;
        [SerializeField] private float sideSpeed = 6f;
        [SerializeField] private float jumpSpeed = 9f;
        [SerializeField] private float sideGravityScale = 3f;
        [SerializeField] private Vector2 topDownSpawn = new Vector2(-12f, 0f);
        [SerializeField] private Vector2 sideViewSpawn = new Vector2(10f, -2.9f);

        private readonly Collider2D[] groundHits = new Collider2D[8];
        private Rigidbody2D body;
        private BoxCollider2D hitbox;
        private SpriteRenderer display;
        private Vector2 input;
        private bool jumpQueued;
        private bool grounded;
        private ViewMode mode;

        public ViewMode Mode => mode;
        public bool IsGrounded => grounded;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hitbox = GetComponent<BoxCollider2D>();
            display = GetComponent<SpriteRenderer>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            SetMode(ViewMode.TopDown);
        }

        private void Update()
        {
            input = ReadMovement();
            if (ReadJumpDown()) jumpQueued = true;
            if (ReadSwitchDown())
                SetMode(mode == ViewMode.TopDown ? ViewMode.SideView : ViewMode.TopDown);
        }

        private void FixedUpdate()
        {
            if (mode == ViewMode.TopDown)
            {
                grounded = false;
                Velocity = input.normalized * topDownSpeed;
            }
            else
            {
                grounded = CheckGrounded();
                Vector2 velocity = Velocity;
                velocity.x = input.x * sideSpeed;
                if (jumpQueued && grounded)
                {
                    velocity.y = jumpSpeed;
                    grounded = false;
                }
                Velocity = velocity;
            }
            jumpQueued = false;
        }

        public void SetMode(ViewMode next)
        {
            mode = next;
            input = Vector2.zero;
            jumpQueued = false;
            grounded = false;
            Velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = mode == ViewMode.TopDown ? 0f : sideGravityScale;
            Damping = mode == ViewMode.TopDown ? 8f : 0f;
            body.position = mode == ViewMode.TopDown ? topDownSpawn : sideViewSpawn;
            if (display != null)
                display.color = mode == ViewMode.TopDown
                    ? new Color(0.25f, 0.8f, 1f)
                    : new Color(1f, 0.73f, 0.25f);
            CameraFollow2D follower = Camera.main != null
                ? Camera.main.GetComponent<CameraFollow2D>() : null;
            if (follower != null) follower.Snap();
        }

        private bool CheckGrounded()
        {
            Bounds bounds = hitbox.bounds;
            Vector2 center = new Vector2(bounds.center.x, bounds.min.y - 0.04f);
            Vector2 size = new Vector2(bounds.size.x * 0.8f, 0.1f);
            ContactFilter2D filter = new ContactFilter2D().NoFilter();
            int count = Physics2D.OverlapBox(center, size, 0f, filter, groundHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = groundHits[i];
                if (other != null && other != hitbox && !other.isTrigger &&
                    other.attachedRigidbody != body)
                    return true;
            }
            return false;
        }

        private Vector2 Velocity
        {
#if UNITY_6000_0_OR_NEWER
            get => body.linearVelocity;
            set => body.linearVelocity = value;
#else
            get => body.velocity;
            set => body.velocity = value;
#endif
        }

        private float Damping
        {
#if UNITY_6000_0_OR_NEWER
            set => body.linearDamping = value;
#else
            set => body.drag = value;
#endif
        }

        private static Vector2 ReadMovement()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;
            float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
#else
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            return new Vector2(x, y);
#endif
        }

        private static bool ReadJumpDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private static bool ReadSwitchDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }
    }
}
