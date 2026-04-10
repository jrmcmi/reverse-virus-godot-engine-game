using Godot;
using System.Collections.Generic;

public partial class GameManager : Node2D
{
    public static GameManager Instance { get; private set; }

    [Export] public ReferenceRect PlayArea;
    [Export] public Area2D        SafezoneArea;
    [Export] public NodePath      ZombieSpawnRoot;
    [Export] public NodePath      PlayerSpawnMarker;

    [Export] public float GameDuration       = 300f;
    [Export] public int   InitialZombieCount = 50;
    [Export] public int   PowerUpCount       = 10;
    [Export] public float MultiplyInterval   = 60f;
    [Export] public float DifficultyInterval = 30f;
    [Export] public float SpeedScalePerStep  = 0.06f;

    // ── HUD customisation ─────────────────────────────────────────────────
    [Export] public int   TimerFontSize   = 38;
    [Export] public int   StatFontSize    = 18;
    [Export] public int   HintFontSize    = 13;
    [Export] public int   StatusFontSize  = 16;
    [Export] public Color TimerColor      = new Color(1f,   0.95f, 0.35f);
    [Export] public Color ZombieColor     = new Color(0.95f,0.4f,  0.4f);
    [Export] public Color HumanColor      = new Color(0.95f,0.85f, 0.2f);
    [Export] public Color SavedColor      = new Color(0.3f, 0.92f, 0.5f);
    [Export] public Color HintColor       = new Color(0.65f,0.65f, 0.65f);
    [Export] public Color StatusColor     = new Color(0.5f, 0.88f, 1f);

    public static Rect2  Field          { get; private set; }
    public static Rect2  SafezoneBounds { get; private set; }
    public static float  ZombieSpeedMult { get; private set; }

    public static readonly List<ZombieBase> AllZombies = new();
    public static readonly List<Human>      AllHumans  = new();
    public static Player PlayerNode { get; private set; }

    public float TimeLeft => _timeLeft;
    private float _timeLeft, _multiplyTimer, _difficultyTimer;
    private bool  _gameOver;
    private int   _humansSaved;

    private Label _timerLabel, _zombieLabel, _humanLabel, _savedLabel,
                  _resultLabel, _dashLabel;
    private ColorRect _resultPanel;
    public  Label BoostLabel => _dashLabel;

    private readonly RandomNumberGenerator _rng = new();
    private const float SpawnMargin = 70f;
    private const float SW_Sprinter = 0.40f, SW_Pusher = 0.35f;
    private AudioManager _audio;

    public override void _Ready()
    {
        Instance = this; ZombieSpeedMult = 1f;
        AllZombies.Clear(); AllHumans.Clear();
        _rng.Randomize();
        _timeLeft = GameDuration; _multiplyTimer = MultiplyInterval; _difficultyTimer = DifficultyInterval;
        _humansSaved = 0; _gameOver = false;

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Maximized);

        Field = PlayArea != null
            ? new Rect2(PlayArea.GlobalPosition, PlayArea.Size)
            : new Rect2(0, 58f, GetViewportRect().Size.X, GetViewportRect().Size.Y - 58f);

        SafezoneBounds = SafezoneArea != null ? ReadArea2DBounds(SafezoneArea) : new Rect2(-9999,-9999,1,1);

        _audio = new AudioManager(); AddChild(_audio);
        BuildHud(GetViewportRect().Size);
        SpawnPlayer(); SpawnInitialZombies(); SpawnPowerUps();
    }

    public override void _Process(double delta)
    {
        if (_gameOver) return;
        float dt = (float)delta;
        _timeLeft -= dt; _multiplyTimer -= dt; _difficultyTimer -= dt;
        int m = Mathf.Max(0,(int)_timeLeft/60), s = Mathf.Max(0,(int)_timeLeft%60);
        _timerLabel.Text  = $"{m}:{s:00}";
        _zombieLabel.Text = $"Zombies:  {AllZombies.Count}";
        _humanLabel.Text  = $"Escorting:  {AllHumans.Count}";
        _savedLabel.Text  = $"Saved:  {_humansSaved}";
        if (_multiplyTimer <= 0f) { _multiplyTimer = MultiplyInterval; MultiplyZombie(); }
        if (_difficultyTimer <= 0f) { _difficultyTimer = DifficultyInterval; ZombieSpeedMult += SpeedScalePerStep; }
        if (_timeLeft < GameDuration - 3f && AllZombies.Count == 0 && AllHumans.Count == 0 && _humansSaved > 0) { EndGame(true); return; }
        if (_timeLeft <= 0f) { EndGame(false); return; }
    }

    // FIX: Use _UnhandledInput so it fires even when paused
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;
        if (k.Keycode == Key.R)
        {
            // MUST unpause before reloading — paused tree blocks _Ready
            GetTree().Paused = false;
            GetTree().ReloadCurrentScene();
            return;
        }
        if (k.Keycode == Key.Escape) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        if (k.Keycode == Key.F)
        {
            var cur = DisplayServer.WindowGetMode();
            DisplayServer.WindowSetMode(cur == DisplayServer.WindowMode.Maximized
                ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Maximized);
        }
    }

    public static void RegisterZombie(ZombieBase z) { AllZombies.Add(z); Instance?._audio?.RegisterZombieAudio(z); }
    public static void UnregisterZombie(ZombieBase z) { AllZombies.Remove(z); Instance?._audio?.UnregisterZombieAudio(z); }
    public static void RegisterHuman(Human h)   => AllHumans.Add(h);
    public static void UnregisterHuman(Human h) => AllHumans.Remove(h);

    public void OnHumanSaved() { _humansSaved++; _audio?.PlaySfx(_audio.SavedStream); }
    public void OnZombieCured() { _audio?.PlaySfx(_audio.ConvertedStream); }
    public void PlayerDied()   => EndGame(false);
    public static AudioManager Audio => Instance?._audio;

    private void EndGame(bool won)
    {
        if (_gameOver) return; _gameOver = true;
        _resultPanel.Visible = true;
        _resultLabel.Text = won
            ? $"YOU WIN!\n{_humansSaved} humans saved\n\nR  to play again"
            : "GAME OVER\n\nR  to try again";
        _audio?.PlayResultMusic(won);
        // FIX: set ProcessMode so _UnhandledInput still fires while paused
        ProcessMode = ProcessModeEnum.Always;
        GetTree().Paused = true;
    }

    private void SpawnPlayer()
    {
        var player = new Player(); player.AddToGroup("player");
        if (!string.IsNullOrEmpty(PlayerSpawnMarker?.ToString()) && GetNodeOrNull(PlayerSpawnMarker) is Node2D m) player.Position = m.GlobalPosition;
        else player.Position = new Vector2(Field.Position.X + Field.Size.X * 0.15f, Field.GetCenter().Y);
        AddChild(player); PlayerNode = player;
    }

    private void SpawnInitialZombies()
    {
        var pts = new List<Vector2>();
        if (!string.IsNullOrEmpty(ZombieSpawnRoot?.ToString())) { var root = GetNodeOrNull(ZombieSpawnRoot); if (root != null) foreach (Node c in root.GetChildren()) if (c is Node2D n) pts.Add(n.GlobalPosition); }
        for (int i = 0; i < InitialZombieCount; i++)
        {
            Vector2 pos = pts.Count > 0 ? pts[_rng.RandiRange(0,pts.Count-1)] + new Vector2(_rng.RandfRange(-30f,30f),_rng.RandfRange(-30f,30f)) : RandomFieldPos(SpawnMargin);
            AddChild(MakeZombie(ClampToField(pos, 30f)));
        }
    }

    private void SpawnPowerUps()
    {
        PowerupBase[] types = { new SpeedBoostPowerup(), new HealPowerup(), new ShieldPowerup(), new SpeedBoostPowerup(), new HealPowerup() };
        for (int i = 0; i < PowerUpCount && i < types.Length; i++) { types[i].Position = RandomFieldPos(SpawnMargin); AddChild(types[i]); }
    }

    private void MultiplyZombie()
    {
        if (AllZombies.Count == 0) return;
        var src = AllZombies[_rng.RandiRange(0,AllZombies.Count-1)];
        if (!IsInstanceValid(src)) return;
        ZombieBase clone = src switch { Sprinter => new Sprinter(), Pusher => new Pusher(), _ => new Trapper() };
        clone.Position = ClampToField(src.GlobalPosition + new Vector2(_rng.RandfRange(-70f,70f),_rng.RandfRange(-70f,70f)), 25f);
        AddChild(clone);
    }

    private ZombieBase MakeZombie(Vector2 pos)
    {
        float r = _rng.Randf();
        ZombieBase z = r < SW_Sprinter ? new Sprinter() : r < SW_Sprinter + SW_Pusher ? new Pusher() : new Trapper();
        z.Position = pos; return z;
    }

    private void BuildHud(Vector2 vp)
    {
        const float HH = 58f;
        float midY = (HH - TimerFontSize) / 2f;
        float statY = (HH - StatFontSize) / 2f;

        // ── Timer — moved up ────────────────────────────────────────────────
        // Subtracting an extra 10-15 pixels from midY moves it higher
        float upwardOffset = 8f; 
        _timerLabel = Lbl("Timer", "5:00", new(vp.X / 2f - 45f, midY - upwardOffset), TimerFontSize, TimerColor);

        // ── Stats — horizontal row at top LEFT ──────────────────────────────────
        // Using a consistent Y (statY) and incrementing X for horizontal spacing
        float startX = 17f;
        float spacingX = 160f; // Adjust this value to increase/decrease gap between stats

        _zombieLabel = Lbl("Zombies", "Zombies: 0",    new(startX, statY), StatFontSize, ZombieColor);
        _humanLabel  = Lbl("Humans",  "Escorting: 0",  new(startX + spacingX, statY), StatFontSize, HumanColor);
        _savedLabel  = Lbl("Saved",   "Saved: 0",      new(startX + (spacingX * 2), statY), StatFontSize, SavedColor);

        // ── Controls — top RIGHT ──────────────────────────────────────────
        var hint = Lbl("Hint", "WASD move  |  SPACE dash  |  LMB attack\nF fullscreen  |  R restart", new(vp.X - 360f, 4f), HintFontSize, HintColor);
        hint.AutowrapMode = TextServer.AutowrapMode.Off;

        // ── Dash/boost status — below timer, centred ──────────────────────
        _dashLabel = Lbl("Boost", "", new(vp.X/2f - 80f, HH + 4f), StatusFontSize, StatusColor);
        _dashLabel.ZIndex = 45;

        // ── Result overlay ────────────────────────────────────────────────
        _resultPanel = new ColorRect { Color=new Color(0,0,0,0.74f), Position=new(vp.X/2f-300f,vp.Y/2f-120f), Size=new(600f,250f), ZIndex=90, Visible=false };
        AddChild(_resultPanel);
        _resultLabel = new Label { Name="Result", Text="", Position=new(vp.X/2f-240f,vp.Y/2f-90f), ZIndex=100 };
        _resultLabel.AddThemeFontSizeOverride("font_size", 40);
        _resultLabel.AddThemeColorOverride("font_color", new Color(1f,0.95f,0.15f));
        AddChild(_resultLabel);
    }

    public Vector2 RandomFieldPos(float margin)
    {
        float maxX = Mathf.Min(Field.End.X - margin, SafezoneBounds.Position.X - margin);
        return new(_rng.RandfRange(Field.Position.X+margin,maxX), _rng.RandfRange(Field.Position.Y+margin,Field.End.Y-margin));
    }

    public static Vector2 ClampToField(Vector2 pos, float margin = 15f)
    {
        var b = Field;
        return new(Mathf.Clamp(pos.X,b.Position.X+margin,b.End.X-margin), Mathf.Clamp(pos.Y,b.Position.Y+margin,b.End.Y-margin));
    }

    public static Rect2 ReadArea2DBounds(Area2D area)
    {
        foreach (Node child in area.GetChildren())
        {
            if (child is not CollisionShape2D cs) continue;
            if (cs.Shape is RectangleShape2D r) { var h = r.Size/2f; return new Rect2(area.GlobalPosition+cs.Position-h, r.Size); }
            if (cs.Shape is CircleShape2D c) { float rad=c.Radius; return new Rect2(area.GlobalPosition+cs.Position-new Vector2(rad,rad), new Vector2(rad*2f,rad*2f)); }
        }
        return new Rect2(area.GlobalPosition, new Vector2(120f,960f));
    }

    public static Polygon2D CirclePoly(float r, Color col, int segs = 28)
    {
        var poly = new Polygon2D { Color = col }; var pts = new Vector2[segs];
        for (int i = 0; i < segs; i++) { float a = i*Mathf.Tau/segs; pts[i] = new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r; }
        poly.Polygon = pts; return poly;
    }

    private Label Lbl(string name, string text, Vector2 pos, int size, Color color)
    {
        var l = new Label { Name=name, Text=text, Position=pos, ZIndex=50 };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        AddChild(l); return l;
    }
}
