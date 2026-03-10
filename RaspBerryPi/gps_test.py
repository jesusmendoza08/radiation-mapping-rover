import serial

ser = serial.Serial("/dev/ttyACMO0", 9600, timeout = 1)

while True:
        line = ser.readline().decode("utf-8","ignore")
        print(line)