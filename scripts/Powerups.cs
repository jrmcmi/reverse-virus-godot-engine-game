using Godot;

/// <summary>
/// Powerups.cs
///
/// CHANGES:
///   • Each powerup now shows a Sprite2D using ground_grass_details.png
///     tinted per powerup type as a placeholder until real sprites are added.
///     To replace: change the texture on the Sprite2D child of each powerup's
///     scene instance, or override OnBuildSprite() in a subclass.
///   • Powerup-pickup sound fired via AudioManager on every pickup.
///   • Pulse animation kept on the Node2D parent (doesn't affect sprite scaling).
/// </summary>
public abstract partial class PowerupBase : Area2D
{   
    protected abstract Color TintColor    { get; }  // Modulate tint for placeholder sprite
    protected abstract float VisualRadius { get; }  // Collision radius

    private float _pulse;
    private Sprite2D _sprite;

    public override void _Ready()
    {
        // ── Sprite: ground_grass_details.png tinted per powerup type ──────
        var tex = GD.Load<Texture2D>("res://PNG/ac-logo.png");
        _sprite = new Sprite2D
        {
            Texture  = tex,
            Modulate = TintColor,
            Scale    = new Vector2(0.02f, 0.02f),  // Scale to fit ~30px radius
            ZIndex   = 5
        };
        AddChild(_sprite);

        // ── Collision ─────────────────────────────────────────────────────
        var col = new CollisionShape2D();
        col.Shape = new CircleShape2D { Radius = VisualRadius + 5f };
        AddChild(col);

        CollisionLayer = 0;
        CollisionMask  = 2;
        Monitoring     = true;
        Monitorable    = false;

        BodyEntered += b =>
        {
            if (b is Player p)
            {
                // Play powerup-pickup sound
                GameManager.Audio?.PlaySfx(GameManager.Audio?.PowerupStream);
                OnPlayerPickup(p);
                QueueFree();
            }
        };
    }

    public override void _Process(double delta)
    {
        // Gentle pulse on the whole node
        _pulse += (float)delta * 3.5f;
        float s = 1f + 0.16f * Mathf.Sin(_pulse);
        Scale = new Vector2(s, s);
    }

    protected abstract void OnPlayerPickup(Player player);
}

// ── Speed Boost — yellow tint ─────────────────────────────────────────────
public partial class SpeedBoostPowerup : PowerupBase
{
    protected override Color TintColor    => new Color(1.0f, 0.9f, 0.1f);   // Yellow
    protected override float VisualRadius => 18f;
    protected override void OnPlayerPickup(Player player) => player.ActivateSpeedBoost();
}

// ── Heal — green tint ─────────────────────────────────────────────────────
public partial class HealPowerup : PowerupBase
{
    [Export] public float HealAmount      = 30f;
    [Export] public float HumanHealRadius = 150f;

    protected override Color TintColor    => new Color(0.2f, 1.0f, 0.3f);   // Green
    protected override float VisualRadius => 18f;

    protected override void OnPlayerPickup(Player player)
    {
        if (player.CurrentHP < player.MaxHP) player.Heal(HealAmount);

        var snap = new System.Collections.Generic.List<Human>(GameManager.AllHumans);
        foreach (var h in snap)
        {
            if (!IsInstanceValid(h)) continue;
            if (player.GlobalPosition.DistanceTo(h.GlobalPosition) < HumanHealRadius)
                h.Heal();
        }
    }
}

// ── Shield — blue tint ────────────────────────────────────────────────────
public partial class ShieldPowerup : PowerupBase
{
    protected override Color TintColor    => new Color(0.3f, 0.6f, 1.0f);   // Blue
    protected override float VisualRadius => 18f;

    protected override void OnPlayerPickup(Player player)
    {
        Human target = null; float best = float.MaxValue;
        foreach (var h in GameManager.AllHumans)
        {
            if (!IsInstanceValid(h) || h.HasShield) continue;
            float d = player.GlobalPosition.DistanceSquaredTo(h.GlobalPosition);
            if (d < best) { best = d; target = h; }
        }

        if (target != null)
            target.ActivateShield();
        else
        {
            player.Modulate = new Color(0.4f, 0.7f, 2f);
            player.GetTree().CreateTimer(0.2f).Timeout += () =>
            { if (IsInstanceValid(player)) player.Modulate = Colors.White; };
        }
    }
}
