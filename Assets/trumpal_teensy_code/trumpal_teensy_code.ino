#include <Wire.h>
#include <Adafruit_VL6180X.h>
#include <string.h>
#include <stdlib.h>

// ------------------------------------------------------------
// Solenoid drivers: DRV8876-style PWM/enable + phase
// Unity mapping:
// SOL,1 -> PWM 2, phase 5
// SOL,2 -> PWM 3, phase 6
// SOL,3 -> PWM 4, phase 7
// ------------------------------------------------------------
const int SOLENOID_PWM[3]   = {2, 3, 4};
const int SOLENOID_PHASE[3] = {5, 6, 7};

// ------------------------------------------------------------
// Solenoid phase setup
// Your hardware only uses phase 1.
// User presses down; solenoid pushes back in one direction.
// ------------------------------------------------------------
const bool SOLENOID_ACTIVE_PHASE = HIGH;

// ------------------------------------------------------------
// ERM drivers: DRV8833-style two-input control
// Unity mapping:
// ERM,1 -> pins 12,13
// ERM,2 -> pins 14,15
// ERM,3 -> pins 22,23
// ------------------------------------------------------------
const bool ERM_HARDWARE_CONNECTED = true;

const int ERM_IN1[3] = {12, 14, 22};
const int ERM_IN2[3] = {13, 15, 23};

// ------------------------------------------------------------
// I2C multiplexer + VL6180X ToF sensors
// Teensy 4.0 default I2C: SDA = 18, SCL = 19
// TCA9548A channels:
// D1/V1 -> SC0/SD0 -> lane 1
// D2/V2 -> SC6/SD6 -> lane 2
// D3/V3 -> SC7/SD7 -> lane 3
// ------------------------------------------------------------
const uint8_t TCA_ADDR = 0x70;
const uint8_t TOF_CHANNELS[3] = {0, 6, 7};

Adafruit_VL6180X tof = Adafruit_VL6180X();
bool tofAvailable[3] = {false, false, false};
unsigned long lastTofRetryTime = 0;
const unsigned long TOF_RETRY_INTERVAL_MS = 1000;

// ------------------------------------------------------------
// Raw ToF press detection
// Unity now performs the main discrete ToF filtering.
// Teensy still reports V1/V2/V3 for debugging only.
// Current measured behavior:
// unpressed distance is larger
// pressed distance is smaller
// therefore raw Teensy V = 1 when D <= threshold.
// ------------------------------------------------------------
const uint8_t INVALID_DISTANCE = 255;
uint8_t pressThresholdMM = 65;

// ------------------------------------------------------------
// Unity serial output timing
// ------------------------------------------------------------
unsigned long lastUnitySendTime = 0;
const unsigned long UNITY_SEND_INTERVAL_MS = 20; // 50 Hz

// ------------------------------------------------------------
// Serial command surface
// MVP commands are always accepted:
// X, PRECUE, TAPCOMPLETE, HOLDSTART, HOLDCOMPLETE, MISS
// Bench tuning commands can be disabled after the hardware feels right:
// THRESH, SOL, SOLRAMP, ERM, ERMRAMP, TEST, TESTCH
// Not used in this Unity MVP: READ, STREAM, RATE, A/B/C quick commands,
// blocking scale playback loops.
// ------------------------------------------------------------
const bool ENABLE_TUNING_COMMANDS = true;

// ------------------------------------------------------------
// Gameplay solenoid push-off tuning.
// Gameplay push-off is intentionally immediate. Long ramps stay available
// through SOLRAMP/TEST for bench tuning only.
// ------------------------------------------------------------
const float MAX_SOLENOID_DUTY = 1.00f;
const unsigned long DEFAULT_SOLENOID_PULSE_MS = 800;
const unsigned long MAX_SOLENOID_PULSE_MS = 800;
const unsigned long MAX_SOLENOID_RAMP_MS = 1000;
const unsigned long MAX_HAPTIC_TEST_DELAY_MS = 5000;

const float TAP_RESET_SOL_DUTY_GOOD = 1.00f;
const float TAP_RESET_SOL_DUTY_PERFECT = 1.00f;
const float HOLD_RESET_SOL_DUTY = 1.00f;
const float MISS_RESET_SOL_DUTY = 1.00f;

const unsigned long TAP_RESET_SOL_MS = 800;

const unsigned long HOLD_RESET_SOL_MS = 800;
const unsigned long MISS_RESET_SOL_MS = 800;

// ------------------------------------------------------------
// ERM gameplay tuning
// ------------------------------------------------------------
const float MAX_ERM_DUTY = 1.00f;
const unsigned long MAX_ERM_PULSE_MS = 500;
const unsigned long MAX_ERM_RAMP_MS = 1200;

const float PRECUE_ERM_DUTY = 1.00f;
const unsigned long PRECUE_ERM_RAMP_MS = 500;
const unsigned long PRECUE_ERM_HOLD_MS = 200;
const int PRECUE_ERM_CURVE = 0;

const float GOOD_ERM_DUTY = 0.25f;
const float PERFECT_ERM_DUTY = 0.35f;
const float MISS_ERM_DUTY = 0.45f;

const unsigned long GOOD_ERM_MS = 120;
const unsigned long PERFECT_ERM_MS = 150;
const unsigned long MISS_ERM_MS = 200;

// ------------------------------------------------------------
// Actuator state reported back to Unity
// S1/S2/S3 = commanded solenoid duty
// SP1/SP2/SP3 = commanded solenoid phase
// E1/E2/E3 = commanded ERM duty
// ------------------------------------------------------------
float solenoidDuty[3] = {0.0f, 0.0f, 0.0f};
bool solenoidPhase[3] = {SOLENOID_ACTIVE_PHASE, SOLENOID_ACTIVE_PHASE, SOLENOID_ACTIVE_PHASE};
unsigned long solenoidOffTime[3] = {0, 0, 0};
bool solenoidRampActive[3] = {false, false, false};
float solenoidRampStartDuty[3] = {0.0f, 0.0f, 0.0f};
float solenoidRampTargetDuty[3] = {0.0f, 0.0f, 0.0f};
int solenoidRampCurve[3] = {0, 0, 0};
unsigned long solenoidRampStartTime[3] = {0, 0, 0};
unsigned long solenoidRampDurationMs[3] = {0, 0, 0};
unsigned long solenoidRampHoldMs[3] = {0, 0, 0};

float ermDuty[3] = {0.0f, 0.0f, 0.0f};
unsigned long ermOffTime[3] = {0, 0, 0};
bool ermRampActive[3] = {false, false, false};
float ermRampStartDuty[3] = {0.0f, 0.0f, 0.0f};
float ermRampTargetDuty[3] = {0.0f, 0.0f, 0.0f};
int ermRampCurve[3] = {0, 0, 0};
unsigned long ermRampStartTime[3] = {0, 0, 0};
unsigned long ermRampDurationMs[3] = {0, 0, 0};
unsigned long ermRampHoldMs[3] = {0, 0, 0};

bool hapticTestSequenceActive[3] = {false, false, false};
unsigned long hapticTestSolenoidStartTime[3] = {0, 0, 0};
unsigned long hapticTestSolenoidRampMs[3] = {0, 0, 0};
int hapticTestSolenoidCurve[3] = {0, 0, 0};
unsigned long hapticTestSolenoidHoldMs[3] = {0, 0, 0};
float hapticTestSolenoidPeakDuty[3] = {0.0f, 0.0f, 0.0f};

// ------------------------------------------------------------
// Serial command buffer from Unity
// ------------------------------------------------------------
char serialBuffer[128];
int serialBufferIndex = 0;

// ------------------------------------------------------------
// Convert duty fraction to 8-bit PWM
// ------------------------------------------------------------
int dutyToPWM(float dutyFraction)
{
    dutyFraction = constrain(dutyFraction, 0.0f, 1.0f);
    return (int)(255.0f * dutyFraction);
}

float applyRampCurve(float t, int curve)
{
    t = constrain(t, 0.0f, 1.0f);

    if (curve == 1)
    {
        return t * t;
    }

    if (curve == 2)
    {
        if (t <= 0.0f)
        {
            return 0.0f;
        }

        return constrain(pow(2.0f, 10.0f * (t - 1.0f)), 0.0f, 1.0f);
    }

    return t;
}

void writeSolenoidDuty(int channel, float dutyFraction)
{
    if (channel < 0 || channel > 2)
    {
        return;
    }

    dutyFraction = constrain(dutyFraction, 0.0f, MAX_SOLENOID_DUTY);
    solenoidDuty[channel] = dutyFraction;
    solenoidPhase[channel] = SOLENOID_ACTIVE_PHASE;

    digitalWrite(SOLENOID_PHASE[channel], SOLENOID_ACTIVE_PHASE);
    analogWrite(SOLENOID_PWM[channel], dutyToPWM(dutyFraction));
}

void writeERMDuty(int channel, float dutyFraction)
{
    if (!ERM_HARDWARE_CONNECTED)
    {
        return;
    }

    if (channel < 0 || channel > 2)
    {
        return;
    }

    dutyFraction = constrain(dutyFraction, 0.0f, MAX_ERM_DUTY);
    ermDuty[channel] = dutyFraction;

    analogWrite(ERM_IN1[channel], dutyToPWM(dutyFraction));
    analogWrite(ERM_IN2[channel], 0);
}

// ------------------------------------------------------------
// Millis-safe timeout check
// ------------------------------------------------------------
bool timeReached(unsigned long targetTime)
{
    return ((long)(millis() - targetTime) >= 0);
}

// ------------------------------------------------------------
// Select one TCA9548A channel
// ------------------------------------------------------------
bool selectTCAChannel(uint8_t channel)
{
    if (channel > 7)
    {
        return false;
    }

    Wire.beginTransmission(TCA_ADDR);
    Wire.write(1 << channel);
    return Wire.endTransmission() == 0;
}

// ------------------------------------------------------------
// Solenoid control
// channel: 0,1,2
// dutyFraction: 0.0 to 1.0
// phase argument is accepted for command compatibility but ignored.
// Hardware always uses phase 1.
// durationMs: 0 = stay on until changed
// ------------------------------------------------------------
void setSolenoid(int channel, float dutyFraction, bool ignoredPhase, unsigned long durationMs = 0)
{
    if (channel < 0 || channel > 2)
    {
        return;
    }

    dutyFraction = constrain(dutyFraction, 0.0f, MAX_SOLENOID_DUTY);

    if (dutyFraction > 0.0f)
    {
        if (durationMs == 0)
        {
            durationMs = DEFAULT_SOLENOID_PULSE_MS;
        }

        durationMs = constrain(durationMs, 1UL, MAX_SOLENOID_PULSE_MS);
    }

    solenoidRampActive[channel] = false;

    writeSolenoidDuty(channel, dutyFraction);

    solenoidOffTime[channel] = (durationMs > 0 && dutyFraction > 0.0f)
                               ? millis() + durationMs
                               : 0;
}

// ------------------------------------------------------------
// Non-blocking solenoid ramp for bench feel testing.
// curve: 0 = linear, 1 = quadratic, 2 = exponential.
// ------------------------------------------------------------
void rampSolenoid(
    int channel,
    unsigned long rampMs,
    int curve,
    unsigned long holdMs,
    float peakDuty
)
{
    if (channel < 0 || channel > 2)
    {
        return;
    }

    rampMs = constrain(rampMs, 1UL, MAX_SOLENOID_RAMP_MS);
    holdMs = constrain(holdMs, 0UL, MAX_SOLENOID_PULSE_MS);
    curve = constrain(curve, 0, 2);
    peakDuty = constrain(peakDuty, 0.0f, MAX_SOLENOID_DUTY);

    solenoidRampStartDuty[channel] = solenoidDuty[channel];
    solenoidRampTargetDuty[channel] = peakDuty;
    solenoidRampCurve[channel] = curve;
    solenoidRampStartTime[channel] = millis();
    solenoidRampDurationMs[channel] = rampMs;
    solenoidRampHoldMs[channel] = holdMs;
    solenoidRampActive[channel] = true;
    solenoidOffTime[channel] = 0;
}

// ------------------------------------------------------------
// ERM control through DRV8833
// One-direction vibration: IN1 = PWM, IN2 = LOW
// ------------------------------------------------------------
void setERM(int channel, float dutyFraction, unsigned long durationMs = 0)
{
    if (!ERM_HARDWARE_CONNECTED)
    {
        return;
    }

    if (channel < 0 || channel > 2)
    {
        return;
    }

    dutyFraction = constrain(dutyFraction, 0.0f, MAX_ERM_DUTY);

    if (durationMs > 0)
    {
        durationMs = constrain(durationMs, 1UL, MAX_ERM_PULSE_MS);
    }

    ermRampActive[channel] = false;

    writeERMDuty(channel, dutyFraction);

    ermOffTime[channel] = (durationMs > 0 && dutyFraction > 0.0f)
                          ? millis() + durationMs
                          : 0;
}

// ------------------------------------------------------------
// Non-blocking ERM ramp.
// Used by note pre-cues so serial input and ToF streaming keep running.
// ------------------------------------------------------------
void rampERM(
    int channel,
    float targetDuty,
    unsigned long rampMs,
    unsigned long holdMs = 0,
    int curve = 0
)
{
    if (!ERM_HARDWARE_CONNECTED)
    {
        return;
    }

    if (channel < 0 || channel > 2)
    {
        return;
    }

    targetDuty = constrain(targetDuty, 0.0f, MAX_ERM_DUTY);
    rampMs = constrain(rampMs, 1UL, MAX_ERM_RAMP_MS);
    curve = constrain(curve, 0, 2);

    if (holdMs > 0)
    {
        holdMs = constrain(holdMs, 1UL, MAX_ERM_PULSE_MS);
    }

    ermRampStartDuty[channel] = ermDuty[channel];
    ermRampTargetDuty[channel] = targetDuty;
    ermRampCurve[channel] = curve;
    ermRampStartTime[channel] = millis();
    ermRampDurationMs[channel] = rampMs;
    ermRampHoldMs[channel] = holdMs;
    ermRampActive[channel] = true;
    ermOffTime[channel] = 0;
}

void cancelHapticTestSequence(int channel)
{
    if (channel < 0 || channel > 2)
    {
        return;
    }

    hapticTestSequenceActive[channel] = false;
}

// ------------------------------------------------------------
// Team serial-tester compatible sequence.
// TEST runs lane 1 exactly like the team's single-channel tester.
// TESTCH adds a lane prefix for Unity multi-valve tuning.
// ------------------------------------------------------------
void startHapticTestSequence(
    int channel,
    unsigned long eRamp,
    int eFunc,
    unsigned long eHold,
    float ePeak,
    unsigned long delayMs,
    unsigned long sRamp,
    int sFunc,
    unsigned long sHold,
    float sPeak
)
{
    if (channel < 0 || channel > 2)
    {
        return;
    }

    eRamp = constrain(eRamp, 1UL, MAX_ERM_RAMP_MS);
    eFunc = constrain(eFunc, 0, 2);
    eHold = constrain(eHold, 0UL, MAX_ERM_PULSE_MS);
    ePeak = constrain(ePeak, 0.0f, MAX_ERM_DUTY);
    delayMs = constrain(delayMs, 0UL, MAX_HAPTIC_TEST_DELAY_MS);

    sRamp = constrain(sRamp, 1UL, MAX_SOLENOID_RAMP_MS);
    sFunc = constrain(sFunc, 0, 2);
    sHold = constrain(sHold, 0UL, MAX_SOLENOID_PULSE_MS);
    sPeak = constrain(sPeak, 0.0f, MAX_SOLENOID_DUTY);

    unsigned long now = millis();

    hapticTestSequenceActive[channel] = true;
    hapticTestSolenoidStartTime[channel] = now + eRamp + eHold + delayMs;
    hapticTestSolenoidRampMs[channel] = sRamp;
    hapticTestSolenoidCurve[channel] = sFunc;
    hapticTestSolenoidHoldMs[channel] = sHold;
    hapticTestSolenoidPeakDuty[channel] = sPeak;

    rampERM(channel, ePeak, eRamp, eHold, eFunc);
}

void updateHapticTestSequence(int channel)
{
    if (channel < 0 || channel > 2 || !hapticTestSequenceActive[channel])
    {
        return;
    }

    if (!timeReached(hapticTestSolenoidStartTime[channel]))
    {
        return;
    }

    hapticTestSequenceActive[channel] = false;
    rampSolenoid(
        channel,
        hapticTestSolenoidRampMs[channel],
        hapticTestSolenoidCurve[channel],
        hapticTestSolenoidHoldMs[channel],
        hapticTestSolenoidPeakDuty[channel]
    );
}

// ------------------------------------------------------------
// Turn all outputs off immediately
// ------------------------------------------------------------
void allOutputsOff()
{
    for (int i = 0; i < 3; i++)
    {
        cancelHapticTestSequence(i);
        setSolenoid(i, 0.0f, SOLENOID_ACTIVE_PHASE, 0);
        setERM(i, 0.0f, 0);
    }
}

// ------------------------------------------------------------
// Auto-off timed solenoid/ERM pulses
// ------------------------------------------------------------
void updateTimedOutputs()
{
    unsigned long now = millis();

    for (int i = 0; i < 3; i++)
    {
        if (solenoidRampActive[i])
        {
            unsigned long elapsed = now - solenoidRampStartTime[i];

            if (elapsed >= solenoidRampDurationMs[i])
            {
                writeSolenoidDuty(i, solenoidRampTargetDuty[i]);
                solenoidRampActive[i] = false;

                solenoidOffTime[i] = solenoidRampTargetDuty[i] > 0.0f
                                     ? now + solenoidRampHoldMs[i]
                                     : 0;
            }
            else
            {
                float t = (float)elapsed / (float)solenoidRampDurationMs[i];
                float rampT = applyRampCurve(t, solenoidRampCurve[i]);
                float duty = solenoidRampStartDuty[i] +
                             (solenoidRampTargetDuty[i] - solenoidRampStartDuty[i]) * rampT;

                writeSolenoidDuty(i, duty);
            }
        }

        if (ermRampActive[i])
        {
            unsigned long elapsed = now - ermRampStartTime[i];

            if (elapsed >= ermRampDurationMs[i])
            {
                writeERMDuty(i, ermRampTargetDuty[i]);
                ermRampActive[i] = false;

                ermOffTime[i] = ermRampTargetDuty[i] > 0.0f
                                ? now + ermRampHoldMs[i]
                                : 0;
            }
            else
            {
                float t = (float)elapsed / (float)ermRampDurationMs[i];
                float rampT = applyRampCurve(t, ermRampCurve[i]);
                float duty = ermRampStartDuty[i] +
                             (ermRampTargetDuty[i] - ermRampStartDuty[i]) * rampT;

                writeERMDuty(i, duty);
            }
        }

        if (!solenoidRampActive[i] && solenoidOffTime[i] != 0 && timeReached(solenoidOffTime[i]))
        {
            setSolenoid(i, 0.0f, SOLENOID_ACTIVE_PHASE, 0);
        }

        if (!ermRampActive[i] && ermOffTime[i] != 0 && timeReached(ermOffTime[i]))
        {
            setERM(i, 0.0f, 0);
        }

        updateHapticTestSequence(i);
    }
}

// ------------------------------------------------------------
// Initialize ToF sensors through TCA9548A
// ------------------------------------------------------------
void initializeTOF()
{
    for (int i = 0; i < 3; i++)
    {
        uint8_t channel = TOF_CHANNELS[i];

        if (!selectTCAChannel(channel))
        {
            tofAvailable[i] = false;
            continue;
        }

        tofAvailable[i] = tof.begin();
    }
}

void retryUnavailableTOF()
{
    unsigned long now = millis();

    if (now - lastTofRetryTime < TOF_RETRY_INTERVAL_MS)
    {
        return;
    }

    lastTofRetryTime = now;

    for (int i = 0; i < 3; i++)
    {
        if (tofAvailable[i])
        {
            continue;
        }

        if (!selectTCAChannel(TOF_CHANNELS[i]))
        {
            continue;
        }

        tofAvailable[i] = tof.begin();
    }
}

// ------------------------------------------------------------
// Read raw ToF distance in mm
// Returns 255 if unavailable or invalid.
// ------------------------------------------------------------
uint8_t readRawTOFmm(int sensorIndex)
{
    if (sensorIndex < 0 || sensorIndex > 2)
    {
        return INVALID_DISTANCE;
    }

    if (!tofAvailable[sensorIndex])
    {
        return INVALID_DISTANCE;
    }

    if (!selectTCAChannel(TOF_CHANNELS[sensorIndex]))
    {
        return INVALID_DISTANCE;
    }

    uint8_t range = tof.readRange();
    uint8_t status = tof.readRangeStatus();

    if (status != VL6180X_ERROR_NONE)
    {
        return INVALID_DISTANCE;
    }

    return range;
}

// ------------------------------------------------------------
// Raw debug press state only.
// Unity discrete tracker uses D1/D2/D3 directly.
// ------------------------------------------------------------
int rawDistanceToPressed(uint8_t distanceMM)
{
    if (distanceMM == INVALID_DISTANCE)
    {
        return 0;
    }

    return distanceMM <= pressThresholdMM ? 1 : 0;
}

// ------------------------------------------------------------
// Send Unity all live hardware state
// D1/D2/D3 are raw distances.
// S1/S2/S3 are commanded solenoid duty.
// SP1/SP2/SP3 are commanded solenoid phase.
// E1/E2/E3 are commanded ERM duty.
// TH is current raw Teensy debug threshold.
// ------------------------------------------------------------
void sendUnityState()
{
    uint8_t d1 = readRawTOFmm(0);
    uint8_t d2 = readRawTOFmm(1);
    uint8_t d3 = readRawTOFmm(2);

    int v1 = rawDistanceToPressed(d1);
    int v2 = rawDistanceToPressed(d2);
    int v3 = rawDistanceToPressed(d3);

    Serial.print("V1:");
    Serial.print(v1);
    Serial.print(",V2:");
    Serial.print(v2);
    Serial.print(",V3:");
    Serial.print(v3);

    Serial.print(",D1:");
    Serial.print(d1);
    Serial.print(",D2:");
    Serial.print(d2);
    Serial.print(",D3:");
    Serial.print(d3);

    Serial.print(",S1:");
    Serial.print(solenoidDuty[0], 2);
    Serial.print(",S2:");
    Serial.print(solenoidDuty[1], 2);
    Serial.print(",S3:");
    Serial.print(solenoidDuty[2], 2);

    Serial.print(",SP1:");
    Serial.print(solenoidPhase[0] ? 1 : 0);
    Serial.print(",SP2:");
    Serial.print(solenoidPhase[1] ? 1 : 0);
    Serial.print(",SP3:");
    Serial.print(solenoidPhase[2] ? 1 : 0);

    Serial.print(",E1:");
    Serial.print(ermDuty[0], 2);
    Serial.print(",E2:");
    Serial.print(ermDuty[1], 2);
    Serial.print(",E3:");
    Serial.print(ermDuty[2], 2);

    Serial.print(",TH:");
    Serial.print(pressThresholdMM);

    Serial.println();
}

// ------------------------------------------------------------
// Gameplay command: note pre-cue
// ERM ramps up before note reaches valve target.
// ------------------------------------------------------------
void handlePreCueCommand(
    int lane,
    float duty = PRECUE_ERM_DUTY,
    unsigned long rampMs = PRECUE_ERM_RAMP_MS,
    unsigned long holdMs = PRECUE_ERM_HOLD_MS,
    int curve = PRECUE_ERM_CURVE
)
{
    int channel = lane - 1;

    if (channel < 0 || channel > 2)
    {
        return;
    }

    cancelHapticTestSequence(channel);
    rampERM(channel, duty, rampMs, holdMs, curve);
}

// ------------------------------------------------------------
// Gameplay command: tap note complete
// This is the main solenoid push-off after a successful tap note.
// ------------------------------------------------------------
void handleTapCompleteCommand(int lane, const char *rating)
{
    int channel = lane - 1;

    if (channel < 0 || channel > 2)
    {
        return;
    }

    cancelHapticTestSequence(channel);
    bool perfect = strcmp(rating, "PERFECT") == 0;

    float solDuty = perfect ? TAP_RESET_SOL_DUTY_PERFECT : TAP_RESET_SOL_DUTY_GOOD;
    unsigned long solDuration = TAP_RESET_SOL_MS;

    setSolenoid(channel, solDuty, SOLENOID_ACTIVE_PHASE, solDuration);

    if (perfect)
    {
        setERM(channel, PERFECT_ERM_DUTY, PERFECT_ERM_MS);
    }
    else
    {
        setERM(channel, GOOD_ERM_DUTY, GOOD_ERM_MS);
    }
}

// ------------------------------------------------------------
// Gameplay command: hold note start
// ERM runs continuously during hold, until hold completes or miss.
// ------------------------------------------------------------
void handleHoldStartCommand(int lane)
{
    int channel = lane - 1;

    if (channel < 0 || channel > 2)
    {
        return;
    }

    cancelHapticTestSequence(channel);
    setERM(channel, GOOD_ERM_DUTY, 0);
}

// ------------------------------------------------------------
// Gameplay command: hold note complete
// Stop ERM and push user off valve with solenoid.
// ------------------------------------------------------------
void handleHoldCompleteCommand(int lane)
{
    int channel = lane - 1;

    if (channel < 0 || channel > 2)
    {
        return;
    }

    cancelHapticTestSequence(channel);
    setERM(channel, 0.0f, 0);
    setSolenoid(channel, HOLD_RESET_SOL_DUTY, SOLENOID_ACTIVE_PHASE, HOLD_RESET_SOL_MS);
}

// ------------------------------------------------------------
// Gameplay command: note miss
// Push the valve back regardless of rating so every completed note state
// gives the user a clear physical release cue.
// ------------------------------------------------------------
void handleMissCommand(int lane)
{
    int channel = lane - 1;

    if (channel < 0 || channel > 2)
    {
        return;
    }

    cancelHapticTestSequence(channel);
    setSolenoid(channel, MISS_RESET_SOL_DUTY, SOLENOID_ACTIVE_PHASE, MISS_RESET_SOL_MS);
    setERM(channel, MISS_ERM_DUTY, MISS_ERM_MS);
}

// ------------------------------------------------------------
// Parse Unity command line
//
// MVP:
// X
// PRECUE,1
// PRECUE,1,1.00,500,200,0
// TAPCOMPLETE,1,PERFECT
// TAPCOMPLETE,1,GOOD
// HOLDSTART,1
// HOLDCOMPLETE,1
// MISS,1
//
// Bench tuning when ENABLE_TUNING_COMMANDS is true:
// THRESH,65
// SOL,1,0.80,1,125
// SOLRAMP,1,500,1,200,0.80
// ERM,1,0.25,120
// ERMRAMP,1,0.25,800,120
// TEST,1000,1,100,1,1000,800,1,800,1
// TESTCH,1,1000,1,100,1,1000,800,1,800,1
// ------------------------------------------------------------
void handleLineCommand(char *line)
{
    char *command = strtok(line, ",");

    if (command == NULL)
    {
        return;
    }

    if (strcmp(command, "X") == 0)
    {
        allOutputsOff();
        return;
    }

    if (ENABLE_TUNING_COMMANDS && strcmp(command, "TEST") == 0)
    {
        char *eRampToken = strtok(NULL, ",");
        char *eFuncToken = strtok(NULL, ",");
        char *eHoldToken = strtok(NULL, ",");
        char *ePeakToken = strtok(NULL, ",");
        char *delayToken = strtok(NULL, ",");
        char *sRampToken = strtok(NULL, ",");
        char *sFuncToken = strtok(NULL, ",");
        char *sHoldToken = strtok(NULL, ",");
        char *sPeakToken = strtok(NULL, ",");

        if (eRampToken == NULL ||
            eFuncToken == NULL ||
            eHoldToken == NULL ||
            ePeakToken == NULL ||
            delayToken == NULL ||
            sRampToken == NULL ||
            sFuncToken == NULL ||
            sHoldToken == NULL ||
            sPeakToken == NULL)
        {
            return;
        }

        startHapticTestSequence(
            0,
            strtoul(eRampToken, NULL, 10),
            atoi(eFuncToken),
            strtoul(eHoldToken, NULL, 10),
            atof(ePeakToken),
            strtoul(delayToken, NULL, 10),
            strtoul(sRampToken, NULL, 10),
            atoi(sFuncToken),
            strtoul(sHoldToken, NULL, 10),
            atof(sPeakToken)
        );
        return;
    }

    if (ENABLE_TUNING_COMMANDS && strcmp(command, "TESTCH") == 0)
    {
        char *laneToken = strtok(NULL, ",");
        char *eRampToken = strtok(NULL, ",");
        char *eFuncToken = strtok(NULL, ",");
        char *eHoldToken = strtok(NULL, ",");
        char *ePeakToken = strtok(NULL, ",");
        char *delayToken = strtok(NULL, ",");
        char *sRampToken = strtok(NULL, ",");
        char *sFuncToken = strtok(NULL, ",");
        char *sHoldToken = strtok(NULL, ",");
        char *sPeakToken = strtok(NULL, ",");

        if (laneToken == NULL ||
            eRampToken == NULL ||
            eFuncToken == NULL ||
            eHoldToken == NULL ||
            ePeakToken == NULL ||
            delayToken == NULL ||
            sRampToken == NULL ||
            sFuncToken == NULL ||
            sHoldToken == NULL ||
            sPeakToken == NULL)
        {
            return;
        }

        startHapticTestSequence(
            atoi(laneToken) - 1,
            strtoul(eRampToken, NULL, 10),
            atoi(eFuncToken),
            strtoul(eHoldToken, NULL, 10),
            atof(ePeakToken),
            strtoul(delayToken, NULL, 10),
            strtoul(sRampToken, NULL, 10),
            atoi(sFuncToken),
            strtoul(sHoldToken, NULL, 10),
            atof(sPeakToken)
        );
        return;
    }

    if (ENABLE_TUNING_COMMANDS && strcmp(command, "THRESH") == 0)
    {
        char *thresholdToken = strtok(NULL, ",");

        if (thresholdToken == NULL)
        {
            return;
        }

        pressThresholdMM = constrain(atoi(thresholdToken), 1, 254);
        return;
    }

    if (ENABLE_TUNING_COMMANDS && strcmp(command, "SOL") == 0)
    {
        char *chToken = strtok(NULL, ",");
        char *dutyToken = strtok(NULL, ",");
        char *phaseToken = strtok(NULL, ",");
        char *msToken = strtok(NULL, ",");

        if (chToken == NULL || dutyToken == NULL)
        {
            return;
        }

        int ch = atoi(chToken) - 1;
        float duty = atof(dutyToken);

        // Phase token is accepted for old command compatibility but ignored.
        unsigned long ms = 0;

        if (msToken != NULL)
        {
            ms = strtoul(msToken, NULL, 10);
        }
        else if (phaseToken != NULL && atoi(phaseToken) > 1)
        {
            // Legacy form: SOL,channel,duty,durationMs.
            ms = strtoul(phaseToken, NULL, 10);
        }

        cancelHapticTestSequence(ch);
        setSolenoid(ch, duty, SOLENOID_ACTIVE_PHASE, ms);
        return;
    }

    if (ENABLE_TUNING_COMMANDS && strcmp(command, "SOLRAMP") == 0)
    {
        char *chToken = strtok(NULL, ",");
        char *rampToken = strtok(NULL, ",");
        char *curveToken = strtok(NULL, ",");
        char *holdToken = strtok(NULL, ",");
        char *peakToken = strtok(NULL, ",");

        if (chToken == NULL || rampToken == NULL || curveToken == NULL || holdToken == NULL || peakToken == NULL)
        {
            return;
        }

        int ch = atoi(chToken) - 1;
        unsigned long rampMs = strtoul(rampToken, NULL, 10);
        int curve = atoi(curveToken);
        unsigned long holdMs = strtoul(holdToken, NULL, 10);
        float peakDuty = atof(peakToken);

        cancelHapticTestSequence(ch);
        rampSolenoid(ch, rampMs, curve, holdMs, peakDuty);
        return;
    }

    if (ENABLE_TUNING_COMMANDS && strcmp(command, "ERM") == 0)
    {
        char *chToken = strtok(NULL, ",");
        char *dutyToken = strtok(NULL, ",");
        char *msToken = strtok(NULL, ",");

        if (chToken == NULL || dutyToken == NULL)
        {
            return;
        }

        int ch = atoi(chToken) - 1;
        float duty = atof(dutyToken);
        unsigned long ms = msToken == NULL ? 0 : strtoul(msToken, NULL, 10);

        cancelHapticTestSequence(ch);
        setERM(ch, duty, ms);
        return;
    }

    if (ENABLE_TUNING_COMMANDS && strcmp(command, "ERMRAMP") == 0)
    {
        char *chToken = strtok(NULL, ",");
        char *dutyToken = strtok(NULL, ",");
        char *rampToken = strtok(NULL, ",");
        char *holdToken = strtok(NULL, ",");
        char *curveToken = strtok(NULL, ",");

        if (chToken == NULL || dutyToken == NULL)
        {
            return;
        }

        int ch = atoi(chToken) - 1;
        float duty = atof(dutyToken);
        unsigned long rampMs = rampToken == NULL
                               ? PRECUE_ERM_RAMP_MS
                               : strtoul(rampToken, NULL, 10);
        unsigned long holdMs = holdToken == NULL
                               ? PRECUE_ERM_HOLD_MS
                               : strtoul(holdToken, NULL, 10);
        int curve = curveToken == NULL ? 0 : atoi(curveToken);

        cancelHapticTestSequence(ch);
        rampERM(ch, duty, rampMs, holdMs, curve);
        return;
    }

    if (strcmp(command, "PRECUE") == 0)
    {
        char *laneToken = strtok(NULL, ",");
        char *dutyToken = strtok(NULL, ",");
        char *rampToken = strtok(NULL, ",");
        char *holdToken = strtok(NULL, ",");
        char *curveToken = strtok(NULL, ",");

        if (laneToken == NULL)
        {
            return;
        }

        float duty = dutyToken == NULL ? PRECUE_ERM_DUTY : atof(dutyToken);
        unsigned long rampMs = rampToken == NULL
                               ? PRECUE_ERM_RAMP_MS
                               : strtoul(rampToken, NULL, 10);
        unsigned long holdMs = holdToken == NULL
                               ? PRECUE_ERM_HOLD_MS
                               : strtoul(holdToken, NULL, 10);
        int curve = curveToken == NULL ? PRECUE_ERM_CURVE : atoi(curveToken);

        handlePreCueCommand(atoi(laneToken), duty, rampMs, holdMs, curve);
        return;
    }

    if (strcmp(command, "TAPCOMPLETE") == 0)
    {
        char *laneToken = strtok(NULL, ",");
        char *ratingToken = strtok(NULL, ",");

        if (laneToken == NULL || ratingToken == NULL)
        {
            return;
        }

        handleTapCompleteCommand(atoi(laneToken), ratingToken);
        return;
    }

    if (strcmp(command, "HOLDSTART") == 0)
    {
        char *laneToken = strtok(NULL, ",");

        if (laneToken == NULL)
        {
            return;
        }

        handleHoldStartCommand(atoi(laneToken));
        return;
    }

    if (strcmp(command, "HOLDCOMPLETE") == 0)
    {
        char *laneToken = strtok(NULL, ",");

        if (laneToken == NULL)
        {
            return;
        }

        handleHoldCompleteCommand(atoi(laneToken));
        return;
    }

    if (strcmp(command, "MISS") == 0)
    {
        char *laneToken = strtok(NULL, ",");

        if (laneToken == NULL)
        {
            return;
        }

        handleMissCommand(atoi(laneToken));
        return;
    }
}

// ------------------------------------------------------------
// Read Unity serial commands
// ------------------------------------------------------------
void readUnityCommands()
{
    while (Serial.available() > 0)
    {
        char c = Serial.read();

        if (c == '\n' || c == '\r')
        {
            if (serialBufferIndex > 0)
            {
                serialBuffer[serialBufferIndex] = '\0';
                handleLineCommand(serialBuffer);
                serialBufferIndex = 0;
            }

            continue;
        }

        if (serialBufferIndex < (int)(sizeof(serialBuffer) - 1))
        {
            serialBuffer[serialBufferIndex++] = c;
        }
        else
        {
            serialBufferIndex = 0;
        }
    }
}

// ------------------------------------------------------------
// Setup
// ------------------------------------------------------------
void setup()
{
    Serial.begin(115200);

    analogWriteResolution(8);

    Wire.begin();
    Wire.setClock(400000);

    for (int i = 0; i < 3; i++)
    {
        pinMode(SOLENOID_PWM[i], OUTPUT);
        pinMode(SOLENOID_PHASE[i], OUTPUT);

        pinMode(ERM_IN1[i], OUTPUT);
        pinMode(ERM_IN2[i], OUTPUT);

        analogWriteFrequency(SOLENOID_PWM[i], 20000);
        analogWriteFrequency(ERM_IN1[i], 20000);
        analogWriteFrequency(ERM_IN2[i], 20000);

        digitalWrite(SOLENOID_PHASE[i], SOLENOID_ACTIVE_PHASE);
    }

    allOutputsOff();

    delay(300);

    initializeTOF();
}

// ------------------------------------------------------------
// Main loop
// ------------------------------------------------------------
void loop()
{
    readUnityCommands();
    updateTimedOutputs();

    unsigned long now = millis();

    if (now - lastUnitySendTime >= UNITY_SEND_INTERVAL_MS)
    {
        lastUnitySendTime = now;
        retryUnavailableTOF();
        sendUnityState();
    }
}
