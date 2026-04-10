import cv2

#Opening the first camera device
cap = cv2.VideoCapture(0)

if not cap.isOpened():
    print("Cannot open Camera")
    exit()
while True:
    #Capture frame-by-frame
    ret, frame = cap.read()

    if not ret:
        print("Can't receive frame (stream end?). Exiting...")
        break

        #Resize for faster processing
        frame = cv2.resize(frame, (640,480))

        #Display the resulting frame
        cv2.imshow('Rover Camera', frame)

        #Press 'q' to Quit
        if cv2.waitkey(1) & 0xFF == ord('q'):
            break
#release the capture
cap.release()
cv2.destroyAllWindows()