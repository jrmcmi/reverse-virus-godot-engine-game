using Godot;

/// <summary>
/// PowerUp.cs — A speed-boost collectible that spawns on the field.
/// When the player walks over it, their speed is temporarily increased.
/// The power-up then disappears.
/// </summary>
public partial class PowerUp : Area2D
{
    // Simple pulse animation so the power-up is easy to spot
    private float _pulseTime = 0f;
    private CollisionShape2D _shape;

    public override void _Ready()
    {
        _shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

        // Connect the body_entered signal so we detect the player stepping on it
        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        // Gentle scale pulse so the icon bounces
        _pulseTime += (float)delta * 3f;
        float scale = 1f + 0.15f * Mathf.Sin(_pulseTime);
        Scale = new Vector2(scale, scale);
    }

    /// <summary>
    /// Triggered when any physics body overlaps this Area2D.
    /// </summary>
    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            player.ActivateBoost(); // Give the player a speed boost
            QueueFree();            // Remove the power-up from the scene
        }
    }
}
