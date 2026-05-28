using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class TeensySerialInput : MonoBehaviour
{
    public static TeensySerialInput Instance { get; private set; }

    [Header("serial")]
    public string portName = "COM3";
    public int baudRate = 115200;
    public bool connectOnStart = true;

    [Header("Teensy pinout from trumpal_teensy_code")]
    public TeensyHardwareChannel[] hardwarePinout = TeensyHardwarePinout.CreateDefaultChannels();

    [Header("ToF calibration, mm")]
    public float valve1RestDistanceMM = 80f;
    public float valve1PressedDistanceMM = 25f;

    public float valve2RestDistanceMM = 80f;
    public float valve2PressedDistanceMM = 25f;

    public float valve3RestDistanceMM = 80f;
    public float valve3PressedDistanceMM = 25f;

    [Header("Unity-side press threshold")]
    public bool derivePressedStateFromDistance = true;
    [Range(0f, 1f)]
    public float pressAmountThreshold = 0.65f;

    [Header("debug")]
    public bool printIncomingLines = false;
    public bool printOutgoingCommands = true;

    private SerialPort serialPort;
    private Thread readThread;
    private volatile bool keepReading = false;

    private readonly object writeLock = new object();
    private readonly object stateLock = new object();

    private bool latestV1 = false;
    private bool latestV2 = false;
    private bool latestV3 = false;

    private int latestD1 = 255;
    private int latestD2 = 255;
    private int latestD3 = 255;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (connectOnStart)
        {
            Connect();
        }
    }

    void Update()
    {
        bool v1;
        bool v2;
        bool v3;

        int d1;
        int d2;
        int d3;

        lock (stateLock)
        {
            v1 = latestV1;
            v2 = latestV2;
            v3 = latestV3;

            d1 = latestD1;
            d2 = latestD2;
            d3 = latestD3;
        }

        float a1 = DistanceToPressAmount(d1, valve1RestDistanceMM, valve1PressedDistanceMM);
        float a2 = DistanceToPressAmount(d2, valve2RestDistanceMM, valve2PressedDistanceMM);
        float a3 = DistanceToPressAmount(d3, valve3RestDistanceMM, valve3PressedDistanceMM);

        if (derivePressedStateFromDistance)
        {
            v1 = v1 || a1 >= pressAmountThreshold;
            v2 = v2 || a2 >= pressAmountThreshold;
            v3 = v3 || a3 >= pressAmountThreshold;
        }

        ApplyValveStateFromTeensyChannel(1, v1, a1, d1);
        ApplyValveStateFromTeensyChannel(2, v2, a2, d2);
        ApplyValveStateFromTeensyChannel(3, v3, a3, d3);
    }

    void ApplyValveStateFromTeensyChannel(
        int teensyChannelNumber,
        bool pressed,
        float amount,
        int distanceMM
    )
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);

        int laneIndex = TeensyHardwarePinout.UnityChannelNumberToLaneIndex(
            teensyChannelNumber,
            hardwarePinout
        );

        ValveInputState.SetTeensyValve(laneIndex, pressed);
        ValveInputState.SetTeensyValveAmount(laneIndex, amount);
        ValveInputState.SetTeensyValveDistanceMM(laneIndex, distanceMM);
    }

    float DistanceToPressAmount(int distanceMM, float restDistanceMM, float pressedDistanceMM)
    {
        if (distanceMM < 0 || distanceMM >= 255)
        {
            return 0f;
        }

        float denominator = restDistanceMM - pressedDistanceMM;

        if (Mathf.Abs(denominator) < 0.001f)
        {
            return 0f;
        }

        float amount = (restDistanceMM - distanceMM) / denominator;

        return Mathf.Clamp01(amount);
    }

    public void Connect()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            return;
        }

        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 50;
            serialPort.WriteTimeout = 50;
            serialPort.NewLine = "\n";
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;

            serialPort.Open();

            keepReading = true;
            readThread = new Thread(ReadSerialLoop);
            readThread.IsBackground = true;
            readThread.Start();

            Debug.Log("Connected to Teensy on " + portName);
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not connect to Teensy on " + portName + ": " + exception.Message);
        }
    }

    public void Disconnect()
    {
        keepReading = false;

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(200);
        }

        if (serialPort != null)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
            catch
            {
                // Ignore close errors.
            }
        }

        serialPort = null;
    }

    void ReadSerialLoop()
    {
        while (keepReading)
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen)
                {
                    Thread.Sleep(10);
                    continue;
                }

                string line = serialPort.ReadLine();
                ParseIncomingLine(line);
            }
            catch (TimeoutException)
            {
                // Normal when no data arrives during timeout.
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Teensy serial read issue: " + exception.Message);
                Thread.Sleep(50);
            }
        }
    }

    void ParseIncomingLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        line = line.Trim();

        if (printIncomingLines)
        {
            Debug.Log("Teensy IN: " + line);
        }

        string[] tokens = line.Split(',');

        lock (stateLock)
        {
            foreach (string token in tokens)
            {
                string[] keyValue = token.Split(':');

                if (keyValue.Length != 2)
                {
                    continue;
                }

                string key = keyValue[0].Trim();
                string value = keyValue[1].Trim();

                if (key == "V1")
                {
                    latestV1 = value == "1";
                }
                else if (key == "V2")
                {
                    latestV2 = value == "1";
                }
                else if (key == "V3")
                {
                    latestV3 = value == "1";
                }
                else if (key == "D1")
                {
                    int.TryParse(value, out latestD1);
                }
                else if (key == "D2")
                {
                    int.TryParse(value, out latestD2);
                }
                else if (key == "D3")
                {
                    int.TryParse(value, out latestD3);
                }
            }
        }
    }

    public void SendLine(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        lock (writeLock)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                return;
            }

            try
            {
                serialPort.WriteLine(command);

                if (printOutgoingCommands)
                {
                    Debug.Log("Teensy OUT: " + command);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Teensy serial write issue: " + exception.Message);
            }
        }
    }

    void OnApplicationQuit()
    {
        SendLine("X");
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void OnValidate()
    {
        TeensyHardwarePinout.EnsureDefaultPinout(ref hardwarePinout);
    }
}
