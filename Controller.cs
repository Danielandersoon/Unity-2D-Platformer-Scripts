using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct FrameInput
{
    public float X;
    public bool JumpDown;
    public bool JumpUp;
    public bool DashDown;
    public bool DashUp;
}

public interface IPlayerController
{
    public Vector3 Velocity { get; }
    public FrameInput Input { get; }
    public bool JumpingThisFrame { get; }
    public bool LandingThisFrame { get; }
    public Vector3 RawMovement { get; }
    public bool Grounded { get; }
}

public struct RayRange
{
    public RayRange(float x1, float y1, float x2, float y2, Vector2 dir)
    {
        Start = new Vector2(x1, y1);
        End = new Vector2(x2, y2);
        Dir = dir;
    }

    public readonly Vector2 Start, End, Dir;
}

public class Controller : MonoBehaviour, IPlayerController
{
    public Vector3 StartPosition;
    public Vector3 Velocity { get; private set; }
    public FrameInput Input { get; private set; }
    public bool JumpingThisFrame { get; private set; }
    public bool LandingThisFrame { get; private set; }
    public Vector3 RawMovement { get; private set; }
    public bool Grounded => _colDown;

    public bool canDash;

    private Vector3 _lastPosition;
    private float _currentHorizontalSpeed, _currentVerticalSpeed;

    // colliders not established at start of Update
    private bool _active;
    void Awake() => Invoke(nameof(Activate), 0.5f);
    void Activate() => _active = true;

    private void Update()
    {
        if (!_active) return;
        // Calc vel
        Velocity = (transform.position - _lastPosition) / Time.deltaTime;
        _lastPosition = transform.position;

        if (Grounded)
            canDash = true;

        GatherInput();
        RunCollisionChecks();

        if (!_dashing)
            CalculateWalk(); // X speed
                               
        CalculateJumpApex(); // fall speed - calculate before gravity
        CalculateGravity(); // Y mov
        CalculateJump(); // overrides vertical... sometimes

        makeDash(); // overwrites all other movement unless caused by collision

        MoveCharacter(); // Transforms character by velocity vector

        Animator(); // The animation controller (Could be replaced with the built in animation controller unity provides)
    }

    #region Gather Input

    private void GatherInput()
    {
        Input = new FrameInput
        {
            JumpDown = UnityEngine.Input.GetButtonDown("Jump"), // Change to your jump button
            JumpUp = UnityEngine.Input.GetButtonUp("Jump"),     // Change to your jump button
            X = UnityEngine.Input.GetAxisRaw("Horizontal"),     // Change to your walk axis   
            DashDown = UnityEngine.Input.GetButtonDown("Dash"), // Change to your dash button
            DashUp = UnityEngine.Input.GetButtonUp("Dash"),     // Change to your dash button

        };
        if (Input.JumpDown)
        {
            _lastJumpPressed = Time.time;
        }
    }

    #endregion

    #region Collisions

    [Header("COLLISION")]
    [SerializeField] private Bounds _characterBounds;
    [SerializeField] private LayerMask _groundLayer; // Select layer you will use for ground collisions
    [SerializeField] private LayerMask _deathLayer;  // Select layer you will use for death
    [SerializeField] private int _detectorCount = 3; // This is the number of rays cast to check collisions
    [SerializeField] private float _detectionRayLength = 0.1f; // This is the length of the collision rays
    [SerializeField][Range(0.1f, 0.3f)] private float _rayBuffer = 0.1f; // Prevents side detectors hitting the ground

        /* All collision checked objects should have a collider and a rb. 
           I'd also recommend putting non-simulated rb on the player      
           just as a fail safe if you're not preccious about performance. */
        private RayRange _raysUp, _raysRight, _raysDown, _raysLeft;
        private bool _colUp, _colRight, _colDown, _colLeft;                     // Used for std collision
        private bool _deathColUp, _deathColRight, _deathColDown, _deathColLeft; // Used for death collision
        private bool _movColUp, _movColRight, _movColDown, _movColLeft;         // Used to check if next move will place player inside solid object

        private float _timeLeftGrounded;

        private void RunCollisionChecks()
        {
            CalculateRayRanged();

            // Ground
            LandingThisFrame = false;
            var groundedCheck = RunSolidDetection(_raysDown);
            if (_colDown && !groundedCheck) _timeLeftGrounded = Time.time; // Only trigger when first leaving
            else if (!_colDown && groundedCheck)
            {
                // trigger when first touch
                _coyoteUsable = true;    
                LandingThisFrame = true;
            }

            _colDown = groundedCheck;

            // Run collision checks for;
            // Ground
            _colUp = RunSolidDetection(_raysUp);
            _colLeft = RunSolidDetection(_raysLeft);
            _colRight = RunSolidDetection(_raysRight);

            // Damage
            _deathColUp = RunDeathDetection(_raysUp);
            _deathColLeft = RunDeathDetection(_raysLeft);
            _deathColRight = RunDeathDetection(_raysRight);

            // moving platforms
            _movColDown = RunMovingPlatformDetection(_raysUp);
            _movColUp = RunMovingPlatformDetection(_raysUp);
            _movColLeft = RunMovingPlatformDetection(_raysLeft);
            _movColRight = RunMovingPlatformDetection(_raysRight);

            // DIEEEEEEEE
            if (_deathColDown || _deathColLeft || _deathColUp || _deathColRight)
            {
                gameObject.transform.position = StartPosition;
            }

            bool RunSolidDetection(RayRange range)
            {
                return EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, _detectionRayLength, _groundLayer));
            }

            bool RunDeathDetection(RayRange range)
            {
                return EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, _detectionRayLength, _deathLayer));
            }

            bool RunMovingPlatformDetection(RayRange range)
            {
                return EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, _detectionRayLength, _movingPlatformLayer));
            }
        }

        private void CalculateRayRanged()
        {
            var b = new Bounds(transform.position + _characterBounds.center, _characterBounds.size);

            _raysDown = new RayRange(b.min.x + _rayBuffer, b.min.y, b.max.x - _rayBuffer, b.min.y, Vector2.down);
            _raysUp = new RayRange(b.min.x + _rayBuffer, b.max.y, b.max.x - _rayBuffer, b.max.y, Vector2.up);
            _raysLeft = new RayRange(b.min.x, b.min.y + _rayBuffer, b.min.x, b.max.y - _rayBuffer, Vector2.left);
            _raysRight = new RayRange(b.max.x, b.min.y + _rayBuffer, b.max.x, b.max.y - _rayBuffer, Vector2.right);
        }


        private IEnumerable<Vector2> EvaluateRayPositions(RayRange range)
        {
            for (var i = 0; i < _detectorCount; i++)
            {
                var t = (float)i / (_detectorCount - 1);
                yield return Vector2.Lerp(range.Start, range.End, t);
            }
        }

        private void OnDrawGizmos()
        {
            // Bounds
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position + _characterBounds.center, _characterBounds.size);

            // Rays
            if (!Application.isPlaying)
            {
                CalculateRayRanged();
                Gizmos.color = Color.blue;
                foreach (var range in new List<RayRange> { _raysUp, _raysRight, _raysDown, _raysLeft })
                {
                    foreach (var point in EvaluateRayPositions(range))
                    {
                        Gizmos.DrawRay(point, range.Dir * _detectionRayLength);
                    }
                }
            }

            if (!Application.isPlaying) return;

            // Draw the future position. Handy for visualizing gravity
            Gizmos.color = Color.red;
            var move = new Vector3(_currentHorizontalSpeed, _currentVerticalSpeed) * Time.deltaTime;
            Gizmos.DrawWireCube(transform.position + _characterBounds.center + move, _characterBounds.size);
        }

    #endregion

    #region Walk

        [Header("WALKING")][SerializeField] 
        private float _acceleration = 90;                     // Walk accellaration
        [SerializeField] private float _moveClamp = 13;       // Fastest player can fall
        [SerializeField] private float _deAcceleration = 60f; // Decelaration when !Horizontal 
        [SerializeField] private float _apexBonus = 2;        // The time player has at apex of jump to control movement

        private void CalculateWalk()
        {
            if (Input.X != 0)
            {
                // Set horizontal move speed
                _currentHorizontalSpeed += Input.X * _acceleration * Time.deltaTime;

                // clamped by max frame movement
                _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed, -_moveClamp, _moveClamp);

                // Apply bonus at the apex of a jump
                var apexBonus = Mathf.Sign(Input.X) * _apexBonus * _apexPoint;
                _currentHorizontalSpeed += apexBonus * Time.deltaTime;
            }
            else
            {
                // No input. Let's slow the character down
                _currentHorizontalSpeed = Mathf.MoveTowards(_currentHorizontalSpeed, 0, _deAcceleration * Time.deltaTime);
            }

            if (_currentHorizontalSpeed > 0 && _colRight || _currentHorizontalSpeed < 0 && _colLeft)
            {
                // Don't walk through walls
                _currentHorizontalSpeed = 0;
            }
            if (_currentHorizontalSpeed > 0 && _movColRight || _currentHorizontalSpeed < 0 && _movColLeft)
            {
                // Don't walk through walls
                _currentHorizontalSpeed = 0;
            }
        }

    #endregion

    #region Gravity

        [Header("GRAVITY")]
        [SerializeField] private float _fallClamp = -40f;    
        [SerializeField] private float _minFallSpeed = 80f;
        [SerializeField] private float _maxFallSpeed = 120f;
        private float _fallSpeed; // Basically G

        private void CalculateGravity()
        {
            if (_colDown)
            {
                // Move out of the ground
                if (_currentVerticalSpeed < 0) _currentVerticalSpeed = 0;
            }
            else
            {
                // Add downward force while ascending if we ended the jump early
                var fallSpeed = _endedJumpEarly && _currentVerticalSpeed > 0 ? _fallSpeed * _jumpEndEarlyGravityModifier : _fallSpeed;

                // Fall
                _currentVerticalSpeed -= fallSpeed * Time.deltaTime;

                // Clamp
                if (_currentVerticalSpeed < _fallClamp) _currentVerticalSpeed = _fallClamp;
            }
        }

    #endregion

    #region Jump

    [Header("JUMPING")]
    [SerializeField] private float _jumpHeight = 30;
    [SerializeField] private float _jumpApexThreshold = 10f;
    [SerializeField] private float _coyoteTimeThreshold = 0.1f;
    [SerializeField] private float _jumpBuffer = 0.1f;
    [SerializeField] private float _jumpEndEarlyGravityModifier = 3;
    private bool _coyoteUsable;
    private bool _endedJumpEarly = true;
    private float _apexPoint; // Becomes 1 at the apex of a jump
    private float _lastJumpPressed;
    private bool CanUseCoyote => _coyoteUsable && !_colDown && _timeLeftGrounded + _coyoteTimeThreshold > Time.time;
    private bool HasBufferedJump => _colDown && _lastJumpPressed + _jumpBuffer > Time.time;

    private void CalculateJumpApex()
    {
        if (!_colDown)
        {
            // Gets stronger the closer to the top of the jump
            _apexPoint = Mathf.InverseLerp(_jumpApexThreshold, 0, Mathf.Abs(Velocity.y));
            _fallSpeed = Mathf.Lerp(_minFallSpeed, _maxFallSpeed, _apexPoint);
        }
        else
        {
            _apexPoint = 0;
        }
    }

    private void CalculateJump()
    {
        // Jump if: grounded or within coyote threshold || sufficient jump buffer
        if (Input.JumpDown && CanUseCoyote || HasBufferedJump)
        {
            _currentVerticalSpeed = _jumpHeight;
            _endedJumpEarly = false;
            _coyoteUsable = false;
            _timeLeftGrounded = float.MinValue;
            JumpingThisFrame = true;
        }
        else
        {
            JumpingThisFrame = false;
        }

        // End the jump early if button released
        if (!_colDown && Input.JumpUp && !_endedJumpEarly && Velocity.y > 0)
        {
            // _currentVerticalSpeed = 0;
            _endedJumpEarly = true;
        }
        if (_colUp /*Bumps Head*/)
        {
            if (_currentVerticalSpeed > 0) _currentVerticalSpeed = 0;
        }
        if (_movColUp /*Bumps Head on Moving Platform*/)
        {
            if (_currentVerticalSpeed > 0) _currentVerticalSpeed = 0;
        }
    }

    #endregion

    #region Dash
        [Header("DASH")]
        [SerializeField] public bool _facingRight;     // Used to find direction player is facing/last moved
        [SerializeField] public float _dashSpeed;      // Dashed speed
        [SerializeField] public float _dashLength;     // How long the dash is
        [SerializeField] public bool _dashing = false; // Is Dashing?
        float _dashTimer = 0;
        private void makeDash()
        {

            // Find what direction player is facing
            if (Input.X > 0)
                _facingRight = true;
            else if (Input.X < 0)
                _facingRight= false;

            // DASH
            if (Input.DashDown && canDash)
            {
                _dashTimer = _dashLength;

                if (!Grounded)
                {
                    _dashing = true;
                }
                canDash = false;

                if (_facingRight)
                    _currentHorizontalSpeed = _dashSpeed;
                else if (!_facingRight)
                    _currentHorizontalSpeed = -_dashSpeed;
            }

            // Times dash
            if (_dashTimer > 0)
            {
                _currentVerticalSpeed = 0;
                _dashTimer -= Time.deltaTime;
            }
            else
                _dashing = false;

        }

    #endregion

    #region Move

        [Header("MOVE")]
        private int _freeColliderIterations = 10;

        [SerializeField] private LayerMask _movingPlatformLayer;

        private Vector3 xOffset = new Vector2(0.375f, 0);
        private Vector3 yOffset = new Vector2(0, 0.625f);

        private Vector3 movPlatformVel;

        // We cast our bounds before moving to avoid future collisions
        private void MoveCharacter()
        {

            if (_currentHorizontalSpeed > 0 && _colRight || _currentHorizontalSpeed < 0 && _colLeft)
            {
                // Don't walk through walls
                _currentHorizontalSpeed = 0;
            }
            if (_currentHorizontalSpeed > 0 && _movColRight || _currentHorizontalSpeed < 0 && _movColLeft)
            {
                // Don't walk through walls
                _currentHorizontalSpeed = 0;
            }

            // Moving platform collision
            // Down
            RaycastHit2D downHit1 = Physics2D.Raycast(transform.position - xOffset, transform.TransformDirection(Vector2.down), 0.8f, _groundLayer.value);
            RaycastHit2D downHit2 = Physics2D.Raycast(transform.position, transform.TransformDirection(Vector2.down), 0.8f, _groundLayer.value);
            RaycastHit2D downHit3 = Physics2D.Raycast(transform.position + xOffset, transform.TransformDirection(Vector2.down), 0.8f, _groundLayer.value);

            // I dont want to explain my mistakes
            if (downHit1.collider != null)
            {
                try
                {
                    movPlatformVel = new Vector3(downHit1.collider.gameObject.GetComponent<MovingPlatforn>().dx, downHit1.collider.gameObject.GetComponent<MovingPlatforn>().dy);
                }
                catch
                { };
            }
            else if (downHit2.collider != null)
            {
                try
                {
                    movPlatformVel = new Vector3(downHit2.collider.gameObject.GetComponent<MovingPlatforn>().dx, downHit2.collider.gameObject.GetComponent<MovingPlatforn>().dy);
                }
                catch
                { };
            }
            else if (downHit3.collider != null)
            {
                try
                {
                    movPlatformVel = new Vector3(downHit3.collider.gameObject.GetComponent<MovingPlatforn>().dx, downHit3.collider.gameObject.GetComponent<MovingPlatforn>().dy);
                }
                catch
                { };
            }
            else
            {
                movPlatformVel = Vector3.zero;
            }

            var pos = transform.position + _characterBounds.center;
            RawMovement = new Vector3(_currentHorizontalSpeed, _currentVerticalSpeed); // Used externally
            var move = (RawMovement * Time.deltaTime) + (movPlatformVel);
            var furthestPoint = pos + move;

            // check furthest movement. If nothing hit, move and don't do extra checks
            var hit = Physics2D.OverlapBox(furthestPoint, _characterBounds.size, 0, _groundLayer);
            if (!hit)
            {
                transform.position += move;
                return;
            }

            // otherwise increment away from current pos; see what closest position we can move to
            var positionToMoveTo = transform.position;
            for (int i = 1; i < _freeColliderIterations; i++)
            {
                // increment to check all but furthestPoint
                var t = (float)i / _freeColliderIterations;
                var posToTry = Vector2.Lerp(pos, furthestPoint, t);

                if (Physics2D.OverlapBox(posToTry, _characterBounds.size, 0, _groundLayer))
                {
                    transform.position = positionToMoveTo;

                    // We've landed on a corner or hit our head on a ledge. Nudge the player gently
                    if (i == 1)
                    {
                        if (_currentVerticalSpeed < 0) _currentVerticalSpeed = 0;
                        var dir = transform.position - hit.transform.position;
                        transform.position += dir.normalized * move.magnitude;
                    }

                    return;
                }

                    positionToMoveTo = posToTry;
            }
        }



    #endregion

    #region Animations
    [Header("Animator")]

    public Animator animator;       // Attach players animator

    Animations PreviousAnimation;   // Stores lable of previous animation
    Animations CurrentAnimation = Animations.Idle;

    void Animator()
    {

    }

    public LayerMask animLayer;

    bool isPlaying(Animator anim, string stateName)
    {
        // Detects if current animarion is already playing (Used to prevent animation being cut off if same animation is called)
        if (anim.GetCurrentAnimatorStateInfo(animLayer).IsName(stateName) &&
                anim.GetCurrentAnimatorStateInfo(animLayer).normalizedTime < 1.0f)
            return true;
        else
            return false;
    }

    
    enum Animations
    {
        // Just some common player animations
        Idle,
        Run,
        JumpRising,
        JumpFalling,
        Dash,
    }

    #endregion
}
