// Copyright Pumpkin Games Ltd. All Rights Reserved.

//Based on code from the FishGame Unity sample from Herioc Labs.
//https://github.com/heroiclabs/fishgame-unity/blob/main/FishGame/Assets/Entities/Player/MatchDataJson.cs

using Godot;
using Nakama.TinyJson;
using System.Collections.Generic;

namespace NakamaGodotPong.Players;

public static class MatchDataJson
{
    /// <summary>
    /// Creates a network message containing velocity and position.
    /// </summary>
    /// <returns>A JSONified string containing velocity and position data.</returns>
    public static string VelocityAndPosition(Vector2 velocity, Vector2 position)
    {
        var values = new Dictionary<string, string>
        {
            { "velocity.x", velocity.X.ToString() },
            { "velocity.y", velocity.Y.ToString() },
            { "position.x", position.X.ToString() },
            { "position.y", position.Y.ToString() }
        };

        return Newtonsoft.Json.JsonConvert.SerializeObject(values);
    }

    /// <summary>
    /// Creates a network message containing direction and position.
    /// </summary>
    /// <returns>A JSONified string containing direction and position data.</returns>
    public static string DirectionAndPosition(float direction, Vector2 position)
    {
        var values = new Dictionary<string, string>
        {
            { "direction", direction.ToString() },
            { "position.x", position.X.ToString() },
            { "position.y", position.Y.ToString() }
        };

        return Newtonsoft.Json.JsonConvert.SerializeObject(values);
    }

    /// <summary>
    /// Creates a network message containing player input.
    /// </summary>
    /// <returns>A JSONified string containing player input.</returns>
    public static string Input(bool moveUp, bool moveDown)
    {
        var values = new Dictionary<string, string>
        {
            { "moveUp", moveUp.ToString() },
            { "moveDown", moveDown.ToString() },
        };

        return values.ToJson();
    }

    /// <summary>
    /// Creates a network message containing player scores.
    /// </summary>
    /// <returns>A JSONified string containing player scores.</returns>
    public static string Score(int player1Score, int player2Score)
    {
        var values = new Dictionary<string, string>
        {
            { "player1.score", player1Score.ToString() },
            { "player2.score", player2Score.ToString() },
        };

        return values.ToJson();
    }
}
