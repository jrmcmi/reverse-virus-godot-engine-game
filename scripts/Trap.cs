using Godot;
using System.Collections.Generic;

public partial class Trap : Area2D
{
    [Export] public float SlowFactor = 0.45f;
    [Export] public float Lifetime   = 20f;

    private float   _timer;
    private Sprite2D _sprite;

    private readonly HashSet<Node> _slowed = new();

    public override void _Ready()
    {
        _timer = Lifetime;

        // ── Visual: placeholder sprite using ground_grass_details.png ──────
        // Tinted red-orange to look like a danger marker
        // Replace texture when you have a real trap sprite
        var tex = GD.Load<Texture2D>("res://PNG/traps.png");
        _sprite = new Sprite2D
        {
            Texture  = tex,
            Scale    = new Vector2(0.52f, 0.52f),
            ZIndex   = 0   // Below characters so it reads as a ground marking
        };
        AddChild(_sprite);

        // ── Collision area ────────────────────────────────────────────────
        var col = new CollisionShape2D();
        col.Shape = new CircleShape2D { Radius = 26f };
        AddChild(col);

        CollisionLayer = 0;
        CollisionMask  = 2 | 8;   // Player (layer 2) + Human (layer 8)
        Monitoring     = true;
        Monitorable    = false;

        BodyEntered += OnEnter;
        BodyExited  += OnExit;
    }

    public override void _Process(double delta)
    {
        _timer -= (float)delta;
        // Fade out as lifetime approaches zero
        float alpha = Mathf.Clamp(_timer / Lifetime, 0f, 1f);
        _sprite.Modulate = new Color(1.0f, 0.25f, 0.1f, 0.75f * alpha);
        if (_timer <= 0f) QueueFree();
    }

    public override void _ExitTree()
    {
        // Clean up all active slows so no entity is permanently stuck
        foreach (var body in _slowed)
        {
            if (!IsInstanceValid(body)) continue;
            if (body is Player p) p.RemoveSlow();
            if (body is Human  h) h.RemoveSlow();
        }
        _slowed.Clear();
    }

    private void OnEnter(Node2D body)
    {
        if      (body is Player player && _slowed.Add(player)) player.ApplySlow(SlowFactor);
        else if (body is Human  human  && _slowed.Add(human))  human.ApplySlow(SlowFactor);
    }

    private void OnExit(Node2D body)
    {
        if (!_slowed.Remove(body)) return;
        if (body is Player p) p.RemoveSlow();
        if (body is Human  h) h.RemoveSlow();
    }
}
