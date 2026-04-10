import socket
import keyboard
import time

HOST = "172.20.10.3"
PORT = 80

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.connect((HOST, PORT))

print("Connected to rover over WiFi")
print("Controls:")
print("W = forward")
print("S = backward")
print("A = left")
print("D = right")
print("SPACE = stop")
print("ESC = quit")

last_cmd = None

def send(cmd):
    global last_cmd
    if cmd != last_cmd:
        sock.send(cmd.encode())
        last_cmd = cmd

try:
    while True:
        if keyboard.is_pressed('w'):
            send('w')
        elif keyboard.is_pressed('s'):
            send('s')
        elif keyboard.is_pressed('a'):
            send('a')
        elif keyboard.is_pressed('d'):
            send('d')
        elif keyboard.is_pressed('space'):
            send('x')
        elif keyboard.is_pressed('esc'):
            send('x')
            break
        else:
            send('x')
        time.sleep(0.05)

finally:
    try:
        sock.send(b'x')
    except:
        pass
    sock.close()
    print("Stopped.")