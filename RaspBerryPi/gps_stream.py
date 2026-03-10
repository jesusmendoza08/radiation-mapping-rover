import serial
import pynmea2
import socket

GPS_PORT = "/dev/ttyACM0"
GPS_BAUD = 9600

HOST = "0.0.0.0"
PORT = 5050

gps = serial.Serial(GPS_PORT, GPS_BAUD, timeout=1)

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind((HOST, PORT))
server.listen(1)

print("Waiting for rover control app...")

conn, addr = server.accept()
print("Connected:", addr)

while True:
    line = gps.readline().decode("utf-8", errors="ignore")

    if line.startswith("$GPGGA"):
        try:
            msg = pynmea2.parse(line)

            lat = msg.latitude
            lon = msg.longitude

            data = f"{lat},{lon}\n"

            conn.send(data.encode())

        except:
            pass