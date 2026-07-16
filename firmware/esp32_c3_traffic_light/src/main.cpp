/**
 * AgentCore-Light v1 firmware for ESP32-C3.
 *
 * Drives a three-color (red/yellow/green) traffic-light via PWM LEDs and
 * accepts state commands over USB serial or BLE.
 *
 * Actual wiring (verified on the delivered board):
 *   - Red LED    -> GPIO21
 *   - Yellow LED -> GPIO2
 *   - Green LED  -> GPIO20
 *
 * The original .ino comment listed GPIO0/2/1, but the working hardware uses
 * GPIO21/2/20. Always change the three #defines below if the wiring changes.
 */

#include <Arduino.h>
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLESecurity.h>
#include <BLEUtils.h>

// Set to 1 if the LED board is active-high.
#define LED_ACTIVE_HIGH 0

// LED GPIOs. Update these when the hardware layout changes.
#define RED_LED_PIN 21
#define YELLOW_LED_PIN 2
#define GREEN_LED_PIN 20

// Independent PWM channels for each LED.
const uint8_t RED_LED_CHANNEL = 0;
const uint8_t YELLOW_LED_CHANNEL = 1;
const uint8_t GREEN_LED_CHANNEL = 2;

const uint32_t LED_PWM_FREQ = 1000;
const uint8_t LED_PWM_RESOLUTION = 8;
const uint8_t LED_PWM_MAX = (1 << LED_PWM_RESOLUTION) - 1;
const uint8_t LED_PWM_LIMIT = 140;  // ~55% duty cycle
const uint8_t LED_PWM_SOFT = 84;    // ~33% duty cycle
const uint8_t LED_PWM_TRAIL = 28;   // soft trail brightness
const uint8_t LED_BREATH_MIN = 6;

const unsigned long IDLE_BREATH_STEP_MS = 14;
const unsigned long THINKING_CHASE_INTERVAL_MS = 110;
const unsigned long AI_CHASE_INTERVAL_MS = 240;
const unsigned long BUSY_BLINK_INTERVAL_MS = 550;
const unsigned long SUCCESS_HOLD_MS = 5000;
const unsigned long ERROR_BLINK_INTERVAL_MS = 130;

const char *BLE_DEVICE_NAME = "AgentCore-Light";
const char *BLE_SERVICE_UUID = "12345678-1234-5678-1234-56789abcdef0";
const char *BLE_CHARACTERISTIC_UUID = "12345678-1234-5678-1234-56789abcdef1";

enum class LightState {
  Idle,
  Thinking,
  Ai,
  Busy,
  Success,
  WaitConfirm,
  Confirm,
  Waiting,
  Wait,
  Error,
  Off
};

LightState currentState = LightState::Off;
String serialBuffer;
BLEServer *bleServer = nullptr;

unsigned long stateStartMs = 0;
unsigned long lastEffectFrameMs = 0;
uint8_t chaseIndex = 0;
bool blinkOn = false;
int breathBrightness = LED_BREATH_MIN;
int breathStep = 2;

uint8_t brightnessToDuty(uint8_t brightness) {
  uint8_t limited = brightness;
  if (limited > LED_PWM_LIMIT) {
    limited = LED_PWM_LIMIT;
  }

#if LED_ACTIVE_HIGH
  return limited;
#else
  return LED_PWM_MAX - limited;
#endif
}

void writeLed(uint8_t channel, uint8_t brightness) {
  ledcWrite(channel, brightnessToDuty(brightness));
}

void setLightLevels(uint8_t red, uint8_t yellow, uint8_t green) {
  writeLed(RED_LED_CHANNEL, red);
  writeLed(YELLOW_LED_CHANNEL, yellow);
  writeLed(GREEN_LED_CHANNEL, green);
}

void setLight(bool red, bool yellow, bool green) {
  setLightLevels(
    red ? LED_PWM_LIMIT : 0,
    yellow ? LED_PWM_LIMIT : 0,
    green ? LED_PWM_LIMIT : 0);
}

void resetEffectState() {
  lastEffectFrameMs = 0;
  chaseIndex = 0;
  blinkOn = false;
  breathBrightness = LED_BREATH_MIN;
  breathStep = 2;
}

void enterState(LightState newState) {
  currentState = newState;
  stateStartMs = millis();
  resetEffectState();
  setLight(false, false, false);
}

bool setStatus(const String &status) {
  String lowered = status;
  lowered.trim();
  lowered.toLowerCase();

  if (lowered.length() == 0) {
    return false;
  }

  if (lowered == "idle") {
    enterState(LightState::Idle);
  } else if (lowered == "thinking") {
    enterState(LightState::Thinking);
  } else if (lowered == "ai" || lowered == "writing") {
    enterState(LightState::Ai);
  } else if (lowered == "busy" || lowered == "running") {
    enterState(LightState::Busy);
  } else if (lowered == "success" || lowered == "done") {
    enterState(LightState::Success);
  } else if (lowered == "wait_confirm") {
    enterState(LightState::WaitConfirm);
  } else if (lowered == "confirm") {
    enterState(LightState::Confirm);
  } else if (lowered == "waiting") {
    enterState(LightState::Waiting);
  } else if (lowered == "wait") {
    enterState(LightState::Wait);
  } else if (lowered == "error") {
    enterState(LightState::Error);
  } else if (lowered == "off") {
    enterState(LightState::Off);
  } else {
    return false;
  }

  return true;
}

/**
 * Extracts the string value of a given key from a small JSON payload.
 *
 * Supports:
 *   - double-quoted and single-quoted keys/values
 *   - unquoted values
 *   - escaped characters inside quoted values
 */
String extractJsonStringValue(const String &json, const char *key) {
  const size_t keyLen = strlen(key);

  for (size_t i = 0; i + keyLen < json.length(); ++i) {
    // Locate the key, allowing leading whitespace.
    if (isspace(static_cast<unsigned char>(json[i]))) {
      continue;
    }

    char quote = json[i];
    if (quote != '\"' && quote != '\'') {
      continue;
    }

    if (json.substring(i + 1, i + 1 + keyLen) != key) {
      continue;
    }

    size_t keyEnd = i + 1 + keyLen;
    if (keyEnd >= json.length() || json[keyEnd] != quote) {
      continue;
    }

    // Find the colon after the key.
    size_t colonIndex = keyEnd + 1;
    while (colonIndex < json.length() && isspace(static_cast<unsigned char>(json[colonIndex]))) {
      colonIndex++;
    }
    if (colonIndex >= json.length() || json[colonIndex] != ':') {
      return "";
    }

    // Find the value start.
    size_t valueStart = colonIndex + 1;
    while (valueStart < json.length() && isspace(static_cast<unsigned char>(json[valueStart]))) {
      valueStart++;
    }
    if (valueStart >= json.length()) {
      return "";
    }

    char valueQuote = json[valueStart];
    if (valueQuote == '\"' || valueQuote == '\'') {
      // Quoted value.
      String value = "";
      for (size_t j = valueStart + 1; j < json.length(); ++j) {
        char c = json[j];
        if (c == '\\' && j + 1 < json.length()) {
          value += json[j + 1];
          j++;
          continue;
        }
        if (c == valueQuote) {
          return value;
        }
        value += c;
      }
      return "";
    }

    // Unquoted value: read until comma, brace, or whitespace.
    size_t valueEnd = valueStart;
    while (valueEnd < json.length()
           && json[valueEnd] != ','
           && json[valueEnd] != '}'
           && !isspace(static_cast<unsigned char>(json[valueEnd]))) {
      valueEnd++;
    }
    return json.substring(valueStart, valueEnd);
  }

  return "";
}

class LightBleServerCallbacks : public BLEServerCallbacks {
  void onConnect(BLEServer *server) override {
    Serial.println("BLE client connected.");
  }

  void onDisconnect(BLEServer *server) override {
    Serial.println("BLE client disconnected.");
    server->startAdvertising();
    Serial.println("BLE advertising restarted.");
  }
};

class LightBleSecurityCallbacks : public BLESecurityCallbacks {
  uint32_t onPassKeyRequest() override {
    return 0;
  }

  void onPassKeyNotify(uint32_t pass_key) override {
    Serial.print("BLE passkey notify: ");
    Serial.println(pass_key);
  }

  bool onConfirmPIN(const uint32_t pin) override {
    Serial.println("BLE confirm PIN accepted.");
    return true;
  }

  bool onSecurityRequest() override {
    Serial.println("BLE security request accepted.");
    return true;
  }

  void onAuthenticationComplete(esp_ble_auth_cmpl_t cmpl) override {
    if (cmpl.success) {
      Serial.println("BLE pairing/authentication successful.");
    } else {
      Serial.print("BLE pairing/authentication failed: ");
      Serial.println(cmpl.fail_reason);
    }
  }
};

class LightBleCharacteristicCallbacks : public BLECharacteristicCallbacks {
  void onWrite(BLECharacteristic *characteristic) override {
    String payload = String(characteristic->getValue().c_str());
    if (payload.length() == 0) {
      return;
    }

    String status = extractJsonStringValue(payload, "status");
    if (status.length() == 0) {
      Serial.print("BLE JSON missing status: ");
      Serial.println(payload);
      return;
    }

    if (!setStatus(status)) {
      Serial.print("Unknown BLE status: ");
      Serial.println(status);
      return;
    }

    Serial.print("BLE status changed to: ");
    Serial.println(status);
  }
};

void setupBle() {
  BLEDevice::init(BLE_DEVICE_NAME);

  // Enable Just Works pairing so Windows 10/11 can authenticate.
  BLEDevice::setEncryptionLevel(ESP_BLE_SEC_ENCRYPT);

  static LightBleSecurityCallbacks securityCallbacks;
  BLEDevice::setSecurityCallbacks(&securityCallbacks);

  {
    BLESecurity security;
    security.setAuthenticationMode(ESP_LE_AUTH_REQ_SC_ONLY);
    security.setCapability(ESP_IO_CAP_NONE);
    security.setInitEncryptionKey(ESP_BLE_ENC_KEY_MASK | ESP_BLE_ID_KEY_MASK);
    security.setRespEncryptionKey(ESP_BLE_ENC_KEY_MASK | ESP_BLE_ID_KEY_MASK);
  }

  bleServer = BLEDevice::createServer();

  static LightBleServerCallbacks serverCallbacks;
  bleServer->setCallbacks(&serverCallbacks);

  BLEService *service = bleServer->createService(BLE_SERVICE_UUID);
  BLECharacteristic *statusCharacteristic = service->createCharacteristic(
    BLE_CHARACTERISTIC_UUID,
    BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR);

  static LightBleCharacteristicCallbacks characteristicCallbacks;
  statusCharacteristic->setCallbacks(&characteristicCallbacks);
  service->start();

  BLEAdvertising *advertising = bleServer->getAdvertising();
  advertising->addServiceUUID(BLE_SERVICE_UUID);
  advertising->setMinPreferred(0x06);
  advertising->setMaxPreferred(0x12);
  advertising->start();

  Serial.print("BLE advertising as: ");
  Serial.println(BLE_DEVICE_NAME);
}

bool effectFrameDue(unsigned long intervalMs) {
  unsigned long now = millis();

  if (lastEffectFrameMs == 0 || now - lastEffectFrameMs >= intervalMs) {
    lastEffectFrameMs = now;
    return true;
  }

  return false;
}

void showThinkingChaseFrame() {
  switch (chaseIndex) {
    case 0:
      setLightLevels(LED_PWM_LIMIT, 0, 0);
      break;
    case 1:
      setLightLevels(LED_PWM_LIMIT, LED_PWM_LIMIT, 0);
      break;
    case 2:
      setLightLevels(0, LED_PWM_LIMIT, 0);
      break;
    case 3:
      setLightLevels(0, LED_PWM_LIMIT, LED_PWM_LIMIT);
      break;
    case 4:
      setLightLevels(0, 0, LED_PWM_LIMIT);
      break;
    default:
      setLightLevels(LED_PWM_LIMIT, 0, LED_PWM_LIMIT);
      break;
  }

  chaseIndex = (chaseIndex + 1) % 6;
}

void showAiChaseFrame() {
  switch (chaseIndex) {
    case 0:
      setLightLevels(LED_PWM_SOFT, LED_PWM_TRAIL, 0);
      break;
    case 1:
      setLightLevels(LED_PWM_TRAIL, LED_PWM_SOFT, 0);
      break;
    case 2:
      setLightLevels(0, LED_PWM_SOFT, LED_PWM_TRAIL);
      break;
    case 3:
      setLightLevels(0, LED_PWM_TRAIL, LED_PWM_SOFT);
      break;
    case 4:
      setLightLevels(LED_PWM_TRAIL, 0, LED_PWM_SOFT);
      break;
    default:
      setLightLevels(LED_PWM_SOFT, 0, LED_PWM_TRAIL);
      break;
  }

  chaseIndex = (chaseIndex + 1) % 6;
}

void updateIdleBreathing() {
  if (!effectFrameDue(IDLE_BREATH_STEP_MS)) {
    return;
  }

  breathBrightness += breathStep;
  if (breathBrightness >= LED_PWM_LIMIT) {
    breathBrightness = LED_PWM_LIMIT;
    breathStep = -2;
  } else if (breathBrightness <= LED_BREATH_MIN) {
    breathBrightness = LED_BREATH_MIN;
    breathStep = 2;
  }

  setLightLevels(0, 0, (uint8_t)breathBrightness);
}

void updateEffect() {
  switch (currentState) {
    case LightState::Idle:
      updateIdleBreathing();
      break;

    case LightState::Thinking:
      if (effectFrameDue(THINKING_CHASE_INTERVAL_MS)) {
        showThinkingChaseFrame();
      }
      break;

    case LightState::Ai:
      if (effectFrameDue(AI_CHASE_INTERVAL_MS)) {
        showAiChaseFrame();
      }
      break;

    case LightState::Busy:
      if (effectFrameDue(BUSY_BLINK_INTERVAL_MS)) {
        blinkOn = !blinkOn;
        setLightLevels(0, blinkOn ? LED_PWM_SOFT : 0, 0);
      }
      break;

    case LightState::Success:
      setLightLevels(0, 0, LED_PWM_LIMIT);
      if (millis() - stateStartMs >= SUCCESS_HOLD_MS) {
        enterState(LightState::Idle);
      }
      break;

    case LightState::WaitConfirm:
    case LightState::Confirm:
    case LightState::Waiting:
    case LightState::Wait:
      setLightLevels(0, LED_PWM_LIMIT, 0);
      break;

    case LightState::Error:
      if (effectFrameDue(ERROR_BLINK_INTERVAL_MS)) {
        blinkOn = !blinkOn;
        setLightLevels(blinkOn ? LED_PWM_LIMIT : 0, 0, 0);
      }
      break;

    case LightState::Off:
      setLightLevels(0, 0, 0);
      break;
  }
}

void handleCommand(const String &command) {
  String lowered = command;
  lowered.trim();
  lowered.toLowerCase();

  if (lowered.length() == 0) {
    return;
  }

  if (!setStatus(lowered)) {
    Serial.print("Unknown state: ");
    Serial.println(lowered);
    return;
  }

  Serial.print("State changed to: ");
  Serial.println(lowered);
}

void readSerialCommands() {
  while (Serial.available() > 0) {
    char c = Serial.read();

    if (c == '\n' || c == '\r') {
      if (serialBuffer.length() > 0) {
        handleCommand(serialBuffer);
        serialBuffer = "";
      }
    } else if (serialBuffer.length() < 31) {
      serialBuffer += c;
    } else {
      // Command too long; drop the rest and warn once per line.
      static bool overflowWarned = false;
      if (!overflowWarned) {
        Serial.println("Serial command too long; truncated.");
        overflowWarned = true;
      }
    }
  }
}

void setup() {
  Serial.begin(115200);
  serialBuffer.reserve(32);

  ledcSetup(RED_LED_CHANNEL, LED_PWM_FREQ, LED_PWM_RESOLUTION);
  ledcAttachPin(RED_LED_PIN, RED_LED_CHANNEL);
  ledcSetup(YELLOW_LED_CHANNEL, LED_PWM_FREQ, LED_PWM_RESOLUTION);
  ledcAttachPin(YELLOW_LED_PIN, YELLOW_LED_CHANNEL);
  ledcSetup(GREEN_LED_CHANNEL, LED_PWM_FREQ, LED_PWM_RESOLUTION);
  ledcAttachPin(GREEN_LED_PIN, GREEN_LED_CHANNEL);
  setLightLevels(0, 0, 0);
  setupBle();

  enterState(LightState::Off);

  Serial.println("ESP32-C3 traffic light ready.");
  Serial.println("Commands: idle, thinking, ai, success, busy, wait_confirm, confirm, waiting, wait, error, off");
}

void loop() {
  readSerialCommands();
  updateEffect();
}
