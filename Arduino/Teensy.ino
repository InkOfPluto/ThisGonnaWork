#define PWMA 11 // 食指Index（Motor A）PWM 引脚 i 开头的数字
#define AIN1 10
#define AIN2 9

#define PWMB 6  // 拇指Thumb（Motor B）PWM 引脚 t开头的数字
#define BIN1 8
#define BIN2 7

int pwmA = 255;          // 食指速度
int pwmB = 255;          // 拇指速度
int spinDuration = 1000; // 动作持续时间（毫秒）

void setup() {
  Serial.begin(115200);
  
  pinMode(PWMA, OUTPUT);
  pinMode(AIN1, OUTPUT);
  pinMode(AIN2, OUTPUT);

  pinMode(PWMB, OUTPUT);
  pinMode(BIN1, OUTPUT);
  pinMode(BIN2, OUTPUT);

  analogWrite(PWMA, 0);
  analogWrite(PWMB, 0);

  Serial.println("Index (Motor A) & Thumb (Motor B) Finger Motor Control Ready");
  Serial.println("Use commands:");
  Serial.println("  f / b → Index (Motor A) forward/backward");
  Serial.println("  g / n → Thumb (Motor B) forward/backward");
  Serial.println("  i### → Set speed for Motor A (Index finger)");
  Serial.println("  t### → Set speed for Motor B (Thumb)");
  Serial.println("  ###  → Set spin duration (e.g., '500' = 500ms)");
  Serial.println("  e.g., 'f:500' → Motor A forward after 500ms delay");
}

void loop() {
  if (Serial.available()) {
    String input = Serial.readStringUntil('\n');
    input.trim();

    if (input.length() == 0) return;

    // === 处理设置速度指令 ===
    if (input.startsWith("i")) {
      int val = input.substring(1).toInt();
      if (val >= 0 && val <= 255) {
        pwmA = val;
        Serial.print("Set Index finger (Motor A) speed to ");
        Serial.println(pwmA);
      } else {
        Serial.println("Invalid Motor A speed (0–255)");
      }
      return;
    }

    if (input.startsWith("t")) {
      int val = input.substring(1).toInt();
      if (val >= 0 && val <= 255) {
        pwmB = val;
        Serial.print("Set Thumb (Motor B) speed to ");
        Serial.println(pwmB);
      } else {
        Serial.println("Invalid Motor B speed (0–255)");
      }
      return;
    }

    // === 如果输入的是纯数字，则设置 spinDuration ===
    bool isNumber = true;
    for (unsigned int i = 0; i < input.length(); i++) {
      if (!isDigit(input.charAt(i))) {
        isNumber = false;
        break;
      }
    }

    if (isNumber) {
      int duration = input.toInt();
      if (duration > 0 && duration <= 5000) {
        spinDuration = duration;
        Serial.print("Set spin duration to ");
        Serial.print(spinDuration);
        Serial.println(" ms");
      } else {
        Serial.println("Invalid duration (1–5000 ms expected)");
      }
      return;
    }

    // === 动作指令解析 ===
    char cmd = input.charAt(0);
    int delayTime = 0;

    int colonIndex = input.indexOf(':');
    if (colonIndex != -1) {
      delayTime = input.substring(colonIndex + 1).toInt();
    }

    if (delayTime > 0) delay(delayTime);

    // === Motor A - Index finger ===
    if (cmd == 'f') {
      digitalWrite(AIN1, HIGH);
      digitalWrite(AIN2, LOW);
      analogWrite(PWMA, pwmA);
      delay(spinDuration);
      analogWrite(PWMA, 0);
      Serial.println("Index finger (Motor A): FORWARD");
    }
    else if (cmd == 'b') {
      digitalWrite(AIN1, LOW);
      digitalWrite(AIN2, HIGH);
      analogWrite(PWMA, pwmA);
      delay(spinDuration);
      analogWrite(PWMA, 0);
      Serial.println("Index finger (Motor A): BACKWARD");
    }

    // === Motor B - Thumb ===
    else if (cmd == 'g') {
      digitalWrite(BIN1, HIGH);
      digitalWrite(BIN2, LOW);
      analogWrite(PWMB, pwmB);
      delay(spinDuration);
      analogWrite(PWMB, 0);
      Serial.println("Thumb (Motor B): FORWARD");
    }
    else if (cmd == 'n') {
      digitalWrite(BIN1, LOW);
      digitalWrite(BIN2, HIGH);
      analogWrite(PWMB, pwmB);
      delay(spinDuration);
      analogWrite(PWMB, 0);
      Serial.println("Thumb (Motor B): BACKWARD");
    }
    else {
      Serial.println("Invalid command.");
    }
  }
}
