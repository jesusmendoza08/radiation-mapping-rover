/* 
The following code is the code used for testing the H-Bridge
and the DC Motors functionality

First step is create a constant variable that will hold the value of the pin numbers we will be 
using on the Arduino Giga R1 Board
We have named the following variables as such due to our H-Bridge design
and for better readibility when doing the wiring.
*/

const int A_1= 2; // Creating a Constant Called A_1 that holds the value of Pin 2
const int B_1= 3; // Creating a Constant Called B_1 that holds the value of Pin 3

const int A_2= 4; // Creating a Constant Called A_2 that holds the value of Pin 4
const int B_2= 5; // Creating a Constant Called B_2 that holds the value of Pin 5

/*The following section under "setup" will be on charge of allocating the Pins with their respective numerical value that indicates their place on the Arduino board.*/
void setup() 
{

  pinMode(A_1, OUTPUT); //Setting up Pin 2 as OUTPUT
  pinMode(B_1, OUTPUT);//Setting up Pin 3 as OUTPUT

  pinMode(A_2, OUTPUT); //Setting up Pin 4 as OUTPUT
  pinMode(B_2, OUTPUT); //Setting up Pin 5 as OUTPUT

}

/*
The following block of code under "Loop" is what the Arduino board will continue to run endlessly
*/
void loop() {
  /*
    Power goes from 0 to 255.
    With a 12V source, the value of 100 should be around 40% Duty Cycle of Motor.
    We use 'analogWrite' Instead of 'digitalWrite' to be able to manipulate the
    Duty Cycle, since 'digitalWrite' only allows to use LOW or HIGH values/
  */

// LOW, LOW STOP
  analogWrite(A_1, 0);  // A1 = 0.  LOW
  analogWrite(B_1, 0);  // B1 = 0.  LOW

  analogWrite(A_2, 0);  //A2 = 0.  LOW
  analogWrite(B_2, 0);  //B2 = 0. LOW
  delay(2000);

//HIGH, LOW FORWARD
  analogWrite(A_1, 100); //A1 = 1. HIGH
  analogWrite(B_1, 0);   //B1 = 0. LOW

  analogWrite(A_2, 100); //A2 = 1.  HIGH
  analogWrite(B_2, 0);  //B2 = 0.  LOW
  delay(2000);

// LOW, LOW STOP
 analogWrite(A_1, 0);  // A1 = 0.  LOW
  analogWrite(B_1, 0);  // B1 = 0.  LOW

  analogWrite(A_2, 0);  //A2 = 0.  LOW
  analogWrite(B_2, 0);  //B2 = 0. LOW
  delay(2000);

//LOW, HIGH REVERSE
  analogWrite(A_1, 0);  // A1 = 0. LOW
  analogWrite(B_1, 100); // B1 = 1. HIGH

  analogWrite(A_2, 0); // A2 = 0. LOW
  analogWrite(B_2, 100);  //B1 = 1. HIGH
  delay(2000);

}

