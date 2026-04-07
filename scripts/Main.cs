using Godot;

/// <summary>
/// Main.cs — Orchestrates the entire game.
/// Spawns zombies and power-ups, manages the timer,
/// tracks how many zombies remain, and handles win/lose states.
/// </summary>
public partial class Main : Node2D
{
	// ── Tuneable game settings ─────────────────────────────────────────────
	[Export] public int   ZombieCount       = 8;    // How many zombies to spawn
	[Export] public int   PowerUpCount      = 3;    // Power-ups on the field at start
	[Export] public float GameTime          = 60f;  // Seconds to cure all zombies

	// ── Field dimensions ───────────────────────────────────────────────────
	private const float FieldX      = 80f;
	private const float FieldY      = 80f;
	private const float FieldWidth  = 860f;
	private const float FieldHeight = 500f;
	private Rect2 _fieldBounds;

	// ── Runtime state ──────────────────────────────────────────────────────
	private float _timeLeft;
	private int   _zombiesLeft;
	private bool  _gameOver = false;

	// ── UI node references ─────────────────────────────────────────────────
	private Label _timerLabel;
	private Label _zombieLabel;
	private Label _boostLabel;
	private Label _resultLabel;

	// ── Random number generator for spawn positions ────────────────────────
	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		_rng.Randomize();
		_fieldBounds = new Rect2(FieldX, FieldY, FieldWidth, FieldHeight);
		_timeLeft    = GameTime;
		_zombiesLeft = ZombieCount;

		// ── Build UI Labels programmatically ──────────────────────────────
		_timerLabel  = MakeLabel("TimerLabel",  "Time: 60",  new Vector2(FieldX, 20));
		_zombieLabel = MakeLabel("ZombieLabel", $"Zombies: {ZombieCount}", new Vector2(FieldX + 200, 20));
		_boostLabel  = MakeLabel("BoostLabel",  "",          new Vector2(FieldX + 460, 20));
		_resultLabel = MakeLabel("ResultLabel", "",          new Vector2(FieldX + FieldWidth / 2 - 150, FieldY + FieldHeight / 2 - 30));
		_resultLabel.AddThemeFontSizeOverride("font_size", 36);
		_resultLabel.Modulate = new Color(1, 1, 0);

		// ── Draw the play field border (via a ColorRect) ───────────────────
		var border = new ColorRect();
		border.Color       = new Color(0.2f, 0.6f, 0.2f, 0.25f);
		border.Position    = new Vector2(FieldX, FieldY);
		border.Size        = new Vector2(FieldWidth, FieldHeight);
		AddChild(border);

		// ── Spawn Player ───────────────────────────────────────────────────
		SpawnPlayer();

		// ── Spawn Zombies ──────────────────────────────────────────────────
		for (int i = 0; i < ZombieCount; i++)
			SpawnZombie();

		// ── Spawn Power-Ups ────────────────────────────────────────────────
		for (int i = 0; i < PowerUpCount; i++)
			SpawnPowerUp();
	}

	public override void _Process(double delta)
	{
		if (_gameOver) return;

		// ── Tick the countdown timer ───────────────────────────────────────
		_timeLeft -= (float)delta;
		_timerLabel.Text = $"Time: {Mathf.Max(0, _timeLeft):F1}";

		if (_timeLeft <= 0f)
		{
			EndGame(false); // Ran out of time — lose
		}
	}

	/// <summary>
	/// Called by Zombie.GetCured() each time a zombie is healed.
	/// </summary>
	public void OnZombieCured()
	{
		_zombiesLeft--;
		_zombieLabel.Text = $"Zombies: {_zombiesLeft}";

		if (_zombiesLeft <= 0)
			EndGame(true); // All cured — win!
	}

	// ── Win / Lose ─────────────────────────────────────────────────────────
	private void EndGame(bool won)
	{
		_gameOver = true;
		_resultLabel.Text = won
			? "🎉 YOU WIN!\nAll zombies cured!"
			: "💀 TIME'S UP!\nPress R to restart";

		// Pause physics so everyone freezes
		GetTree().Paused = true;
	}

	public override void _Input(InputEvent @event)
	{
		// Press R to restart
		if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
		{
			GetTree().Paused = false;
			GetTree().ReloadCurrentScene();
		}
	}

	// ── Spawn helpers ──────────────────────────────────────────────────────

	private void SpawnPlayer()
	{
		var player = new Player();
		player.AddToGroup("player");
		player.Position = new Vector2(FieldX + FieldWidth / 2, FieldY + FieldHeight / 2);

		// Visual: green circle
		var circle = MakeCircleShape(18f, new Color(0.2f, 0.9f, 0.3f));
		player.AddChild(circle);

		// Collision shape
		var col = new CollisionShape2D();
		col.Shape = new CircleShape2D { Radius = 18f };
		player.AddChild(col);

		AddChild(player);
	}

	private void SpawnZombie()
	{
		var zombie = new Zombie();
		zombie.FieldBounds = _fieldBounds;
		zombie.Position    = RandomFieldPosition(margin: 30f);

		// Visual: purple/grey rectangle
		var rect = new ColorRect();
		rect.Color    = new Color(0.55f, 0.2f, 0.7f);
		rect.Size     = new Vector2(28, 28);
		rect.Position = new Vector2(-14, -14); // Centre the rect on the node origin
		zombie.AddChild(rect);

		// Collision shape
		var col = new CollisionShape2D();
		col.Shape = new RectangleShape2D { Size = new Vector2(28, 28) };
		zombie.AddChild(col);

		AddChild(zombie);
	}

	private void SpawnPowerUp()
	{
		var pu = new PowerUp();
		pu.Position = RandomFieldPosition(margin: 40f);

		// Visual: yellow star-ish polygon (drawn as a small circle for simplicity)
		var gfx = MakeCircleShape(12f, new Color(1f, 0.85f, 0f));
		pu.AddChild(gfx);

		// Collision (Area2D needs a CollisionShape2D)
		var col = new CollisionShape2D();
		col.Shape = new CircleShape2D { Radius = 14f };
		pu.AddChild(col);

		AddChild(pu);
	}

	// ── Utility ────────────────────────────────────────────────────────────

	/// <summary>Returns a random position inside the field bounds.</summary>
	private Vector2 RandomFieldPosition(float margin = 20f)
	{
		return new Vector2(
			_rng.RandfRange(FieldX + margin, FieldX + FieldWidth  - margin),
			_rng.RandfRange(FieldY + margin, FieldY + FieldHeight - margin)
		);
	}

	/// <summary>Creates a Node2D that draws a filled circle via a Polygon2D.</summary>
	private static Node2D MakeCircleShape(float radius, Color color, int segments = 24)
	{
		var poly    = new Polygon2D();
		poly.Color  = color;
		var points  = new Vector2[segments];
		for (int i = 0; i < segments; i++)
		{
			float angle = i * Mathf.Tau / segments;
			points[i]   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}
		poly.Polygon = points;
		return poly;
	}

	private Label MakeLabel(string name, string text, Vector2 pos)
	{
		var lbl        = new Label();
		lbl.Name       = name;
		lbl.Text       = text;
		lbl.Position   = pos;
		lbl.AddThemeFontSizeOverride("font_size", 20);
		AddChild(lbl);
		return lbl;
	}
}
