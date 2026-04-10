// Geiger click detector via ANALOG input
// Tap point: A42 transistor control node ("Pin 2") -> 10k series resistor -> A0
// Geiger GND (H1 hole 4) -> Arduino GND

const int SIG_PIN = A0;

// Signal thresholds
const float THRESHOLD_HIGH = 1.10f;
const float THRESHOLD_LOW  = 0.60f;

// One count per beep burst
const unsigned long DEBOUNCE_MS = 1200;

// Print every 5 seconds
const unsigned long REPORT_MS = 5000;

// ---- Tube conversion factor ----
// Common rough starting point for J305 / SBM-20 style tubes:
const float CPM_PER_USVH = 175.0f;

uint32_t totalClicks = 0;
bool armed = true;
unsigned long lastClickMs = 0;

static inline float readVoltage() {
  int raw = analogRead(SIG_PIN);
  return (raw * 3.3f) / 4095.0f;
}

void setup() {
  Serial.begin(115200);
  analogReadResolution(12);
}

void loop() {
  float v = readVoltage();
  unsigned long now = millis();

  // Count only the start of each click burst
  if (armed && v > THRESHOLD_HIGH && (now - lastClickMs > DEBOUNCE_MS)) {
    totalClicks++;
    lastClickMs = now;
    armed = false;
  }

  // Re-arm after signal falls back down
  if (!armed && v < THRESHOLD_LOW) {
    armed = true;
  }

  static unsigned long lastReport = 0;
  static uint32_t lastTotal = 0;

  if (now - lastReport >= REPORT_MS) {
    uint32_t diff = totalClicks - lastTotal;
    lastTotal = totalClicks;
    lastReport = now;

    // clicks in 5s -> CPM
    float cpm = diff * 12.0f;

    // CPM -> uSv/h
    float usvh = cpm / CPM_PER_USVH;

    Serial.print("V=");
    Serial.print(v, 3);
    Serial.print("  clicks/5s=");
    Serial.print(diff);
    Serial.print("  CPM=");
    Serial.print(cpm, 1);
    Serial.print("  uSv/h=");
    Serial.print(usvh, 3);
    Serial.print("  total=");
    Serial.println(totalClicks);
  }
}