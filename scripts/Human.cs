using Godot;
using System.Collections.Generic;

public partial class Human : CharacterBody2D
{
    [Export] public float MoveSpeed     = 50f;
    [Export] public float FollowRadius  = 50f;
    [Export] public float PersonalSpace = 10f;
    [Export] public float SepRadius     = 32f;
    [Export] public float MaxHP         = 20f;
    [Export] public float HpRegenRate   = 4f;   // HP/s regeneration when not being hit

    [Export] public AudioStream SfxDie;

    private float _hp;
    private bool  _hasShield, _isDead;

    private readonly List<float> _slowStack = new();
    private float SlowFactor => _slowStack.Count > 0 ? _slowStack[0] : 1f;

    private AnimatedSprite2D _sprite;
    private string _facingDir = "down";
    private Polygon2D _shieldRing;
    private ColorRect _hpBarBg, _hpBarFill;
    private AudioStreamPlayer _audio;
    private const float Radius = 14f, BarW = 28f;

    public override void _Ready()
    {
        _slowStack.Clear(); _hp = MaxHP;
        CollisionLayer = 8; CollisionMask = 1 | 4;
        var col = new CollisionShape2D(); col.Shape = new CircleShape2D { Radius = Radius }; AddChild(col);
        var frames = GD.Load<SpriteFrames>("res://resources/HumanWalkFrames.tres");
        _sprite = new AnimatedSprite2D { SpriteFrames = frames, Scale = new(1.5f,1.5f), ZIndex = 1 };
        AddChild(_sprite); _sprite.Play("hwalk_down");
        _shieldRing = GameManager.CirclePoly(Radius+9f, new Color(0.3f,0.65f,1f,0.55f));
        _shieldRing.Visible = false; AddChild(_shieldRing);
        _hpBarBg   = new ColorRect { Color=new Color(0.2f,0,0), Size=new(BarW,5f), Position=new(-BarW/2f,-28f), ZIndex=10 };
        _hpBarFill = new ColorRect { Color=new Color(0.15f,0.82f,0.85f), Size=new(BarW,5f), Position=new(-BarW/2f,-28f), ZIndex=11 };
        AddChild(_hpBarBg); AddChild(_hpBarFill);
        _audio = new AudioStreamPlayer(); AddChild(_audio);
        GameManager.RegisterHuman(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead) return;
        float dt = (float)delta;
        if (GameManager.SafezoneBounds.HasPoint(GlobalPosition)) { Save(); return; }
        if (_hp < MaxHP) _hp = Mathf.Min(_hp + HpRegenRate * dt, MaxHP);
        float pct = Mathf.Clamp(_hp / MaxHP, 0f, 1f);
        _hpBarFill.Size  = new Vector2(BarW * pct, 5f);
        _hpBarFill.Color = pct > 0.5f ? new Color(0.15f,0.82f,0.85f) : pct > 0.25f ? new Color(0.9f,0.75f,0.1f) : new Color(0.9f,0.15f,0.1f);

        var player = GameManager.PlayerNode;
        if (player == null) return;
        Vector2 toPlayer = player.GlobalPosition - GlobalPosition;
        float   dist     = toPlayer.Length();
        Vector2 moveDir  = Vector2.Zero;
        if (dist > FollowRadius) moveDir += toPlayer.Normalized() * 1.4f;
        else if (dist < PersonalSpace) moveDir -= toPlayer.Normalized() * 1.1f;
        foreach (var other in GameManager.AllHumans)
        {
            if (!IsInstanceValid(other) || other == this) continue;
            var away = GlobalPosition - other.GlobalPosition; float d = away.Length();
            if (d < SepRadius && d > 0.01f) moveDir += away.Normalized() * (SepRadius - d) / SepRadius;
        }
        Velocity = moveDir.LengthSquared() > 0.01f ? moveDir.Normalized() * MoveSpeed * SlowFactor : Vector2.Zero;
        MoveAndSlide(); ClampToField(); UpdateAnim();
    }

    public void OnZombieContact(ZombieBase zombie)
    {
        if (_isDead) return;
        if (_hasShield)
        {
            _hasShield = false; _shieldRing.Visible = false;
            Modulate = Colors.White * 2.5f;
            GetTree().CreateTimer(0.12f).Timeout += () => { if (IsInstanceValid(this)) Modulate = Colors.White; };
            return;
        }
        _hp -= zombie.ContactDamage;
        Modulate = new Color(2f,0.3f,0.3f);
        GetTree().CreateTimer(0.1f).Timeout += () => { if (IsInstanceValid(this)) Modulate = Colors.White; };
        if (_hp <= 0f) { _hp = 0f; TurnToZombie(); }
    }

    private void Save()
    {
        if (_isDead) return; _isDead = true;
        GameManager.UnregisterHuman(this); GameManager.Instance?.OnHumanSaved();
        Modulate = new Color(0.4f,1f,0.7f) * 3f; QueueFree();
    }

    private void TurnToZombie()
    {
        if (_isDead) return; _isDead = true;
        GameManager.UnregisterHuman(this);
        if (SfxDie != null && IsInstanceValid(_audio)) { _audio.VolumeDb = AudioManager.SfxVolumeDb; _audio.Stream = SfxDie; _audio.Play(); }
        if (IsInstanceValid(GameManager.Instance)) { var z = new Sprinter(); z.Position = GlobalPosition; GameManager.Instance.AddChild(z); }
        QueueFree();
    }

    private void UpdateAnim()
    {
        if (_sprite == null) return;
        if (Velocity.LengthSquared() > 10f)
        {
            if (Mathf.Abs(Velocity.X) > Mathf.Abs(Velocity.Y)) _facingDir = Velocity.X > 0f ? "right" : "left";
            else _facingDir = Velocity.Y > 0f ? "down" : "up";
        }
        string anim = $"hwalk_{_facingDir}";
        if (_sprite.Animation != anim) _sprite.Play(anim);
        _sprite.SpeedScale = Velocity.LengthSquared() > 100f ? 1f : 0f;
    }

    public void ActivateShield() { _hasShield = true; _shieldRing.Visible = true; }
    public bool HasShield => _hasShield;
    public void ApplySlow(float f) => _slowStack.Add(f);
    public void RemoveSlow() { if (_slowStack.Count > 0) _slowStack.RemoveAt(0); }
    public void Heal() { _hp = MaxHP; Modulate = new Color(0.5f,2f,0.5f); GetTree().CreateTimer(0.15f).Timeout += () => { if (IsInstanceValid(this)) Modulate = Colors.White; }; }
    public void ClampToField()
    {
        var b = GameManager.Field; var pos = GlobalPosition;
        pos.X = Mathf.Clamp(pos.X, b.Position.X+Radius, b.End.X-Radius);
        pos.Y = Mathf.Clamp(pos.Y, b.Position.Y+Radius, b.End.Y-Radius);
        GlobalPosition = pos;
    }
}
