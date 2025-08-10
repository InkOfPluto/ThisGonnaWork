// ====== Motor Pin Mapping (ESP32) ======

// ================== 大拇指Thumb-Motor-(2-A) ==================
#define PWMC 22
#define CIN1 16
#define CIN2 27
// ================== 食指Index-Motor-(2-B) ==================
#define PWMD 25
#define DIN1 21
#define DIN2 17
// ================== 中指Middle-Motor-(3-B) ==================
#define PWME 4
#define EIN1 32
#define EIN2 33
// ================== 无名指Ring-Motor-(1-A) ==================
#define PWMA 26
#define AIN1 14
#define AIN2 23
// ================== 小拇指Pinky-Motor-(1-B) ==================
#define PWMB 5
#define BIN1 18
#define BIN2 19

// 当前速度（带符号）：-255..255，0=停止
int spdR = 0, spdP = 0, spdT = 0, spdI = 0, spdM = 0;

void setup() {
  Serial.begin(115200);

  // 初始化所有引脚
  pinMode(PWMA, OUTPUT); pinMode(AIN1, OUTPUT); pinMode(AIN2, OUTPUT);
  pinMode(PWMB, OUTPUT); pinMode(BIN1, OUTPUT); pinMode(BIN2, OUTPUT);
  pinMode(PWMC, OUTPUT); pinMode(CIN1, OUTPUT); pinMode(CIN2, OUTPUT);
  pinMode(PWMD, OUTPUT); pinMode(DIN1, OUTPUT); pinMode(DIN2, OUTPUT);
  pinMode(PWME, OUTPUT); pinMode(EIN1, OUTPUT); pinMode(EIN2, OUTPUT);

  stopAll();

  Serial.println("⚙️ Five-Finger Motor Controller (Persistent Speed, No Duration)");
  Serial.println("Commands (comma-separated):");
  Serial.println("  r###  -> Motor R (Ring)   speed -255..255 (sign = direction)");
  Serial.println("  p###  -> Motor P (Pinky)  speed -255..255");
  Serial.println("  t###  -> Motor T (Thumb)  speed -255..255");
  Serial.println("  i###  -> Motor I (Index)  speed -255..255");
  Serial.println("  m###  -> Motor M (Middle) speed -255..255");
  Serial.println("  ?     -> print current speeds");
  Serial.println("  s     -> STOP ALL motors immediately");
  Serial.println("Examples:");
  Serial.println("  t255,i150,m100,r-150,p-230");
  Serial.println("  t0,i0,m0,r0,p0");
  Serial.println("  i-255");
}

void loop() {
  if (!Serial.available()) return;

  String line = Serial.readStringUntil('\n');
  line.trim();
  if (line.length() == 0) return;

  // 新增：一键急停
  if (line == "s") {
    stopAll();
    Serial.println("🛑 ALL motors STOPPED.");
    return;
  }

  if (line == "?") {
    printStatus();
    return;
  }

  // 逐个 token 解析
  line += ",";
  String token = "";
  for (int i = 0; i < line.length(); i++) {
    char ch = line.charAt(i);
    if (ch == ',') {
      token.trim();
      token.toLowerCase();

      if (token.length() >= 2) {
        char id = token.charAt(0);
        String numStr = token.substring(1);
        int val = numStr.toInt();
        if (val < -255) val = -255;
        if (val > 255)  val = 255;

        switch (id) {
          case 'r': spdR = val; applyMotor(AIN1, AIN2, PWMA, spdR, "R (Ring)");   break;
          case 'p': spdP = val; applyMotor(BIN1, BIN2, PWMB, spdP, "P (Pinky)");  break;
          case 't': spdT = val; applyMotor(CIN1, CIN2, PWMC, spdT, "T (Thumb)");  break;
          case 'i': spdI = val; applyMotor(DIN1, DIN2, PWMD, spdI, "I (Index)");  break;
          case 'm': spdM = val; applyMotor(EIN1, EIN2, PWME, spdM, "M (Middle)"); break;
          default:
            if (token.length() > 0) {
              Serial.print("⚠️ Unknown token: "); Serial.println(token);
            }
            break;
        }
      } else if (token.length() == 1 && token[0] != '?' && token[0] != 's') {
        Serial.print("⚠️ Incomplete token: "); Serial.println(token);
      }

      token = "";
    } else {
      token += ch;
    }
  }
}

void applyMotor(int in1, int in2, int pwm, int signedSpeed, const char* name) {
  int mag = abs(signedSpeed);

  if (signedSpeed == 0) {
    digitalWrite(in1, LOW);
    digitalWrite(in2, LOW);
    analogWrite(pwm, 0);
    Serial.print("🛑 Motor "); Serial.print(name); Serial.println(" STOP");
    return;
  }

  if (signedSpeed > 0) {
    digitalWrite(in1, HIGH);
    digitalWrite(in2, LOW);
    Serial.print("➡️  Motor "); Serial.print(name); Serial.print(" FORWARD @ ");
  } else {
    digitalWrite(in1, LOW);
    digitalWrite(in2, HIGH);
    Serial.print("⬅️  Motor "); Serial.print(name); Serial.print(" BACKWARD @ ");
  }

  analogWrite(pwm, mag);
  Serial.print(mag); Serial.println(" PWM");
}

void stopAll() {
  // 同步清零状态变量
  spdR = spdP = spdT = spdI = spdM = 0;

  applyMotor(AIN1, AIN2, PWMA, 0, "R (Ring)");
  applyMotor(BIN1, BIN2, PWMB, 0, "P (Pinky)");
  applyMotor(CIN1, CIN2, PWMC, 0, "T (Thumb)");
  applyMotor(DIN1, DIN2, PWMD, 0, "I (Index)");
  applyMotor(EIN1, EIN2, PWME, 0, "M (Middle)");
}

void printStatus() {
  Serial.print("Status | T:"); Serial.print(spdT);
  Serial.print("  I:");       Serial.print(spdI);
  Serial.print("  M:");       Serial.print(spdM);
  Serial.print("  R:");       Serial.print(spdR);
  Serial.print("  P:");       Serial.println(spdP);
}
