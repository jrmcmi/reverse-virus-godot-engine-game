using Godot;

// ═════════════════════════════════════════════════════════════════════════════
// ZombieTypes.cs — Sprinter, Pusher, Trapper
//
// All three use the same ZombieWalkFrames.tres sprite sheet (loaded in ZombieBase).
// Each type tints _sprite.Modulate in BuildVisual() to distinguish visually:
//   Sprinter → red-orange tint
//   Pusher   → blue tint
//   Trapper  → green tint
//
// BUGS FIXED vs previous version:
//   • Sprinter chase logic restored (detect radius → chase player / humans)
//   • All speeds were 20 → corrected to playable values
//   • Pusher now deals TakeDamage on push (not just ApplyKnockback)
//   • Trapper ChaseChance was 20 (always chasing) → 0.25 (25%)
//   • UpdateZombieAnim() called at end of every MoveLogic() path
// ═════════════════════════════════════════════════════════════════════════════


// ── Sprinter ──────────────────────────────────────────────────────────────
// Red tint. Fastest type.
// Solo mode: wanders; chases player when inside DetectRadius.
// Protect mode: chases nearest human regardless of distance.
// ─────────────────────────────────────────────────────────────────────────

public partial class Sprinter : ZombieBase
{
    [Export] public float ChaseSpeed   = 30f;
    [Export] public float RoamSpeed    = 30f;
    [Export] public float DetectRadius = 70f;

    private Vector2 _roamDir;
    private float   _roamTimer;
    private float   _roamCooldown;

    // Red-orange tint to distinguish Sprinters visually
    protected override void BuildVisual()
        => _sprite.Modulate = new Color(1.0f, 0.45f, 0.35f);

    public override void _Ready()
    {
        MaxHP         = 80f;
        ContactDamage = 10f;
        base._Ready();
        PickRoam();
    }

    protected override void MoveLogic(float delta)
    {
        _roamTimer    -= delta;
        _roamCooldown -= delta;

        // Protect mode — humans exist: chase nearest human
        if (GameManager.AllHumans.Count > 0)
        {
            var h = FindNearestHuman();
            if (h != null)
            {
                MoveToward(h.GlobalPosition, ChaseSpeed);
                UpdateZombieAnim();
                return;
            }
        }

        // Solo mode — player inside detect radius: chase player
        var player = GameManager.PlayerNode;
        if (player != null)
        {
            float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
            if (dist < DetectRadius)
            {
                MoveToward(player.GlobalPosition, ChaseSpeed);
                UpdateZombieAnim();
                return;
            }
        }

        // Roam — change direction when near wall to avoid corner lock
        if (_roamTimer <= 0f || (IsNearWall() && _roamCooldown <= 0f))
        {
            PickRoam();
            if (IsNearWall()) _roamCooldown = 0.8f;
        }
        Velocity = WallAvoid(_roamDir) * RoamSpeed * SpeedMult;
        UpdateZombieAnim();
    }

    private void PickRoam()
    {
        float a    = Rng.RandfRange(0f, Mathf.Tau);
        _roamDir   = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        _roamTimer = Rng.RandfRange(1.8f, 4f);
    }
}


// ── Pusher ────────────────────────────────────────────────────────────────
// Blue tint. Approaches player in solo mode and launches a knockback impulse.
// Pushback DISABLED in protect mode — chases humans instead.
// Deals contact damage AND extra knockback damage on push hit.
// ─────────────────────────────────────────────────────────────────────────

public partial class Pusher : ZombieBase
{
    [Export] public float ChaseSpeed    = 30f;
    [Export] public float RoamSpeed     = 30f;
    [Export] public float ApproachSpeed = 30f;
    [Export] public float PushRadius    = 50f;
    [Export] public float PushForce     = 200f;
    [Export] public float PushCooldown  = 10f;
    [Export] public float DetectRadius  = 70f;

    private float   _pushTimer;
    private Vector2 _roamDir;
    private float   _roamTimer;
    private float   _roamCooldown;

    protected override void BuildVisual()
        => _sprite.Modulate = new Color(0.55f, 0.7f, 1.0f);  // Blue tint

    public override void _Ready()
    {
        MaxHP         = 120f;
        ContactDamage = 14f;
        base._Ready();
        _pushTimer = 0.8f;
        PickRoam();
    }

    protected override void MoveLogic(float delta)
    {
        _pushTimer    -= delta;
        _roamTimer    -= delta;
        _roamCooldown -= delta;

        // Protect mode: chase nearest human (no push)
        if (GameManager.AllHumans.Count > 0)
        {
            var h = FindNearestHuman();
            if (h != null)
            {
                MoveToward(h.GlobalPosition, ChaseSpeed);
                UpdateZombieAnim();
                return;
            }
        }

        // Solo mode: approach + push
        var player = GameManager.PlayerNode;
        if (player != null)
        {
            float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
            if (dist < DetectRadius)
            {
                // Inside push range → launch push impulse
                if (dist < PushRadius && _pushTimer <= 0f)
                {
                    _pushTimer = PushCooldown;
                    var pushDir = (player.GlobalPosition - GlobalPosition).Normalized();
                    float scale = 1f - dist / PushRadius;
                    player.ApplyKnockback(pushDir * PushForce * (1f + scale));
                    // FIX: also deal damage on push (was missing)
                    player.TakeDamage(ContactDamage * 1.4f);
                    Velocity -= pushDir * PushForce * 0.1f; // small recoil
                    UpdateZombieAnim();
                    return;
                }
                // Move toward player to set up the push
                MoveToward(player.GlobalPosition, ApproachSpeed);
                UpdateZombieAnim();
                return;
            }
        }

        // Roam when far
        if (_roamTimer <= 0f || (IsNearWall() && _roamCooldown <= 0f))
        {
            PickRoam();
            if (IsNearWall()) _roamCooldown = 1f;
        }
        Velocity = WallAvoid(_roamDir) * RoamSpeed * SpeedMult;
        UpdateZombieAnim();
    }

    private void PickRoam()
    {
        float a    = Rng.RandfRange(0f, Mathf.Tau);
        _roamDir   = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        _roamTimer = Rng.RandfRange(2f, 5f);
    }
}


// ── Trapper ───────────────────────────────────────────────────────────────
// Green tint. Slow roamer that drops Trap Area2Ds periodically.
// 25% chance to chase nearest human when humans exist.
// Traps work in BOTH solo and protect mode.
// ─────────────────────────────────────────────────────────────────────────

public partial class Trapper : ZombieBase
{
    [Export] public float RoamSpeed    = 30f;
    [Export] public float ChaseSpeed   = 30f;
    [Export] public float TrapInterval = 10f;
    [Export] public float ChaseChance  = 0.25f;   // FIX: was 20f (always chasing)

    private float   _trapTimer;
    private Vector2 _roamDir;
    private float   _roamTimer;
    private float   _roamCooldown;
    private bool    _isChasing;
    private float   _stateTimer;

    protected override void BuildVisual()
        => _sprite.Modulate = new Color(0.55f, 1.0f, 0.55f);  // Green tint

    public override void _Ready()
    {
        MaxHP         = 90f;
        ContactDamage = 8f;
        base._Ready();
        _trapTimer = TrapInterval * 0.4f;  // Drop first trap quickly
        PickRoam();
        DecideState();
    }

    protected override void MoveLogic(float delta)
    {
        _trapTimer    -= delta;
        _roamTimer    -= delta;
        _stateTimer   -= delta;
        _roamCooldown -= delta;

        // Drop trap at current position (works in BOTH solo and protect mode)
        if (_trapTimer <= 0f)
        {
            DropTrap();
            _trapTimer = TrapInterval;
        }

        // Re-evaluate chase state
        if (_stateTimer <= 0f) DecideState();

        // Chase state — move toward nearest human
        if (_isChasing)
        {
            var t = FindNearestHuman();
            if (t != null)
            {
                MoveToward(t.GlobalPosition, ChaseSpeed);
                UpdateZombieAnim();
                return;
            }
            _isChasing = false;
        }

        // Roam with anti-corner direction changes
        if (_roamTimer <= 0f || (IsNearWall() && _roamCooldown <= 0f))
        {
            PickRoam();
            if (IsNearWall()) _roamCooldown = 0.9f;
        }
        Velocity = WallAvoid(_roamDir) * RoamSpeed * SpeedMult;
        UpdateZombieAnim();
    }

    private void DecideState()
    {
        // FIX: ChaseChance is 0.25 (25%) — was 20 causing always-chase
        _isChasing  = GameManager.AllHumans.Count > 0 && Rng.Randf() < ChaseChance;
        _stateTimer = Rng.RandfRange(2.5f, 5f);
    }

    private void DropTrap()
    {
        if (!IsInstanceValid(GameManager.Instance)) return;
        var trap = new Trap();
        trap.GlobalPosition = GlobalPosition;
        GameManager.Instance.AddChild(trap);
    }

    private void PickRoam()
    {
        float a    = Rng.RandfRange(0f, Mathf.Tau);
        _roamDir   = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        _roamTimer = Rng.RandfRange(2f, 5f);
    }
}
