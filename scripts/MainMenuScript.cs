using Godot;

/// <summary>
/// MainMenuScript.cs — Wires the Play Game button to load the game scene.
///
/// Place this script at res://scripts/MainMenuScript.cs
/// Change GameScenePath if your main scene is at a different path.
/// </summary>
public partial class MainMenuScript : Control
{
    [Export] public string GameScenePath = "res://Main.tscn";

    public override void _Ready()
    {
        // Find the Play button by node name and connect its pressed signal
        if (GetNodeOrNull("CenterContainer/VBox/PlayButton") is Button btn)
            btn.Pressed += OnPlayPressed;
    }

    private void OnPlayPressed()
    {
        // LoadSceneAs is the Godot 4 way to switch scenes completely
        GetTree().ChangeSceneToFile(GameScenePath);
    }
}
