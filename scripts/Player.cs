using Godot;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
    [Export] public SpriteFrames WalkFrames;
    [Export] public SpriteFrames AttackFrames;
    [Export] public float BaseSpeed         = 50f;
    [Export] public float DashDistance      = 50f;
    [Export] public float DashCooldown      = 10f;
    [Export] public float KnockbackFriction = 9f;
    [Export] public float MaxHP             = 100f;
    [Export] public float AttackCooldown    = 0.35f;
    [Export] public float AttackRange       = 90f;
    [Export] public float AttackDamage      = 30f;
    [Export] public float AttackArc         = 140f;
    [Export] public float BoostMultiplier   = 1.5f;
    [Export] public float BoostDuration     = 10f;
    [Export] public AudioStream SfxAttack;
    [Export] public AudioStream SfxDash;
    [Export] public AudioStream SfxHurt;

    private float   _hp, _currentSpeed, _boostTimer, _dashTimer, _dashAnimTimer;
    private bool    _isBoosted, _isDashing, _isAttacking;
    private Vector2 _knockback, _dashDir, _lastMoveDir = Vector2.Down;
    private float   _attackTimer, _swordTimer;
    private string  _facingDir = "down";
    private readonly List<float> _slowStack = new();
    private float SlowFactor => _slowStack.Count > 0 ? _slowStack[0] : 1f;

    private const float Radius = 18f, SwordShowDur = 0.25f, DashAnimDur = 0.18f;

    private AnimatedSprite2D  _walkSprite, _attackSprite;
    private Node2D            _swordPivot, _swordVisual;
    private ColorRect         _hpBar, _hpBarBg;
    private AudioStreamPlayer _audio;

    public float CurrentHP => _hp;
    public static Player Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this; _hp = MaxHP; _currentSpeed = BaseSpeed; _slowStack.Clear();
        CollisionLayer = 2; CollisionMask = 1 | 4;
        var bc = new CollisionShape2D(); bc.Shape = new CircleShape2D { Radius = Radius }; AddChild(bc);

        if (WalkFrames   == null) WalkFrames   = TryFrames("res://resources/WalkFrames.tres");
        if (AttackFrames == null) AttackFrames = TryFrames("res://resources/AttackFrames.tres");

        if (WalkFrames != null)
        {
            _walkSprite = new AnimatedSprite2D { SpriteFrames = WalkFrames, Scale = new(1.5f,1.5f), ZIndex = 1 };
            AddChild(_walkSprite); _walkSprite.Play("walk_down");
        }
        if (AttackFrames != null)
        {
            _attackSprite = new AnimatedSprite2D { SpriteFrames = AttackFrames, Visible = false, Scale = new(1.5f,1.5f), ZIndex = 2 };
            _attackSprite.AnimationFinished += OnAttackDone; AddChild(_attackSprite);
        }
        if (WalkFrames == null)
        {
            AddChild(GameManager.CirclePoly(Radius, new Color(0.15f,0.92f,0.35f)));
            _swordPivot = new Node2D { ZIndex=2 }; _swordVisual = new Node2D { Visible=false };
            _swordVisual.AddChild(new ColorRect { Color=new Color(0.88f,0.88f,1f), Size=new(48f,7f), Position=new(Radius+4f,-3.5f) });
            _swordVisual.AddChild(new ColorRect { Color=new Color(0.72f,0.55f,0.18f), Size=new(7f,18f), Position=new(Radius,-9f) });
            _swordPivot.AddChild(_swordVisual); AddChild(_swordPivot);
        }
        _hpBarBg = new ColorRect { Color=new Color(0.3f,0,0), Size=new(44f,7f), Position=new(-22f,-42f), ZIndex=10 };
        _hpBar   = new ColorRect { Color=new Color(0.18f,0.85f,0.18f), Size=new(44f,7f), Position=new(-22f,-42f), ZIndex=11 };
        AddChild(_hpBarBg); AddChild(_hpBar);
        _audio = new AudioStreamPlayer(); AddChild(_audio);
        if (SfxAttack == null) SfxAttack = TrySfx("res://sounds/sword-attack.mp3");
        if (SfxDash   == null) SfxDash   = TrySfx("res://sounds/dash.mp3");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_attackTimer  > 0f) _attackTimer  -= dt;
        if (_dashTimer    > 0f) _dashTimer    -= dt;
        if (_dashAnimTimer> 0f) { _dashAnimTimer -= dt; if (_dashAnimTimer <= 0f) _isDashing = false; }
        if (_swordTimer   > 0f) { _swordTimer -= dt; if (_swordTimer <= 0f && _swordVisual != null) _swordVisual.Visible = false; }
        if (_isBoosted) { _boostTimer -= dt; if (_boostTimer <= 0f) { _isBoosted = false; _currentSpeed = BaseSpeed; } }

        string hud = "";
        if (_isBoosted)      hud += $"BOOST {_boostTimer:F1}s  ";
        if (_dashTimer > 0f) hud += $"Dash {_dashTimer:F1}s";
        if (GameManager.Instance?.BoostLabel is Label bl) bl.Text = hud;

        var dir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) dir.Y -= 1; if (Input.IsKeyPressed(Key.S)) dir.Y += 1;
        if (Input.IsKeyPressed(Key.A)) dir.X -= 1; if (Input.IsKeyPressed(Key.D)) dir.X += 1;
        bool moving = dir.LengthSquared() > 0f;
        if (moving) { dir = dir.Normalized(); _lastMoveDir = dir; if (!_isAttacking) _facingDir = VecToDir(dir); }

        var mouse = GetGlobalMousePosition();
        if (Input.IsMouseButtonPressed(MouseButton.Left) && _attackTimer <= 0f) PerformAttack(mouse);
        if (Input.IsKeyPressed(Key.Space) && _dashTimer <= 0f) PerformDash();

        _knockback = _knockback.Lerp(Vector2.Zero, dt * KnockbackFriction);
        Velocity = dir * _currentSpeed * SlowFactor + _knockback;
        MoveAndSlide(); ClampToField(Radius);
        UpdateAnim(moving);

        float pct = Mathf.Clamp(_hp / MaxHP, 0f, 1f);
        _hpBar.Size  = new Vector2(44f * pct, 7f);
        _hpBar.Color = pct > 0.5f ? new Color(0.18f,0.85f,0.18f) : pct > 0.25f ? new Color(0.9f,0.75f,0.1f) : new Color(0.9f,0.15f,0.1f);
    }

    private void UpdateAnim(bool moving)
    {
        if (_isAttacking) return;
        if (_attackSprite != null) _attackSprite.Visible = false;
        if (_walkSprite == null) return;
        _walkSprite.Visible = true;
        if (_isDashing)
        {
            string da = $"walk_{VecToDir(_dashDir)}";
            if (_walkSprite.Animation != da) _walkSprite.Play(da);
            _walkSprite.SpeedScale = 3f;
        }
        else
        {
            string wa = $"walk_{_facingDir}";
            if (_walkSprite.Animation != wa) _walkSprite.Play(wa);
            _walkSprite.SpeedScale = moving ? 1f : 0f;
        }
    }

    private void OnAttackDone()
    {
        _isAttacking = false;
        if (_attackSprite != null) _attackSprite.Visible = false;
        if (_walkSprite   != null) _walkSprite.Visible   = true;
    }

    private static string VecToDir(Vector2 v)
    {
        if (Mathf.Abs(v.X) >= Mathf.Abs(v.Y)) return v.X >= 0f ? "right" : "left";
        return v.Y >= 0f ? "down" : "up";
    }

    private void PerformAttack(Vector2 mouse)
    {
        _attackTimer = AttackCooldown; _isAttacking = true;
        PlayRestart(SfxAttack);
        var toM = mouse - GlobalPosition;
        _facingDir = VecToDir(toM.LengthSquared() > 10f ? toM : _lastMoveDir);
        if (_attackSprite != null)
        {
            _attackSprite.Visible = true; if (_walkSprite != null) _walkSprite.Visible = false;
            _attackSprite.Play($"attack_{_facingDir}"); _attackSprite.Frame = 0;
        }
        if (_swordVisual != null) { _swordVisual.Visible = true; _swordTimer = SwordShowDur; }
        float aim = toM.Angle(), half = Mathf.DegToRad(AttackArc * 0.5f);
        foreach (var z in new List<ZombieBase>(GameManager.AllZombies))
        {
            if (!IsInstanceValid(z)) continue;
            var tz = z.GlobalPosition - GlobalPosition;
            if (tz.Length() > AttackRange) continue;
            if (Mathf.Abs(Mathf.AngleDifference(aim, tz.Angle())) <= half) z.TakeDamage(AttackDamage);
        }
    }

    private void PerformDash()
    {
        _dashTimer = DashCooldown; _dashAnimTimer = DashAnimDur; _isDashing = true;
        _dashDir = Velocity.LengthSquared() > 1f ? Velocity.Normalized() : _lastMoveDir;
        PlaySfx(SfxDash);
        var off = _dashDir * DashDistance;
        GlobalPosition += off; ClampToField(Radius);
        foreach (var h in new List<Human>(GameManager.AllHumans))
        { if (IsInstanceValid(h)) { h.GlobalPosition += off; h.ClampToField(); } }
    }

    public void TakeDamage(float a)
    {
        if (_hp <= 0f) return; _hp -= a; PlaySfx(SfxHurt);
        Modulate = new Color(2f,0.3f,0.3f);
        GetTree().CreateTimer(0.1f).Timeout += () => { if (IsInstanceValid(this)) Modulate = Colors.White; };
        if (_hp <= 0f) { _hp = 0f; GameManager.Instance?.PlayerDied(); }
    }
    public void Heal(float a) { _hp = Mathf.Min(_hp+a,MaxHP); Modulate=new Color(0.4f,2f,0.4f); GetTree().CreateTimer(0.15f).Timeout+=()=>{ if(IsInstanceValid(this))Modulate=Colors.White; }; }
    public void ActivateSpeedBoost() { _isBoosted=true; _boostTimer=BoostDuration; _currentSpeed=BaseSpeed*BoostMultiplier; }
    public void ApplyKnockback(Vector2 f) => _knockback = f;
    public void ApplySlow(float f) => _slowStack.Add(f);
    public void RemoveSlow() { if (_slowStack.Count > 0) _slowStack.RemoveAt(0); }
    public void ClampToField(float r = Radius)
    {
        var b=GameManager.Field; var p=GlobalPosition;
        p.X=Mathf.Clamp(p.X,b.Position.X+r,b.End.X-r); p.Y=Mathf.Clamp(p.Y,b.Position.Y+r,b.End.Y-r);
        GlobalPosition=p;
    }
    private void PlaySfx(AudioStream s) { if(s==null||_audio==null)return; _audio.VolumeDb=AudioManager.SfxVolumeDb; _audio.Stream=s; _audio.Play(); }
    private void PlayRestart(AudioStream s) { if(s==null||_audio==null)return; _audio.Stop(); _audio.VolumeDb=AudioManager.SfxVolumeDb; _audio.Stream=s; _audio.Play(); }
    private static SpriteFrames TryFrames(string p) => ResourceLoader.Exists(p)?GD.Load<SpriteFrames>(p):null;
    private static AudioStream  TrySfx(string p)    => ResourceLoader.Exists(p)?GD.Load<AudioStream>(p):null;
}
