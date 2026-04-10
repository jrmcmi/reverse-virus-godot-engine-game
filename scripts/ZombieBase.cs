using Godot;
using System.Collections.Generic;

public abstract partial class ZombieBase : CharacterBody2D
{
    public float MaxHP          = 100f;
    public float ContactDamage  = 25f;
    public float DamageInterval = 0.5f;

    protected bool  _isCured = false;
    protected float _hp;
    protected const float BodyHalf = 14f;

    protected AnimatedSprite2D _sprite;
    protected string _facingDir = "down";

    private readonly Dictionary<Node, float> _dmgTimers = new();

    protected float SpeedMult => GameManager.ZombieSpeedMult;
    protected readonly RandomNumberGenerator Rng = new();

    private ColorRect _hpBar;
    private const float BarW = 34f;

    [Export] public AudioStream SfxHit;
    [Export] public AudioStream SfxDie;
    private AudioStreamPlayer _audio;

    public override void _Ready()
    {
        Rng.Randomize();
        _hp = MaxHP;

        CollisionLayer = 4;
        CollisionMask  = 1 | 2 | 8;

        // Zombie renders below environment objects (trees/rocks at z_index 2-4)
        ZIndex = 1;

        var frames = GD.Load<SpriteFrames>("res://resources/ZombieWalkFrames.tres");
        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Scale        = new Vector2(1.5f, 1.5f),
            ZIndex       = 1
        };
        AddChild(_sprite);
        _sprite.Play("zwalk_down");

        var col = new CollisionShape2D();
        col.Shape = new CircleShape2D { Radius = BodyHalf };
        AddChild(col);

        // ── HP bar — FIX: colour is now RED not violet ─────────────────────
        AddChild(new ColorRect
        {
            Color    = new Color(0.15f, 0f, 0f),
            Size     = new Vector2(BarW, 5f),
            Position = new Vector2(-BarW / 2f, -30f),
            ZIndex   = 10
        });
        _hpBar = new ColorRect
        {
            Color    = new Color(0.9f, 0.1f, 0.1f),  // Red
            Size     = new Vector2(BarW, 5f),
            Position = new Vector2(-BarW / 2f, -30f),
            ZIndex   = 11
        };
        AddChild(_hpBar);

        _audio = new AudioStreamPlayer();
        AddChild(_audio);

        BuildVisual();

        GameManager.RegisterZombie(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isCured) return;

        float dt = (float)delta;

        var keys = new List<Node>(_dmgTimers.Keys);
        foreach (var k in keys)
        {
            _dmgTimers[k] -= dt;
            if (_dmgTimers[k] < -5f) _dmgTimers.Remove(k);
        }

        MoveLogic(dt);
        MoveAndSlide();
        ClampToField();

        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            if (GetSlideCollision(i).GetCollider() is not Node target) continue;
            if (!_dmgTimers.TryGetValue(target, out float t) || t <= 0f)
            {
                _dmgTimers[target] = DamageInterval;
                if      (target is Player p) p.TakeDamage(ContactDamage);
                else if (target is Human  h) h.OnZombieContact(this);
            }
        }

        // HP bar — red → orange → dark red
        float pct = Mathf.Clamp(_hp / MaxHP, 0f, 1f);
        _hpBar.Size  = new Vector2(BarW * pct, 5f);
        _hpBar.Color = pct > 0.5f ? new Color(0.9f, 0.1f, 0.1f) :  // Red
                       pct > 0.25f ? new Color(0.85f, 0.45f, 0.05f) : // Orange
                                     new Color(0.6f, 0.05f, 0.05f);   // Dark red
    }

    protected abstract void MoveLogic(float delta);
    protected virtual void BuildVisual() { }

    public void TakeDamage(float amount)
    {
        if (_isCured) return;
        _hp -= amount;
        PlaySfx(SfxHit);
        Modulate = new Color(2.5f, 2.5f, 2.5f);
        GetTree().CreateTimer(0.07f).Timeout += () =>
        {
            if (IsInstanceValid(this) && !_isCured) Modulate = Colors.White;
        };
        if (_hp <= 0f) GetCured();
    }

    public void GetCured()
    {
        if (_isCured) return;
        _isCured = true;
        PlaySfx(SfxDie);
        GameManager.UnregisterZombie(this);

        if (IsInstanceValid(GameManager.Instance))
        {
            // FIX: Fire "zombie converted to human" sound via GameManager
            GameManager.Instance.OnZombieCured();

            var h = new Human();
            h.GlobalPosition = GlobalPosition;
            GameManager.Instance.AddChild(h);
        }
        QueueFree();
    }

    protected Human FindNearestHuman()
    {
        Human best = null; float bd = float.MaxValue;
        foreach (var h in GameManager.AllHumans)
        {
            if (!IsInstanceValid(h)) continue;
            float d = GlobalPosition.DistanceSquaredTo(h.GlobalPosition);
            if (d < bd) { bd = d; best = h; }
        }
        return best;
    }

    protected Vector2 WallAvoid(Vector2 dir, float edge = 80f)
    {
        var b = GameManager.Field; var p = GlobalPosition;
        float l = p.X - b.Position.X, r = b.End.X - p.X;
        float t = p.Y - b.Position.Y, d = b.End.Y - p.Y;
        if (l < edge) dir.X += Mathf.Lerp(2.5f, 0f, l / edge);
        if (r < edge) dir.X -= Mathf.Lerp(2.5f, 0f, r / edge);
        if (t < edge) dir.Y += Mathf.Lerp(2.5f, 0f, t / edge);
        if (d < edge) dir.Y -= Mathf.Lerp(2.5f, 0f, d / edge);
        return dir.LengthSquared() > 0.01f ? dir.Normalized() : Vector2.Right;
    }

    protected void MoveToward(Vector2 target, float speed)
    {
        var dir = target - GlobalPosition;
        if (dir.LengthSquared() < 4f) { Velocity = Vector2.Zero; return; }
        Velocity = WallAvoid(dir.Normalized()) * speed * SpeedMult;
    }

    protected bool IsNearWall(float edge = 65f)
    {
        var b = GameManager.Field; var p = GlobalPosition;
        return p.X - b.Position.X < edge || b.End.X - p.X < edge ||
               p.Y - b.Position.Y < edge || b.End.Y - p.Y < edge;
    }

    public void ClampToField()
    {
        var b = GameManager.Field; var pos = GlobalPosition;
        pos.X = Mathf.Clamp(pos.X, b.Position.X + BodyHalf, b.End.X - BodyHalf);
        pos.Y = Mathf.Clamp(pos.Y, b.Position.Y + BodyHalf, b.End.Y - BodyHalf);
        GlobalPosition = pos;
    }

    protected void UpdateZombieAnim()
    {
        if (_sprite == null) return;
        if (Velocity.LengthSquared() > 10f)
        {
            if (Mathf.Abs(Velocity.X) > Mathf.Abs(Velocity.Y))
                _facingDir = Velocity.X > 0f ? "right" : "left";
            else
                _facingDir = Velocity.Y > 0f ? "down" : "up";
        }
        string anim = $"zwalk_{_facingDir}";
        if (_sprite.Animation != anim) _sprite.Play(anim);
        _sprite.SpeedScale = Velocity.LengthSquared() > 100f ? 1f : 0f;
    }

    private void PlaySfx(AudioStream s)
    {
        if (s == null || !IsInstanceValid(_audio)) return;
        _audio.VolumeDb = AudioManager.SfxVolumeDb;
        _audio.Stream   = s;
        _audio.Play();
    }
}
