using System;
using UnityEngine;

[Serializable]
public class TeensyHardwareChannel
{
    public string valveName;
    public int unityChannelNumber;
    public int tofMuxChannel;
    public int solenoidPwmPin;
    public int solenoidPhasePin;
    public int ermIn1Pin;
    public int ermIn2Pin;

    public TeensyHardwareChannel()
    {
    }

    public TeensyHardwareChannel(
        string valveName,
        int unityChannelNumber,
        int tofMuxChannel,
        int solenoidPwmPin,
        int solenoidPhasePin,
        int ermIn1Pin,
        int ermIn2Pin
    )
    {
        this.valveName = valveName;
        this.unityChannelNumber = unityChannelNumber;
        this.tofMuxChannel = tofMuxChannel;
        this.solenoidPwmPin = solenoidPwmPin;
        this.solenoidPhasePin = solenoidPhasePin;
        this.ermIn1Pin = ermIn1Pin;
        this.ermIn2Pin = ermIn2Pin;
    }
}

public static class TeensyHardwarePinout
{
    public const int ChannelCount = 3;
    public const int ActiveSolenoidPhase = 1;
    public const int TcaAddress = 0x70;
    public const int TeensySdaPin = 18;
    public const int TeensySclPin = 19;

    public static TeensyHardwareChannel[] CreateDefaultChannels()
    {
        // Default mapping mirrors the final Teensy firmware pin assignments.
        return new TeensyHardwareChannel[]
        {
            new TeensyHardwareChannel("Valve 1", 1, 0, 2, 5, 12, 13),
            new TeensyHardwareChannel("Valve 2", 2, 6, 3, 6, 14, 15),
            new TeensyHardwareChannel("Valve 3", 3, 7, 4, 7, 22, 23)
        };
    }

    public static void EnsureDefaultPinout(ref TeensyHardwareChannel[] channels)
    {
        // Repair null/short arrays so inspector data can be safely shared across components.
        if (channels == null || channels.Length != ChannelCount)
        {
            TeensyHardwareChannel[] defaults = CreateDefaultChannels();

            for (int i = 0; i < ChannelCount; i++)
            {
                if (channels != null && i < channels.Length && channels[i] != null)
                {
                    defaults[i] = channels[i];
                }
            }

            channels = defaults;
        }

        TeensyHardwareChannel[] fallback = CreateDefaultChannels();

        for (int i = 0; i < ChannelCount; i++)
        {
            if (channels[i] == null)
            {
                channels[i] = fallback[i];
            }

            if (channels[i].unityChannelNumber < 1 || channels[i].unityChannelNumber > ChannelCount)
            {
                channels[i].unityChannelNumber = i + 1;
            }
        }
    }

    public static int LaneToUnityChannelNumber(int laneIndex, TeensyHardwareChannel[] channels)
    {
        // Convert Unity's zero-based lane index into the one-based firmware command channel.
        EnsureDefaultPinout(ref channels);

        if (!IsValidLane(laneIndex))
        {
            return 1;
        }

        return channels[laneIndex].unityChannelNumber;
    }

    public static int UnityChannelNumberToLaneIndex(
        int unityChannelNumber,
        TeensyHardwareChannel[] channels
    )
    {
        // Convert telemetry channel numbers back into Unity's lane indexes.
        EnsureDefaultPinout(ref channels);

        for (int i = 0; i < channels.Length; i++)
        {
            if (channels[i].unityChannelNumber == unityChannelNumber)
            {
                return i;
            }
        }

        return unityChannelNumber - 1;
    }

    public static string GetDebugLabel(int laneIndex, TeensyHardwareChannel[] channels)
    {
        // Human-readable pinout label used by inspector/debug readouts.
        EnsureDefaultPinout(ref channels);

        if (!IsValidLane(laneIndex))
        {
            return "unknown lane";
        }

        TeensyHardwareChannel channel = channels[laneIndex];

        return channel.valveName +
            " / Unity channel " + channel.unityChannelNumber +
            " / ToF mux SC" + channel.tofMuxChannel +
            " / solenoid PWM " + channel.solenoidPwmPin +
            " phase " + channel.solenoidPhasePin +
            " / ERM " + channel.ermIn1Pin + "," + channel.ermIn2Pin;
    }

    public static bool IsValidLane(int laneIndex)
    {
        return laneIndex >= 0 && laneIndex < ChannelCount;
    }
}
