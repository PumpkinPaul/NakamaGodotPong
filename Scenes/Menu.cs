using Godot;
using Nakama;

public partial class Menu : Node2D
{
    public Node CurrentScene { get; set; }

	Button _connectButton;
    Button _cancelButton;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
        Viewport root = GetTree().Root;
        CurrentScene = root.GetChild(root.GetChildCount() - 1);
        
        _connectButton = GetNode<Button>("connectButton");
        _connectButton.Pressed += ConnectButton_Pressed;

        _cancelButton = GetNode<Button>("cancelButton");
        _cancelButton.Pressed += CancelButton_Pressed;
    }

    private async void ConnectButton_Pressed()
    {
        GD.Print("ConnectButton_Pressed");

        var http_adapter = new GodotHttpAdapter();
        // It's a Node, so it needs to be added to the scene tree.
        // Consider putting this in an autoload singleton so it won't go away unexpectedly.
        AddChild(http_adapter);

        const string scheme = "http";
        const string host = "127.0.0.1";
        const int port = 7350;
        const string serverKey = "defaultkey";

        // Pass in the 'http_adapter' as the last argument.
        var client = new Client(scheme, host, port, serverKey, http_adapter)
        {
            // To log DEBUG messages to the Godot console.
            Logger = new GodotLogger("Nakama", GodotLogger.LogLevel.DEBUG)
        };

        ISession session;
        try
        {
            session = await client.AuthenticateDeviceAsync(OS.GetUniqueId(), "TestUser", true);
        }
        catch (ApiResponseException e)
        {
            GD.PrintErr(e.ToString());
            return;
        }

        var websocket_adapter = new GodotWebSocketAdapter();
        // Like the HTTP adapter, it's a Node, so it needs to be added to the scene tree.
        // Consider putting this in an autoload singleton so it won't go away unexpectedly.
        AddChild(websocket_adapter);

        // Pass in the 'websocket_adapter' as the last argument.

        //GotoScene("res://Scenes/Game.tscn");
    }

    private void CancelButton_Pressed()
    {
        GD.Print("CancelButton_Pressed");
    }

    public void GotoScene(string path)
    {
        // This function will usually be called from a signal callback,
        // or some other function from the current scene.
        // Deleting the current scene at this point is
        // a bad idea, because it may still be executing code.
        // This will result in a crash or unexpected behavior.

        // The solution is to defer the load to a later time, when
        // we can be sure that no code from the current scene is running:

        CallDeferred(MethodName.DeferredGotoScene, path);
    }

    public void DeferredGotoScene(string path)
    {
        // It is now safe to remove the current scene
        var oldScene = CurrentScene;
        oldScene.Free();

        // Load a new scene.
        var nextScene = (PackedScene)GD.Load(path);

        // Instance the new scene.
        CurrentScene = nextScene.Instantiate();

        // Add it to the active scene, as child of root.
        GetTree().Root.AddChild(CurrentScene);

        // Optionally, to make it compatible with the SceneTree.change_scene_to_file() API.
        //GetTree().CurrentScene = CurrentScene;

        GetTree().Root.RemoveChild(oldScene);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}
}
