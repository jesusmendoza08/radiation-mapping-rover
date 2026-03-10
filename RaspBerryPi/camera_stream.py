import cv2
from flask import Flask, Response

app = Flask(__name__)
cap =cv2.VideoCapture(0)

def gen_frames():
        while True:
            success, frame = cap.read()
            if not success:
                break
            else:
                #Encode the frame as a JPEG File
                ret, buffer = cv2.imencode('.jpg',frame)
                frame_bytes = buffer.tobytes()

                #Tield frame in HTTP multipart format
                yield(b'--frame/r/n'
                b'Content-Type:image/jpeg\r\n\r\n' + frame_bytes + b'\r\n')
@app.route('/video_feed')
def video_feed():
    return Response(gen_frames(),mimetype = 'multipart/x-mixed-replace;boundary=frame')
if __name__ == '__main__':
    app.run(host = '0.0.0.0', port = 5000)