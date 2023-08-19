// Copyright Pumpkin Games Ltd. All Rights Reserved.

using Godot;
using NakamaGodotPong.Network;
using System;

namespace NakamaGodotPong.Players;

/// <summary>
/// Represents the player controlled paddle.
/// </summary>
public class Paddle
{
    const float PADDLE_SPEED = 0.3f;

    // To implement smoothing, we need more than one copy of the tank state.
    // We must record both where it used to be, and where it is now, an also
    // a smoothed value somewhere in between these two states which is where
    // we will draw the tank on the screen. To simplify managing these three
    // different versions of the tank state, we move all the state fields into
    // this internal helper structure.
    record struct PaddleState
    {
        public Vector2 Position;
        public Vector2 Velocity;
    }

    // This is the latest master copy of the tank state, used by our local
    // physics computations and prediction. This state will jerk whenever
    // a new network packet is received.
    PaddleState _simulationState;

    // This is a copy of the state from immediately before the last
    // network packet was received.
    PaddleState _previousState;

    // This is the tank state that is drawn onto the screen. It is gradually
    // interpolated from the previousState toward the simultationState, in
    // order to smooth out any sudden jumps caused by discontinuities when
    // a network packet suddenly modifies the simultationState.
    PaddleState _displayState;

    // Used to interpolate displayState from previousState toward simulationState.
    float _currentSmoothing;

    // Averaged time difference from the last 100 incoming packets, used to
    // estimate how our local clock compares to the time on the remote machine.
    readonly RollingAverage _clockDelta = new(100);

    // Input controls can be read from keyboard, gamepad, or the network.
    Vector2 _paddleInput;
    
    Vector2 _screenSize;

    /// <summary>
    /// Constructs a new Tank instance.
    /// </summary>
    public Paddle(Vector2 position, Vector2 screenSize)
    {
        _simulationState.Position = position;

        // Initialize all three versions of our state to the same values.
        _previousState = _simulationState;
        _displayState = _simulationState;

        _screenSize = screenSize;
    }

    /// <summary>
    /// Moves a locally controlled tank in response to the specified inputs.
    /// </summary>
    public void UpdateLocal(Vector2 paddleInput)
    {
        _paddleInput = paddleInput;

        // Update the master simulation state.
        UpdateState(ref _simulationState);

        // Locally controlled tanks have no prediction or smoothing, so we
        // just copy the simulation state directly into the display state.
        _displayState = _simulationState;
    }

    /// <summary>
    /// Applies prediction and smoothing to a remotely controlled tank.
    /// </summary>
    public void UpdateRemote(int framesBetweenPackets, bool enablePrediction)
    {
        // Update the smoothing amount, which interpolates from the previous
        // state toward the current simultation state. The speed of this decay
        // depends on the number of frames between packets: we want to finish
        // our smoothing interpolation at the same time the next packet is due.
        float smoothingDecay = 1.0f / framesBetweenPackets;

        _currentSmoothing -= smoothingDecay;

        if (_currentSmoothing < 0)
            _currentSmoothing = 0;

        if (enablePrediction)
        {
            // Predict how the remote tank will move by updating
            // our local copy of its simultation state.
            UpdateState(ref _simulationState);

            // If both smoothing and prediction are active,
            // also apply prediction to the previous state.
            if (_currentSmoothing > 0)
            {
                UpdateState(ref _previousState);
            }
        }

        if (_currentSmoothing > 0)
        {
            // Interpolate the display state gradually from the
            // previous state to the current simultation state.
            ApplySmoothing();
        }
        else
        {
            // Copy the simulation state directly into the display state.
            _displayState = _simulationState;
        }
    }

    /// <summary>
    /// Writes our local tank state into a network packet.
    /// </summary>
    public void WriteNetworkPacket(double delta, PacketWriter packetWriter)
    {
        // Send our current time.
        packetWriter.Write((float)delta);

        // Send the current state of the tank.
        packetWriter.Write(_simulationState.Position);
        packetWriter.Write(_simulationState.Velocity);

        // Also send our current inputs. These can be used to more accurately
        // predict how the tank is likely to move in the future.
        packetWriter.Write(_paddleInput);
    }

    /// <summary>
    /// Reads the state of a remotely controlled tank from a network packet.
    /// </summary>
    public void ReadNetworkPacketEvent(
        float delta,
        ReceivedRemotePlayerPaddleStateEventArgs paddlePacket,
        TimeSpan latency,
        bool enablePrediction,
        bool enableSmoothing)
    {
        if (enableSmoothing)
        {
            // Start a new smoothing interpolation from our current
            // state toward this new state we just received.
            _previousState = _displayState;
            _currentSmoothing = 1;
        }
        else
        {
            _currentSmoothing = 0;
        }

        // Read what time this packet was sent.
        float packetSendTime = paddlePacket.TotalSeconds;

        // Read simulation state from the network packet.
        _simulationState.Position = paddlePacket.Position;
        _simulationState.Velocity = paddlePacket.Velocity;

        // Read remote inputs from the network packet.
        _paddleInput = paddlePacket.TankInput;

        // Optionally apply prediction to compensate for
        // how long it took this packet to reach us.
        if (enablePrediction)
        {
            ApplyPrediction(delta, latency, packetSendTime);
        }
    }

    /// <summary>
    /// Applies smoothing by interpolating the display state somewhere
    /// in between the previous state and current simulation state.
    /// </summary>
    void ApplySmoothing()
    {
        _displayState.Position = _previousState.Position.Lerp(_simulationState.Position, _currentSmoothing);
        _displayState.Velocity = _previousState.Velocity.Lerp(_simulationState.Velocity, _currentSmoothing);
    }

    /// <summary>
    /// Incoming network packets tell us where the tank was at the time the packet
    /// was sent. But packets do not arrive instantly! We want to know where the
    /// tank is now, not just where it used to be. This method attempts to guess
    /// the current state by figuring out how long the packet took to arrive, then
    /// running the appropriate number of local updates to catch up to that time.
    /// This allows us to figure out things like "it used to be over there, and it
    /// was moving that way while turning to the left, so assuming it carried on
    /// using those same inputs, it should now be over here".
    /// </summary>
    void ApplyPrediction(double delta, TimeSpan latency, float packetSendTime)
    {
        // Work out the difference between our current local time
        // and the remote time at which this packet was sent.
        float localTime = (float)delta;

        float timeDelta = localTime - packetSendTime;

        // Maintain a rolling average of time deltas from the last 100 packets.
        _clockDelta.AddValue(timeDelta);

        // The caller passed in an estimate of the average network latency, which
        // is provided by the XNA Framework networking layer. But not all packets
        // will take exactly that average amount of time to arrive! To handle
        // varying latencies per packet, we include the send time as part of our
        // packet data. By comparing this with a rolling average of the last 100
        // send times, we can detect packets that are later or earlier than usual,
        // even without having synchronized clocks between the two machines. We
        // then adjust our average latency estimate by this per-packet deviation.

        float timeDeviation = timeDelta - _clockDelta.AverageValue;

        latency += TimeSpan.FromSeconds(timeDeviation);

        TimeSpan oneFrame = TimeSpan.FromSeconds(1.0 / 60.0);

        // Apply prediction by updating our simulation state however
        // many times is necessary to catch up to the current time.
        while (latency >= oneFrame)
        {
            UpdateState(ref _simulationState);

            latency -= oneFrame;
        }
    }

    /// <summary>
    /// Updates one of our state structures, using the current inputs to turn
    /// the tank, and applying the velocity and inertia calculations. This
    /// method is used directly to update locally controlled tanks, and also
    /// indirectly to predict the motion of remote tanks.
    /// </summary>
    void UpdateState(ref PaddleState state)
    {
        // Update the position and velocity.
        state.Position += state.Velocity;

        // Clamp so the tank cannot drive off the edge of the screen.
        state.Position = state.Position.Clamp(Vector2.Zero, _screenSize);
    }

    /// <summary>
    /// Gradually rotates the tank to face the specified direction.
    /// See the Aiming sample (creators.xna.com) for details.
    /// </summary>
    static float TurnToFace(float rotation, Vector2 target, float turnRate)
    {
        if (target == Vector2.Zero)
            return rotation;

        float angle = (float)Math.Atan2(-target.Y, target.X);

        float difference = rotation - angle;

        while (difference > Math.PI)
            difference -= (float)Math.PI * 2.0f;

        while (difference < -Math.PI)
            difference += (float)Math.PI * 2.0f;

        turnRate *= Math.Abs(difference);

        if (difference < 0)
            return rotation + Math.Min(turnRate, -difference);
        else
            return rotation - Math.Min(turnRate, difference);
    }
}
