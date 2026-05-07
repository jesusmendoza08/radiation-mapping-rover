import cv2
from flask import Flask, Response
import threading
import socket
import serial
import time
import json

# Configuring Ports
CAMERA_PORT = 5000
TELEMETRY_PORT = 5060 
MOTOR_PORT = 5070

# Configuring USB Device path
ARDUINO_SERIAL_PORT = '/dev/ttyACM0' 
GPS_SERIAL_PORT = '/dev/ttyACM1'

#Setting Baud Rates for serial communication
BAUD_ARDUINO = 115200
BAUD_GPS = 9600

# Global State
latest_gps = {"lat": 0.0, "lon": 0.0, "alt": 0.0}

# New Geiger Counter state
latest_rad = {"total": 0, "cpm": 0.0,"usv":0.0} 
motor_command_queue = []

# Initialize Arduino Connection
try:
    arduino_ser = serial.Serial(ARDUINO_SERIAL_PORT, BAUD_ARDUINO, timeout=0.1)
    print(f"CONNECTED: Arduino on {ARDUINO_SERIAL_PORT}")
except Exception as e:
    arduino_ser = None
    print(f"ERROR: Arduino not found: {e}")

# ~~~~~~~~~~~ Arduino Handler - Handling the Geiger Counter and Motors ~~~~~~~~~~~~~~
def arduino_handler():

    global latest_rad
    print("THREAD STARTED: Arduino Serial Handler")
    while True:
        if arduino_ser and arduino_ser.is_open:
            try:
                # Push commands from WPF to the Arduino
                while motor_command_queue:
                    cmd = motor_command_queue.pop(0)
                    arduino_ser.write(cmd.encode())
                
                # Parse received 'RAD_DATA' and log confirmations
                if arduino_ser.in_waiting > 0:
                    line = arduino_ser.readline().decode('utf-8', errors='ignore').strip()
                    
                    if line.startswith("RAD_DATA|"):
                        # Expected format: RAD_DATA|Total:###|CPM:##.#
                        parts = line.split('|')
                        total_clicks = int(parts[1].split(':')[1])
                        cpm_val = float(parts[2].split(':')[1])
                        
                        # Update global state for telemetry
                        latest_rad["total"] = total_clicks
                        latest_rad["cpm"] = cpm_val
                        
                        if len(parts) > 3 and parts[3].startswith("uSv:"): latest_rad["usv"] = float(parts[3].split(":")[1])
                    elif line:
                        # Print motor confirmations or other logs
                        print(f"[ARDUINO]: {line}")
                        
            except Exception as e:
                print(f"Serial Communication Error (Arduino): {e}")
        
        time.sleep(0.01) # 100Hz frequency for zero-lag steering

# ~~~~~~~~~~~~~~~~~~ Motor Socket Server ~~~~~~~~~~~~~~
def motor_server():
    #Listens for TCP packets from WPF 
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind(('0.0.0.0', MOTOR_PORT))
    server.listen(5)
    
    print(f"MOTOR SERVER: Listening on port {MOTOR_PORT}")
    
    #Maps them to Arduino Command Array
    cmd_map = {"FORWARD":"w", "BACKWARD":"s", "LEFT":"a", "RIGHT":"d", "STOP":"x"}
    
    while True:
        try:
            #Connects to the Server
            conn, addr = server.accept()
            print(f"WPF CONTROLLER CONNECTED: {addr}")
            while True:
                #Decode Data
                data = conn.recv(1024).decode('utf-8').strip()
                if not data: break
                
                #Split data to process each command one by one
                for line in data.split('\n'):
                    #Cleans the command from whitespace and makes it Uppercase
                    clean_cmd = line.strip().upper()
                    #Checking if the command is within the Array of set Commands.
                    if clean_cmd in cmd_map:
                        motor_command_queue.append(cmd_map[clean_cmd])
        except Exception as e:
            print(f"Connection error: {e}")

#~~~~~~~~~ GPS Logic ~~~~~~~~~~~~~~

#Parsing NMEA Values to Decimal
def nmea_to_dec(value, direction):
    if not value or not direction: return 0.0
    try:
        dot = value.find('.')
        dd = float(value[:dot-2])
        mm = float(value[dot-2:])
        dec = dd + (mm/60)
        return round(-dec if direction in ['S', 'W'] else dec, 6)
    except: return 0.0

def gps_reader():
    #Updates GPS Data in the Background
    #Creating Global GPS Data Variable
    global latest_gps
    try:
        #Open Serial Communication through Designated Ports
        gps_ser = serial.Serial(GPS_SERIAL_PORT, BAUD_GPS, timeout=1)
        print(f" GPS CONNECTED: {GPS_SERIAL_PORT}")

        while True:
            #Read data and convert it into text, stopping at a Newline character
            line = gps_ser.readline().decode('utf-8', errors='ignore').strip()
            #Look for the GPGGA Characters in the NMEA Data
            if line.startswith("$GPGGA"):
                #Create a list split by ','
                p = line.split(',')
                if len(p) > 6 and p[6] != '0':
                    #Converting Received NMEA Data into Decimal Degrees
                    latest_gps["lat"] = nmea_to_dec(p[2], p[3])
                    latest_gps["lon"] = nmea_to_dec(p[4], p[5])
                    latest_gps["alt"] = float(p[9]) if p[9] else 0.0
    except Exception as e:
        print(f"GPS Reader Error: {e}")

# ~~~~~ Telemetry Server. Sends GPS and Radiation Data every second to the WPF App ~~~~~~
def telemetry_server():

    #Creating a new Socket using IPv4 and TCP
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind(('0.0.0.0', TELEMETRY_PORT))
    server.listen(5)
    
    print(f"TELEMETRY SERVER: Listening on port {TELEMETRY_PORT}")
    
    while True:
        conn, _ = server.accept()

        def broadcaster(c):
            try:
                while True:
                    # Combined payload of JSON Data for the WPF App
                    payload = json.dumps({
                        "gps": latest_gps,
                        "rad": latest_rad
                    }) + "\n"
                    
                    c.sendall(payload.encode())
                    time.sleep(1) # Send update once per second
            except:
                c.close()
        #Starting a new Thread for each client. Daemon=True means the threads will shut down as soon as the main program is closed.
        threading.Thread(target=broadcaster, args=(conn,), daemon=True).start()

# ~~~~~~~~~~~~~~~ CAMERA STREAM ~~~~~~~~~~~~~~~~

#Initializing Camera Application
app = Flask(__name__)

def generate_frames():
    #Accessing Hardware Camera
    cap = cv2.VideoCapture(0)

    #Setting Capture Resolution.
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    
    while True:
        #Checking if the Camera is connected and reading the Frame
        success, frame = cap.read()
        if not success: break

        #Converting RAW Data into a JPEG Image. Quality at 70 for reliable streaming over 2.4GHz Wi-Fi
        ret, buffer = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 70])

        #Generator that wraps the JPEG Bytes in a boundary telling the browser to keep connection open for receiving more information.
        yield (b'--frame\r\nContent-Type: image/jpeg\r\n\r\n' + buffer.tobytes() + b' \r\n')

#Defining the URL/Route where the video will go
@app.route('/video_feed')
def video_feed():

    #Returning the frames to the browser. Mimetype tells the browser to replace the current frame with the next one, creating a Live Feed.
    return Response(generate_frames(), mimetype='multipart/x-mixed-replace; boundary=frame')

# ~~~~~~~~~~ Main Program ~~~~~~~~~~~~~~
if __name__ == "__main__":
    # Launch background threads
    threading.Thread(target=arduino_handler, daemon=True).start()
    threading.Thread(target=motor_server, daemon=True).start()
    threading.Thread(target=gps_reader, daemon=True).start()
    threading.Thread(target=telemetry_server, daemon=True).start()

    print("ROVER SERVER ACTIVE")
    app.run(host='0.0.0.0', port=CAMERA_PORT, threaded=True, use_reloader=False)