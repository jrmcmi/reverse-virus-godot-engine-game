using Godot;
using System.Collections.Generic;

/// <summary>
/// AudioManager.cs — Fixed zombie sound spam with per-zombie interval throttle.
///
/// FIX: Each zombie audio player now has a PlayInterval (seconds).
/// Once the zombie sound plays, it won't play again until PlayInterval elapses.
/// This prevents 20 zombies from all playing simultaneously every frame.
/// ZombiePlayInterval is exported so you can tune it in the Inspector.
/// </summary>
public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    [Export(PropertyHint.Range, "-40,0,1")] public float ConfigMusicVol  = -3f;
    [Export(PropertyHint.Range, "-40,0,1")] public float ConfigSfxVol    = -6f;
    [Export(PropertyHint.Range, "-40,0,1")] public float ZombieVolumeDb  = -12f;
    [Export] public float ZombieHearRadius   = 200f;
    [Export] public float ZombiePlayInterval = 4f;   // Seconds between zombie sound plays per zombie

    public static float SfxVolumeDb   => Instance?._sfxVol   ?? -5f;
    public static float MusicVolumeDb => Instance?._musicVol ?? -3f;

    private float _sfxVol, _musicVol, _zombieVol;

    private AudioStreamPlayer _bgMusic, _resultMusic, _sfxOneShot;

    // Zombie audio: player + cooldown timer per zombie
    private readonly Dictionary<ZombieBase, AudioStreamPlayer> _zombiePlayers = new();
    private readonly Dictionary<ZombieBase, float>             _zombieCooldowns = new();

    private AudioStream _bgStream, _winStream, _loseStream, _zombieStream,
                        _powerupStream, _convertedStream, _savedStream;

    public override void _Ready()
    {
        Instance  = this;
        _sfxVol   = ConfigSfxVol;
        _musicVol = ConfigMusicVol;
        _zombieVol = ZombieVolumeDb;

        _bgStream        = TryLoad("res://sounds/background-music.mp3");
        _winStream       = TryLoad("res://sounds/game-music-win.mp3");
        _loseStream      = TryLoad("res://sounds/game-music-lose.mp3");
        _zombieStream    = TryLoad("res://sounds/zombie.mp3");
        _powerupStream   = TryLoad("res://sounds/powerup-pickup.mp3");
        _convertedStream = TryLoad("res://sounds/zombie-converted-human.mp3");
        _savedStream     = TryLoad("res://sounds/human-saved.mp3");

        _bgMusic     = new AudioStreamPlayer { VolumeDb = _musicVol }; AddChild(_bgMusic);
        _resultMusic = new AudioStreamPlayer { VolumeDb = _musicVol }; AddChild(_resultMusic);
        _sfxOneShot  = new AudioStreamPlayer { VolumeDb = _sfxVol  }; AddChild(_sfxOneShot);

        if (_bgStream is AudioStreamMP3 mp3) mp3.Loop = true;
        if (_bgStream != null) { _bgMusic.Stream = _bgStream; _bgMusic.Play(); }
    }

    public override void _Process(double delta)
    {
        if (Instance == null) return;
        float dt = (float)delta;
        _sfxVol = ConfigSfxVol; _musicVol = ConfigMusicVol; _zombieVol = ZombieVolumeDb;
        _bgMusic.VolumeDb = _musicVol; _resultMusic.VolumeDb = _musicVol; _sfxOneShot.VolumeDb = _sfxVol;

        var player = GameManager.PlayerNode;
        if (player == null) return;

        var toRemove = new List<ZombieBase>();
        // Age cooldowns
        var cdKeys = new List<ZombieBase>(_zombieCooldowns.Keys);
        foreach (var k in cdKeys) { _zombieCooldowns[k] -= dt; }

        foreach (var kvp in _zombiePlayers)
        {
            var zombie = kvp.Key; var ap = kvp.Value;
            if (!IsInstanceValid(zombie) || !IsInstanceValid(ap)) { toRemove.Add(zombie); continue; }
            float dist = player.GlobalPosition.DistanceTo(zombie.GlobalPosition);
            if (dist <= ZombieHearRadius)
            {
                ap.VolumeDb = _zombieVol;
                // Only trigger play if cooldown expired and not already playing
                _zombieCooldowns.TryGetValue(zombie, out float cd);
                if (!ap.Playing && cd <= 0f)
                {
                    ap.Play();
                    _zombieCooldowns[zombie] = ZombiePlayInterval;
                }
            }
            else { if (ap.Playing) ap.Stop(); }
        }
        foreach (var z in toRemove) { if (_zombiePlayers.TryGetValue(z, out var ap) && IsInstanceValid(ap)) ap.QueueFree(); _zombiePlayers.Remove(z); _zombieCooldowns.Remove(z); }
    }

    public void RegisterZombieAudio(ZombieBase zombie)
    {
        if (_zombieStream == null || zombie == null || _zombiePlayers.ContainsKey(zombie)) return;
        var ap = new AudioStreamPlayer { Stream = _zombieStream, VolumeDb = _zombieVol };
        AddChild(ap); _zombiePlayers[zombie] = ap; _zombieCooldowns[zombie] = 0f;
    }

    public void UnregisterZombieAudio(ZombieBase zombie)
    {
        if (_zombiePlayers.TryGetValue(zombie, out var ap)) { if (IsInstanceValid(ap)) ap.QueueFree(); _zombiePlayers.Remove(zombie); }
        _zombieCooldowns.Remove(zombie);
    }

    public void PlaySfx(AudioStream stream)
    {
        if (stream == null || _sfxOneShot == null) return;
        _sfxOneShot.VolumeDb = _sfxVol; _sfxOneShot.Stream = stream; _sfxOneShot.Play();
    }

    public void PlayResultMusic(bool won)
    {
        _bgMusic.Stop();
        var s = won ? _winStream : _loseStream;
        if (s == null) return; _resultMusic.Stream = s; _resultMusic.Play();
    }

    public AudioStream PowerupStream   => _powerupStream;
    public AudioStream ConvertedStream => _convertedStream;
    public AudioStream SavedStream     => _savedStream;

    private static AudioStream TryLoad(string p)
    {
        if (ResourceLoader.Exists(p)) return GD.Load<AudioStream>(p);
        GD.PushWarning($"[AudioManager] Missing: {p}"); return null;
    }
}
