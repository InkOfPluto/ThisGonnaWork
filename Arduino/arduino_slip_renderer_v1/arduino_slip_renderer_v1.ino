#define PWMA 11 // PWM pin for motor
#define AIN1 9 // direction pin 1
#define AIN2 10 // direction pin 2

#define PWMB 6 // PWM pin for motor
#define BIN1 8 // direction pin 1
#define BIN2 7 // direction pin 2

int pwmsig   = 100;  // PWM duty cycle (out of 255)
float motorCurr = 0; 
float voltage   = 0; 

void setup() {
  Serial.begin(115200);
  
  // set motor pins to output
  pinMode(PWMA, OUTPUT);
  pinMode(AIN1, OUTPUT);
  pinMode(AIN2, OUTPUT);
  pinMode(PWMB, OUTPUT);
  pinMode(BIN1, OUTPUT);
  pinMode(BIN2, OUTPUT);

  // Ensure motor is off at startup
  digitalWrite(AIN1, LOW);
  digitalWrite(AIN2, LOW);
  analogWrite(PWMA, 0);
  digitalWrite(BIN1, LOW);
  digitalWrite(BIN2, LOW);
  analogWrite(PWMB, 0);
}

void loop() {
  // If there is serial data, read one character
  if (Serial.available() > 0) {
    char command = Serial.read();
    
    if (command == 'f') {
      // Spin forward for 500 ms
      digitalWrite(AIN1, HIGH);
      digitalWrite(AIN2, LOW);
      analogWrite(PWMA, pwmsig);
      delay(50);
      analogWrite(PWMA, 0);
    }
    else if (command == 'b') {
      // Spin backward for 500 ms
      digitalWrite(AIN1, LOW);
      digitalWrite(AIN2, HIGH);
      analogWrite(PWMA, pwmsig);
      delay(50);
      analogWrite(PWMA, 0);
    }
    else if (command == 'g') {
      // Spin backward for 500 ms
      digitalWrite(BIN1, HIGH);
      digitalWrite(BIN2, LOW);
      analogWrite(PWMB, pwmsig);
      delay(50);
      analogWrite(PWMB, 0);
    }
    else if (command == 'n') {
      // Spin backward for 500 ms
      digitalWrite(BIN1, LOW);
      digitalWrite(BIN2, HIGH);
      analogWrite(PWMB, pwmsig);
      delay(50);
      analogWrite(PWMB, 0);
    }
  }

  // Measure and print motor current (via analogRead on A0)
  motorCurr = analogRead(A0);
  voltage   = motorCurr * (5.0 / 1024.0);
  Serial.println(voltage);
}
