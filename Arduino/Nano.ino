#define PWMA 3   // 中指Middle (Motor A)PWM 引脚 m 开头的数字
#define AIN1 4
#define AIN2 5

#define PWMB 9   // 无名指Ring (Motor B)PWM 引脚 r 开头的数字
#define BIN1 10
#define BIN2 11

int pwmA = 255;            // 中指电机默认速度
int pwmB = 255;            // 无名指电机默认速度
int spinDuration = 1000;   // 默认旋转持续时间（毫秒）

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

  Serial.println("Thumb & Ring Finger Motor Control Ready");
  Serial.println("Use commands:");
  Serial.println("  f / b → Thumb (Motor A) forward/backward");
  Serial.println("  g / n → Ring finger (Motor B) forward/backward");
  Serial.println("  m### → Set speed for Thumb (Motor A)");
  Serial.println("  r### → Set speed for Ring (Motor B)");
  Serial.println("  ###  → Set spin duration (e.g., '500' = 500ms)");
  Serial.println("  e.g., 'f:500' means Motor A forward after 500ms delay");
}

void loop() {
  if (Serial.available()) {
    String input = Serial.readStringUntil('\n');
    input.trim(); // 清除空格与换行

    if (input.length() == 0) return;

    // === 设置单个电机速度 ===
    if (input.startsWith("m")) {
      int val = input.substring(1).toInt();
      if (val >= 0 && val <= 255) {
        pwmA = val;
        Serial.print("Set Middle finger (Motor A) speed to ");
        Serial.println(pwmA);
      } else {
        Serial.println("Invalid Motor A speed (0–255)");
      }
      return;
    }

    if (input.startsWith("r")) {
      int val = input.substring(1).toInt();
      if (val >= 0 && val <= 255) {
        pwmB = val;
        Serial.print("Set Ring finger (Motor B) speed to ");
        Serial.println(pwmB);
      } else {
        Serial.println("Invalid Motor B speed (0–255)");
      }
      return;
    }

    // === 设置旋转持续时间（输入纯数字） ===
    bool isNumber = true;
    for (unsigned int i = 0; i < input.length(); i++) {
      if (!isDigit(input.charAt(i))) {
        isNumber = false;
        break;
      }
    }

    if (isNumber) {
      int newDuration = input.toInt();
      if (newDuration > 0 && newDuration <= 5000) {
        spinDuration = newDuration;
        Serial.print("Updated spin duration to: ");
        Serial.print(spinDuration);
        Serial.println(" ms");
      } else {
        Serial.println("Invalid duration. Enter 1–5000 milliseconds.");
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

    // === Motor A（中指）===
    if (cmd == 'f') {
      digitalWrite(AIN1, HIGH);
      digitalWrite(AIN2, LOW);
      analogWrite(PWMA, pwmA);
      delay(spinDuration);
      analogWrite(PWMA, 0);
      Serial.println("Thumb (Motor A): FORWARD");
    }
    else if (cmd == 'b') {
      digitalWrite(AIN1, LOW);
      digitalWrite(AIN2, HIGH);
      analogWrite(PWMA, pwmA);
      delay(spinDuration);
      analogWrite(PWMA, 0);
      Serial.println("Thumb (Motor A): BACKWARD");
    }

    // === Motor B（无名指）===
    else if (cmd == 'g') {
      digitalWrite(BIN1, HIGH);
      digitalWrite(BIN2, LOW);
      analogWrite(PWMB, pwmB);
      delay(spinDuration);
      analogWrite(PWMB, 0);
      Serial.println("Ring finger (Motor B): FORWARD");
    }
    else if (cmd == 'n') {
      digitalWrite(BIN1, LOW);
      digitalWrite(BIN2, HIGH);
      analogWrite(PWMB, pwmB);
      delay(spinDuration);
      analogWrite(PWMB, 0);
      Serial.println("Ring finger (Motor B): BACKWARD");
    }

    else {
      Serial.println("Invalid command.");
    }
  }
}
