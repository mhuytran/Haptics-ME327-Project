// ============================================================================
// Haptic Serial Tester (ERM & Solenoid)
// ============================================================================
// Send commands over the Serial Monitor to sequentially trigger the ERM 
// and then the Solenoid with custom ramp parameters and a delay.
//
// Format: TEST,<eRamp>,<eFunc>,<eHold>,<ePeak>,<delayMs>,<sRamp>,<sFunc>,<sHold>,<sPeak>
// 
// eRamp / sRamp : Time in ms to reach peak PWM
// eFunc / sFunc : 0 = Linear, 1 = Quadratic, 2 = Exponential
// eHold / sHold : Time in ms to hold at peak PWM
// ePeak / sPeak : Peak duty cycle (0.0 to 1.0)
// delayMs       : Time in ms to wait AFTER the ERM finishes before starting the Solenoid
//
// Example: TEST,300,1,150,0.5,1000,500,0,200,0.8
// (ERM: 300ms quad ramp, 150ms hold, 50% max)
// (Wait 1000ms)
// (Solenoid: 500ms linear ramp, 200ms hold, 80% max)
// ============================================================================

#include <Arduino.h>

// ERM 1 Pins
const int ERM_IN1 = 12;
const int ERM_IN2 = 13;

// Solenoid 1 Pins
const int SOLENOID_PWM = 3;
const int SOLENOID_PHASE = 5;

// Variables for parsing Serial commands
char serialBuffer[128];
int serialBufferIndex = 0;

void setup() {
  Serial.begin(115200);
  
  pinMode(SOLENOID_PWM, OUTPUT);
  pinMode(SOLENOID_PHASE, OUTPUT);
  pinMode(ERM_IN1, OUTPUT);
  pinMode(ERM_IN2, OUTPUT);

  // Teensy 4.0 high frequency PWM
  analogWriteFrequency(SOLENOID_PWM, 20000);
  analogWriteFrequency(ERM_IN1, 20000);
  analogWriteFrequency(ERM_IN2, 20000);
  
  analogWriteResolution(8);

  // Initial State: OFF
  digitalWrite(SOLENOID_PHASE, HIGH); // Default actuation phase
  analogWrite(SOLENOID_PWM, 0);
  analogWrite(ERM_IN1, 0);
  analogWrite(ERM_IN2, 0);

  // Wait for Serial connection
  unsigned long startWait = millis();
  while (!Serial && millis() - startWait < 1500) {}

  Serial.println("\n=== Haptic Serial Tester Ready ===");
  Serial.println("Send commands in this format:");
  Serial.println("TEST,<eRamp>,<eFunc>,<eHold>,<ePeak>,<delayMs>,<sRamp>,<sFunc>,<sHold>,<sPeak>");
  Serial.println("Functions: 0=Linear, 1=Quadratic, 2=Exponential");
  Serial.println("Example: TEST,300,1,150,0.5,1000,500,0,200,0.8\n");
}

int dutyToPWM(float dutyFraction) {
  dutyFraction = constrain(dutyFraction, 0.0f, 1.0f);
  return (int)(255.0f * dutyFraction);
}

// ----------------------------------------------------------------------------
// Blocking Actuation Helper
// ----------------------------------------------------------------------------
void actuateDevice(const char* name, int pwmPin, unsigned long rampTime, int func, unsigned long holdTime, float peakPWM) {
  Serial.print("Actuating ");
  Serial.print(name);
  Serial.print("... Ramp: ");
  Serial.print(rampTime);
  Serial.print("ms, Func: ");
  Serial.print(func);
  Serial.print(", Hold: ");
  Serial.print(holdTime);
  Serial.print("ms, Peak: ");
  Serial.println(peakPWM);

  unsigned long startTime = millis();
  
  // RAMP PHASE
  while (true) {
    unsigned long elapsed = millis() - startTime;
    if (elapsed >= rampTime) break;

    float t = (float)elapsed / (float)rampTime;
    float multiplier = 1.0f;

    switch (func) {
      case 0: // Linear
        multiplier = t;
        break;
      case 1: // Quadratic
        multiplier = t * t;
        break;
      case 2: // Exponential
        if (t == 0) {
          multiplier = 0;
        } else {
          multiplier = pow(2, 10 * (t - 1));
        }
        break;
      default:
        multiplier = t;
        break;
    }

    float currentDuty = peakPWM * multiplier;
    analogWrite(pwmPin, dutyToPWM(currentDuty));
  }

  // HOLD PHASE
  analogWrite(pwmPin, dutyToPWM(peakPWM));
  unsigned long holdStartTime = millis();
  while (millis() - holdStartTime < holdTime) {
    // Wait for hold duration
  }

  // TURN OFF
  analogWrite(pwmPin, 0);
}

// ----------------------------------------------------------------------------
// Dual Actuation Sequence
// ----------------------------------------------------------------------------
void actuateSequence(unsigned long eRamp, int eFunc, unsigned long eHold, float ePeak, 
                     unsigned long delayMs, 
                     unsigned long sRamp, int sFunc, unsigned long sHold, float sPeak) {
  
  // 1. Actuate ERM
  actuateDevice("ERM", ERM_IN1, eRamp, eFunc, eHold, ePeak);

  // 2. Wait Delay
  if (delayMs > 0) {
    Serial.print("Waiting delay ");
    Serial.print(delayMs);
    Serial.println("ms...");
    
    unsigned long delayStart = millis();
    while (millis() - delayStart < delayMs) {}
  }

  // 3. Actuate Solenoid
  actuateDevice("Solenoid", SOLENOID_PWM, sRamp, sFunc, sHold, sPeak);
  
  Serial.println("Sequence Complete.\n");
}

// ----------------------------------------------------------------------------
// Serial Parser
// ----------------------------------------------------------------------------
void handleLineCommand(char* line) {
  char* command = strtok(line, ",");
  
  if (command == NULL) return;

  if (strcmp(command, "TEST") == 0) {
    char* eRampT = strtok(NULL, ",");
    char* eFuncT = strtok(NULL, ",");
    char* eHoldT = strtok(NULL, ",");
    char* ePeakT = strtok(NULL, ",");
    char* delayT = strtok(NULL, ",");
    char* sRampT = strtok(NULL, ",");
    char* sFuncT = strtok(NULL, ",");
    char* sHoldT = strtok(NULL, ",");
    char* sPeakT = strtok(NULL, ",");

    if (sPeakT == NULL) {
      Serial.println("ERROR: Incomplete command.");
      Serial.println("Use: TEST,<eRamp>,<eFunc>,<eHold>,<ePeak>,<delayMs>,<sRamp>,<sFunc>,<sHold>,<sPeak>");
      return;
    }

    unsigned long eRamp = strtoul(eRampT, NULL, 10);
    int eFunc = atoi(eFuncT);
    unsigned long eHold = strtoul(eHoldT, NULL, 10);
    float ePeak = atof(ePeakT);
    
    unsigned long delayMs = strtoul(delayT, NULL, 10);
    
    unsigned long sRamp = strtoul(sRampT, NULL, 10);
    int sFunc = atoi(sFuncT);
    unsigned long sHold = strtoul(sHoldT, NULL, 10);
    float sPeak = atof(sPeakT);

    actuateSequence(eRamp, eFunc, eHold, ePeak, delayMs, sRamp, sFunc, sHold, sPeak);
    
  } else {
    Serial.println("ERROR: Unknown command.");
  }
}

void loop() {
  // Read Serial data into buffer
  while (Serial.available() > 0) {
    char c = Serial.read();

    if (c == '\n' || c == '\r') {
      if (serialBufferIndex > 0) {
        serialBuffer[serialBufferIndex] = '\0';
        handleLineCommand(serialBuffer);
        serialBufferIndex = 0;
      }
    } else {
      if (serialBufferIndex < (int)(sizeof(serialBuffer) - 1)) {
        serialBuffer[serialBufferIndex++] = c;
      } else {
        serialBufferIndex = 0;
        Serial.println("ERROR: Command too long. Buffer cleared.");
      }
    }
  }
}