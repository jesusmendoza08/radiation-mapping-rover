// Pin Configuration
#define GEIGER_PIN 2 
#define LED_PIN 13

// Motor Pins
#define M1_RPWM 9
#define M1_LPWM 10
#define M2_RPWM 5
#define M2_LPWM 6

// Geiger Counter Variables
volatile unsigned long totalClicks = 0;
volatile unsigned long lastClickMicros = 0;

// Sliding Window for Real-Time CPM
const int MAX_CLICKS = 200; 
unsigned long clickTimes[MAX_CLICKS];
int clickIndex = 0;

// Motor Functions
void stopMotors() 
{
  digitalWrite(M1_RPWM, LOW); 
  digitalWrite(M1_LPWM, LOW);

  digitalWrite(M2_RPWM, LOW); 
  digitalWrite(M2_LPWM, LOW);
}

void backward() {
  digitalWrite(M1_RPWM, HIGH);
  digitalWrite(M1_LPWM, LOW);

  digitalWrite(M2_RPWM, LOW);  
  digitalWrite(M2_LPWM, HIGH);
}

void forward() {
  digitalWrite(M1_RPWM, LOW);  
  digitalWrite(M1_LPWM, HIGH);
  
  digitalWrite(M2_RPWM, HIGH); 
  digitalWrite(M2_LPWM, LOW);
}

void rightTurn() {
  digitalWrite(M1_RPWM, LOW);  
  digitalWrite(M1_LPWM, HIGH);

  digitalWrite(M2_RPWM, LOW);  
  digitalWrite(M2_LPWM, HIGH);
}

void leftTurn() {
  digitalWrite(M1_RPWM, HIGH); 
  digitalWrite(M1_LPWM, LOW);

  digitalWrite(M2_RPWM, HIGH); 
  digitalWrite(M2_LPWM, LOW);
}

// Geiger Interrupt
void countPulse() {
  unsigned long currentMicros = micros();
  
  // 80us Noise Filter
  delayMicroseconds(80); 
  if (digitalRead(GEIGER_PIN) == HIGH) {
    
    // 15ms Lockout
    if (currentMicros - lastClickMicros > 15000) { 
      totalClicks++;
      
      // Track time for real-time calculation
      clickTimes[clickIndex] = millis();
      clickIndex = (clickIndex + 1) % MAX_CLICKS;
      
      lastClickMicros = currentMicros;
    }
  }
}

//Setup
void setup() {
//Beginning Communication with RaspberryPi
  Serial.begin(115200);

  // Initialize Motors
  pinMode(M1_RPWM, OUTPUT);
  pinMode(M1_LPWM, OUTPUT);
  pinMode(M2_RPWM, OUTPUT);
  pinMode(M2_LPWM, OUTPUT);
  stopMotors();

  // Initialize Geiger
  pinMode(GEIGER_PIN, INPUT_PULLDOWN); 
  pinMode(LED_PIN, OUTPUT);
  attachInterrupt(digitalPinToInterrupt(GEIGER_PIN), countPulse, RISING);

}

// Main Loop

void loop() {

  // If communication to Raspberry Pi is successful
  if (Serial.available() > 0) {
    char cmd = Serial.read();

    //Check for Input from Driver
    if (cmd == 'w') forward();
    else if (cmd == 's') backward();
    else if (cmd == 'a') leftTurn();
    else if (cmd == 'd') rightTurn();
    else if (cmd == 'x') stopMotors();
  }

  // Report Radiation every 2 seconds
  static unsigned long reportTimer = 0;
  if (millis() - reportTimer > 2000) {
    unsigned long now = millis();
    int windowClicks = 0;
    
    // Calculate CPM from the last 30 seconds
    for (int i = 0; i < MAX_CLICKS; i++) {
      if (now - clickTimes[i] < 30000 && clickTimes[i] > 0) {
        windowClicks++;
      }
    }

    float realTimeCPM = windowClicks * 2.0;
    float uSvh = realTimeCPM / 153.8;

    // Send formatted data for the Pi/Dashboard to parse
   Serial.print("RAD_DATA|Total:");
  Serial.print(totalClicks);
  Serial.print("|CPM:");
  Serial.print(realTimeCPM);
  Serial.print("|uSv:");
  Serial.println(uSvh, 4);

    
    reportTimer = now;
  }
}