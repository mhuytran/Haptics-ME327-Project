#include <Wire.h>

// Standalone Teensy 4.0 I2C scanner for the trumpet haptics rig.
// Default Teensy 4.0 I2C pins: SDA = 18, SCL = 19.
// Expected devices:
//   0x70 = TCA9548A I2C multiplexer
//   0x29 = VL6180X ToF sensor behind one or more mux channels

const uint8_t TCA_ADDR = 0x70;
const uint8_t VL6180X_ADDR = 0x29;
const unsigned long SCAN_INTERVAL_MS = 3000;

unsigned long lastScanTime = 0;

void printHexAddress(uint8_t address)
{
    Serial.print("0x");

    if (address < 16)
    {
        Serial.print("0");
    }

    Serial.print(address, HEX);
}

void printDeviceLabel(uint8_t address)
{
    if (address == TCA_ADDR)
    {
        Serial.print("  TCA9548A mux");
    }
    else if (address == VL6180X_ADDR)
    {
        Serial.print("  VL6180X ToF");
    }
}

bool probeAddress(uint8_t address)
{
    Wire.beginTransmission(address);
    return Wire.endTransmission() == 0;
}

bool setTCAChannelMask(uint8_t mask)
{
    Wire.beginTransmission(TCA_ADDR);
    Wire.write(mask);
    return Wire.endTransmission() == 0;
}

bool selectTCAChannel(uint8_t channel)
{
    if (channel > 7)
    {
        return false;
    }

    return setTCAChannelMask(1 << channel);
}

void disableTCAChannels()
{
    setTCAChannelMask(0);
}

int scanCurrentBus(const char *label)
{
    int foundCount = 0;

    Serial.println(label);

    for (uint8_t address = 1; address < 127; address++)
    {
        if (!probeAddress(address))
        {
            continue;
        }

        Serial.print("  FOUND ");
        printHexAddress(address);
        printDeviceLabel(address);
        Serial.println();

        foundCount++;
    }

    if (foundCount == 0)
    {
        Serial.println("  No I2C devices found.");
    }

    return foundCount;
}

void scanAllTCAChannels()
{
    Serial.println();
    Serial.println("Scanning TCA9548A channels SC0-SC7...");

    for (uint8_t channel = 0; channel < 8; channel++)
    {
        Serial.println();
        Serial.print("SC");
        Serial.print(channel);
        Serial.println(":");

        if (!selectTCAChannel(channel))
        {
            Serial.println("  Could not select this channel.");
            continue;
        }

        delay(5);

        int foundCount = 0;
        bool foundTof = false;

        for (uint8_t address = 1; address < 127; address++)
        {
            if (!probeAddress(address))
            {
                continue;
            }

            Serial.print("  FOUND ");
            printHexAddress(address);
            printDeviceLabel(address);
            Serial.println();

            if (address == VL6180X_ADDR)
            {
                foundTof = true;
            }

            foundCount++;
        }

        if (foundCount == 0)
        {
            Serial.println("  No I2C devices found.");
        }

        if (foundTof)
        {
            Serial.print("  -> VL6180X present on SC");
            Serial.println(channel);
        }

        disableTCAChannels();
        delay(5);
    }
}

void runScan()
{
    Serial.println();
    Serial.println("==================================================");
    Serial.println("Teensy 4.0 I2C Scanner");
    Serial.println("Bus: Wire, SDA 18, SCL 19, 400 kHz");
    Serial.println("Expected: 0x70 TCA9548A, 0x29 VL6180X");
    Serial.println("==================================================");

    bool hasMux = probeAddress(TCA_ADDR);

    if (hasMux)
    {
        disableTCAChannels();
        delay(5);
    }

    scanCurrentBus("Main I2C bus:");

    if (!hasMux)
    {
        Serial.println();
        Serial.println("TCA9548A mux was not found at 0x70. Check SDA/SCL, power, ground, and address pins.");
        return;
    }

    scanAllTCAChannels();

    disableTCAChannels();

    Serial.println();
    Serial.println("Scan complete. All TCA9548A channels disabled.");
}

void setup()
{
    Serial.begin(115200);

    unsigned long serialWaitStart = millis();

    while (!Serial && millis() - serialWaitStart < 3000)
    {
        delay(10);
    }

    Wire.begin();
    Wire.setClock(400000);

    Serial.println("I2C scanner ready.");
    Serial.println("Open Serial Monitor at 115200 baud.");

    runScan();
    lastScanTime = millis();
}

void loop()
{
    if (millis() - lastScanTime < SCAN_INTERVAL_MS)
    {
        return;
    }

    lastScanTime = millis();
    runScan();
}
