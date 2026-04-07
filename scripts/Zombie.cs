using Godot;

/// <summary>
/// Zombie.cs — Controls each zombie NPC.
/// Zombies flee from the player at all times.
/// When touched by the player they are "cured" and removed.
/// </summary>
public partial class Zombie : CharacterBody2D
{
    // ── Exported properties ────────────────────────────────────────────────
    [Export] public float FleeSpeed    = 100f;  // How fast the zombie runs away
    [Export] public float DetectRadius = 300f;  // Distance at which zombie starts fleeing

    // Reference to the player — found at runtime
    private CharacterBody2D _player;

    // Boundary of the play field (set by Main scene after spawning)
    public Rect2 FieldBounds;

    // Random number generator for wandering when player is far
    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
    private Vector2 _wanderDir = Vector2.Zero;
    private float   _wanderTimer = 0f;

    public override void _Ready()
    {
        _rng.Randomize();
        // Find the player node by group tag
        var players = GetTree().GetNodesInGroup("player");
        if (players.Count > 0)
            _player = players[0] as CharacterBody2D;

        // Start with a random wander direction
        PickNewWanderDir();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null) return;

        Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
        float   distance = toPlayer.Length();

        Vector2 moveDir;

        if (distance < DetectRadius)
        {
            // ── Flee: move directly AWAY from the player ──────────────────
            moveDir = -toPlayer.Normalized();
        }
        else
        {
            // ── Wander randomly when player is far ────────────────────────
            _wanderTimer -= (float)delta;
            if (_wanderTimer <= 0f)
                PickNewWanderDir();
            moveDir = _wanderDir;
        }

        Velocity = moveDir * FleeSpeed;
        MoveAndSlide();

        // ── Clamp position inside the field so zombies don't escape ───────
        Vector2 pos = GlobalPosition;
        pos.X = Mathf.Clamp(pos.X, FieldBounds.Position.X + 20, FieldBounds.End.X - 20);
        pos.Y = Mathf.Clamp(pos.Y, FieldBounds.Position.Y + 20, FieldBounds.End.Y - 20);
        GlobalPosition = pos;
    }

    /// <summary>
    /// Called when the player touches this zombie.
    /// Signals the Main scene to decrease the zombie count, then removes self.
    /// </summary>
    public void GetCured()
    {
        // Notify Main scene to update the counter
        var main = GetTree().Root.GetNodeOrNull<Main>("Main");
        main?.OnZombieCured();

        QueueFree(); // Remove this zombie from the scene
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private void PickNewWanderDir()
    {
        float angle  = _rng.RandfRange(0, Mathf.Tau);
        _wanderDir   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        _wanderTimer = _rng.RandfRange(1f, 3f);
    }
}
