/* такой джойстик — это по сути два потенциометра по оси X-Y + кнопка
СМОТРИТЕ ВНИМАТЕЛЬНО на реальную маркировку пинов у реального джойстика
(VX = HORZ / VY = VERT)
они могут отличаться по порядку и расположению от картинки тут
*/


const uint8_t JOY_X_PIN = A6;
const uint8_t JOY_Y_PIN = A5;
const uint8_t JOY_SW_PIN = 2;

const unsigned long JOY_INTERVAL_MS = 30;   // axis update rate (~33 Hz)
const int JOY_THRESHOLD = 8;                // deadzone against jitter/drift

int joyLastX = -1000, joyLastY = -1000;
unsigned long joyLastEmit = 0;

const unsigned long JOY_DEBOUNCE_MS = 25;
int joySwLastReading = HIGH;
int joySwStable = HIGH;
unsigned long joySwLastChange = 0;
unsigned long joySwPressStart = 0;


const uint8_t BTN_PIN = 6; // пин D6 (можно любой D пин)

// кнопка физически при нажатии дребезжит 
// и замыкание контакта происходит несколько раз
// чтобы избежать двойных или тройных нажатий за раз
// делают дебаунс — нажали и после первого контакта 
// делаем таймаут на 25 милисек

const unsigned long DEBOUNCE_MS = 25;
const unsigned long HELD_REPEAT_MS = 200;

int lastReading = HIGH;
int stableState = HIGH;
// для фиксирования времени удержания (HELD)
unsigned long lastChangeTime = 0;
unsigned long pressStart = 0;
unsigned long lastHeldEmit = 0;

void setup() {
    Serial.begin(115200);
    pinMode(JOY_SW_PIN, INPUT_PULLUP);
    pinMode(BTN_PIN, INPUT_PULLUP);
}

void read_simple_button()
{
  int reading = digitalRead(BTN_PIN);
  if (reading != lastReading) {
    lastChangeTime = millis();
    lastReading = reading;
  }

  if ((millis() - lastChangeTime) > DEBOUNCE_MS) {
    if (reading != stableState) {
      stableState = reading;
      if (stableState == LOW) {
        pressStart = millis();
        lastHeldEmit = pressStart;
        Serial.println("BTN,DOWN");
      } else {
        Serial.print("BTN,UP,");
        Serial.println(millis() - pressStart);
      }
    }
  }

  if (stableState == LOW) {
    if (millis() - lastHeldEmit >= HELD_REPEAT_MS) {
      lastHeldEmit = millis();
      Serial.print("BTN,HELD,");
      Serial.println(millis() - pressStart);
    }
  }
}


void read_joystick()
{
  // ---- axes: throttle + threshold ----
  if (millis() - joyLastEmit >= JOY_INTERVAL_MS) {
    int x = analogRead(JOY_X_PIN);
    int y = analogRead(JOY_Y_PIN);

    // emit if either axis moved beyond threshold
    if (abs(x - joyLastX) >= JOY_THRESHOLD ||
        abs(y - joyLastY) >= JOY_THRESHOLD) {
      joyLastX = x;
      joyLastY = y;
      joyLastEmit = millis();
      Serial.print("JOY,");
      Serial.print(x);
      Serial.print(",");
      Serial.println(y);
    }
  }

  // ---- button: debounced events ----
  int reading = digitalRead(JOY_SW_PIN);
  if (reading != joySwLastReading) {
    joySwLastChange = millis();
    joySwLastReading = reading;
  }

  if ((millis() - joySwLastChange) > JOY_DEBOUNCE_MS) {
    if (reading != joySwStable) {
      joySwStable = reading;
      if (joySwStable == LOW) {
        joySwPressStart = millis();
        Serial.println("JOY,DOWN");
      } else {
        Serial.print("JOY,UP,");
        Serial.println(millis() - joySwPressStart);
      }
    }
  }
}

void loop() {
  read_joystick();
  read_simple_button();
}
