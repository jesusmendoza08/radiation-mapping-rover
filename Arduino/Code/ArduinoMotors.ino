#include <WiFi.h>

#define M1_RPWM 9
#define M1_LPWM 10
#define M2_RPWM 5
#define M2_LPWM 6

char ssid[] = "iPhone";
char pass[] = "Samiscool";

WiFiServer server(80);
WiFiClient client;

char cmd;
unsigned long lastStatusPrint = 0;
bool wasConnected = false;

// ================= MOTOR FUNCTIONS =================
void stopMotors() {
  digitalWrite(M1_RPWM, LOW);
  digitalWrite(M1_LPWM, LOW);
  digitalWrite(M2_RPWM, LOW);
  digitalWrite(M2_LPWM, LOW);
}

void forward() {
  digitalWrite(M1_RPWM, HIGH);
  digitalWrite(M1_LPWM, LOW);
  digitalWrite(M2_RPWM, LOW);
  digitalWrite(M2_LPWM, HIGH);
}

void backward() {
  digitalWrite(M1_RPWM, LOW);
  digitalWrite(M1_LPWM, HIGH);
  digitalWrite(M2_RPWM, HIGH);
  digitalWrite(M2_LPWM, LOW);
}

void leftTurn() {
  digitalWrite(M1_RPWM, LOW);
  digitalWrite(M1_LPWM, HIGH);
  digitalWrite(M2_RPWM, LOW);
  digitalWrite(M2_LPWM, HIGH);
}

void rightTurn() {
  digitalWrite(M1_RPWM, HIGH);
  digitalWrite(M1_LPWM, LOW);
  digitalWrite(M2_RPWM, HIGH);
  digitalWrite(M2_LPWM, LOW);
}

// ================= WIFI STATUS =================
void printWiFiStatus() {
  int status = WiFi.status();  // ✅ FIXED

  Serial.println("----- WIFI STATUS -----");

  if (status == WL_CONNECTED) {
    Serial.println("Connected!");
    Serial.print("SSID: ");
    Serial.println(WiFi.SSID());
    Serial.print("IP Address: ");
    Serial.println(WiFi.localIP());
    Serial.print("Signal: ");
    Serial.println(WiFi.RSSI());
  } else {
    Serial.print("Not connected. Status code: ");
    Serial.println(status);
  }

  Serial.println("-----------------------");
}

// ================= SETUP =================
void setup() {
  Serial.begin(115200);
  delay(5000);

  Serial.println();
  Serial.println("BOOTED");
  Serial.print("Trying WiFi: ");
  Serial.println(ssid);

  pinMode(M1_RPWM, OUTPUT);
  pinMode(M1_LPWM, OUTPUT);
  pinMode(M2_RPWM, OUTPUT);
  pinMode(M2_LPWM, OUTPUT);

  stopMotors();

  WiFi.begin(ssid, pass);
  server.begin();
}

// ================= LOOP =================
void loop() {

  // Print WiFi status every 2 seconds
  if (millis() - lastStatusPrint > 2000) {
    lastStatusPrint = millis();
    printWiFiStatus();
  }

  // Detect first connection
  if (WiFi.status() == WL_CONNECTED && !wasConnected) {
    wasConnected = true;
    Serial.println("WiFi just connected!");
    Serial.print("IP Address: ");
    Serial.println(WiFi.localIP());
  }

  if (WiFi.status() != WL_CONNECTED) {
    wasConnected = false;
  }

  // Handle client
  client = server.available();

  if (client) {
    Serial.println("Client connected");

    while (client.connected()) {
      if (client.available()) {
        cmd = client.read();
        Serial.print("Received: ");
        Serial.println(cmd);

        if (cmd == 'w') forward();
        else if (cmd == 's') backward();
        else if (cmd == 'a') leftTurn();
        else if (cmd == 'd') rightTurn();
        else if (cmd == 'x' || cmd == ' ') stopMotors();
      }
    }

    client.stop();
    stopMotors();
    Serial.println("Client disconnected");
  }
}