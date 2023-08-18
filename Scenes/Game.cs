using Godot;

public partial class Game : Node2D
{
    //Member variables
    Vector2 _screenSize = new(640, 400);
    Vector2 _padSize;
    Vector2 _direction = new(1.0f, 0.0f);

    const int INITIAL_BALL_SPEED = 160; //Constant for ball speed (in pixels/second)
    float _ballSpeed = INITIAL_BALL_SPEED; //Speed of the ball (also in pixels/second)
    const int PAD_SPEED = 150; // Constant for pads speed

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        //screen_size = Viewport.Cu _rect().size
        _padSize = GetNode<Sprite2D>("leftPaddle").Texture.GetSize();
        GD.Randomize();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        var dt = (float)delta;

        var ballNode = GetNode<Sprite2D>("ball");
        var ballPosition = ballNode.Position;
        var leftRect = new Rect2(GetNode<Sprite2D>("leftPaddle").Position - _padSize * 0.5f, _padSize);
        var rightRect = new Rect2(GetNode<Sprite2D>("rightPaddle").Position - _padSize * 0.5f, _padSize);

        //Integrate new ball position
        ballPosition += _direction * _ballSpeed * dt;

        //Flip when touching roof or floor
        if ((ballPosition.Y < 0 && _direction.Y < 0) || (ballPosition.Y > _screenSize.Y && _direction.Y > 0))
            _direction.Y = -_direction.Y;

        //Flip, change direction and increase speed when touching pads
        if ((leftRect.HasPoint(ballPosition) && _direction.X < 0) || (rightRect.HasPoint(ballPosition) && _direction.Y > 0))
        {
            _direction.X = -_direction.X;
            _direction.Y = GD.Randf() * 2.0f - 1f;
            _direction = _direction.Normalized();
            _ballSpeed *= 1.1f;
        }

        //Check gameover
        if (ballPosition.X < 0 || ballPosition.X > _screenSize.X)
        {
            ballPosition = _screenSize * 0.5f;
            _ballSpeed = INITIAL_BALL_SPEED;
            _direction = new Vector2(-1, 0);
        }

        ballNode.Position = ballPosition;

        //Move left paddle
        var leftPaddleNode = GetNode<Sprite2D>("leftPaddle");
        var leftPosition = leftPaddleNode.Position;

        if (leftPosition.Y > 0 && Input.IsActionPressed("left_move_up"))
            leftPosition.Y += -PAD_SPEED * dt;
        else if (leftPosition.Y < _screenSize.Y && Input.IsActionPressed("left_move_down"))
            leftPosition.Y += PAD_SPEED * dt;

        leftPaddleNode.Position = leftPosition;

        //Move right paddle
        var rightPaddleNode = GetNode<Sprite2D>("rightPaddle");
        var rightPosition = rightPaddleNode.Position;

        if (rightPosition.Y > 0 && Input.IsActionPressed("right_move_up"))
            rightPosition.Y += -PAD_SPEED * dt;
        else if (rightPosition.Y < _screenSize.Y && Input.IsActionPressed("right_move_down"))
            rightPosition.Y += PAD_SPEED * dt;

        rightPaddleNode.Position = rightPosition;
    }
}
