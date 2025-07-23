import serial
ser1 = serial.Serial("COM5", 115200)
   
# Send character 'S' to start the program
# ser.write(bytearray('S','ascii'))
ser1.write(b'fbbffgggnnggg')

# Read line   
# while True:
# bs = ser.readline()
# print(bs)