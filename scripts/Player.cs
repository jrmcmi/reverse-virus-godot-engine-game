using Godot;

/// <summary>
/// Player.cs — Controls the human player character.
/// The player moves with WASD and can collect speed power-ups.
/// Touching a zombie "cures" it (removes it from the scene).
/// </summary>
public partial class Player : CharacterBody2D
{
    // ── Exported properties (editable in Godot Inspector) ──────────────────
    [Export] public float BaseSpeed = 150f;   // Normal movement speed
    [Export] public float BoostSpeed = 280f;  // Speed while power-up is active
    [Export] public float BoostDuration = 5f; // How long the speed boost lasts (seconds)

    // ── Internal state ─────────────────────────────────────────────────────
    private float _currentSpeed;
    private float _boostTimer = 0f;      // Countdown for speed boost
    private bool  _isBoosted  = false;

    // Reference to the HUD label that shows boost status
    private Label _boostLabel;

    public override void _Ready()
    {
        // Start at base speed
        _currentSpeed = BaseSpeed;

        // Grab the boost indicator label from the HUD (optional — won't crash if missing)
        _boostLabel = GetTree().Root.FindChild("BoostLabel", true, false) as Label;
    }

    public override void _PhysicsProcess(double delta)
    {
        // ── Handle speed-boost countdown ───────────────────────────────────
        if (_isBoosted)
        {
            _boostTimer -= (float)delta;
            if (_boostTimer <= 0f)
            {
                _isBoosted     = false;
                _currentSpeed  = BaseSpeed;
                if (_boostLabel != null) _boostLabel.Text = "";
            }
            else
            {
                if (_boostLabel != null)
                    _boostLabel.Text = $"⚡ BOOST {_boostTimer:F1}s";
            }
        }

        // ── Read WASD input and build a direction vector ───────────────────
        Vector2 direction = Vector2.Zero;
        if (Input.IsActionPressed("ui_right") || Input.IsKeyPressed(Key.D)) direction.X += 1;
        if (Input.IsActionPressed("ui_left")  || Input.IsKeyPressed(Key.A)) direction.X -= 1;
        if (Input.IsActionPressed("ui_down")  || Input.IsKeyPressed(Key.S)) direction.Y += 1;
        if (Input.IsActionPressed("ui_up")    || Input.IsKeyPressed(Key.W)) direction.Y -= 1;

        // Normalize so diagonal movement isn't faster
        if (direction.Length() > 0)
            direction = direction.Normalized();

        Velocity = direction * _currentSpeed;

        // ── Move and handle collisions (built-in Godot method) ─────────────
        MoveAndSlide();

        // ── Check for zombie collisions ────────────────────────────────────
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() is Zombie zombie)
            {
                zombie.GetCured(); // Cure the zombie!
            }
        }
    }

    /// <summary>
    /// Called by a PowerUp node when the player walks over it.
    /// </summary>
    public void ActivateBoost()
    {
        _isBoosted    = true;
        _boostTimer   = BoostDuration;
        _currentSpeed = BoostSpeed;
    }
}
