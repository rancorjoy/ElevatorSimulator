// Include all needed libraries
using System;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.Toolkit;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ElevatorSimulator.classes
{
    public class ElevatorController
    {
        // State Variables
        private MainWindow window; // stores the MainWindow
        private float doorPos = 1.0f; // 1 == open, 0 == closed
        private float pos = 0; // exact position in terms of floor number - eg 2.5 for 1/2 between floors 2 and 3
        private int floor = 0; // cannonical floor (displayed on 7-segment display) - round up when dir = 0, round down when dir = 1
        private int shaft = 0; // which shaft does this elevator belong to? (starts at 0 on the left -> shafts - 1 on the right)
        private bool searchFail = false; // moving elevator fails to find target
        private enum carStates
        {
            idle, // At top or bottom, or no floors requested (doors closed)
            idle_closing, // Transition from open to idle
            open, // At floor ready to board/unboard (doors open)
            closed, // At floor ready to move
            opening, // Transitioning from closed/partially closed to open
            closing, // Transitioning from open to closed
            moving // Currently moving
        }
        private carStates currentState = carStates.idle;
        private enum Direction
        {
            down,
            up,
            none
        }
        private Direction dir = Direction.up;

        // Operation Variables
        private bool openPressed = false; // this is set true when the 'open door' button is pressed or when the door detects a blockage
        private bool closedPressed = false; // this is set true when the 'close door' button is pressed and removed when the 'open door' button is pressed
        private bool[] floorsPressed = new bool[MainWindow.MaxFloors]; // an array storing all floors that are added as stops - maximum floors is 127 for this simulation
        private bool deactivated = false; // When a shaft is removed - this variable disables elevator FOREVER (adding a shaft makes a new controller to replace this one)
        private int prevTarget = 0; //Stores the previous stopped floor for elevator movement curve
        private int currentTarget = -1; // Used to simplify state and movement logic, represents the current target floor if there is one
        private int delayTime = 0; // used to keep track of time since the doors opened or closed
        private int delayTimer = 0; // these values get updated each frame incase they are changed during run-time
        private int framerate = 24; // ^
        private float moveSpeed = 0.5f; // ^
        private float catchThresh = 0.5f; // ^
        private float doorSpeed = 0.5f; // ^
        private float posError = 0.005f; // This gives the movement system a small amount of error room
        private int topFloor = 1; // This stores the current top floor passed from the MainWindow
        private int capacity = 8; // Stores global car capacity (updates each frame)
        private int carCapacity = 0; // The current number of agents in this car

        public ElevatorController(int this_shaft, MainWindow u_window)
        {
            dir = Direction.up; // all elevators start on first floor ready to go up
            doorPos = 0.0f; // doors start graphically closed
            pos = 0.0f; // all elevators start on the first floor at ground level
            floor = 0; // all elevators start on the first floor
            shaft = this_shaft; // this elevator has a left-right location that is unique
            window = u_window; // this is the main window
        }

        // Getters - these values are needed for graphical output
        public int get_dir()
        {
            if (dir == Direction.down) return 0;
            if (dir == Direction.up) return 1;
            else return 2;
        }
        public bool get_idle() { return currentState == carStates.idle; }
        public float get_pos() { return pos; }
        public float get_doorPos() { return doorPos; }
        public int get_floor() { return floor; }
        public int get_shaft() { return shaft; }
        public bool get_deactivated() { return deactivated; }
        public bool get_full() { return carCapacity == capacity; }
        public bool get_empy() { return carCapacity == 0; }

        // Elevator Agent Functions
        public bool board()
        {
            if (carCapacity < capacity) // there is room in the elevator
            {
                carCapacity += 1;
                Console.WriteLine("Elevator " + shaft + " carCapacity Updated To " + carCapacity);
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool canBoard() // check boarding but do not board!
        {
            if (carCapacity < capacity) // there is room in the elevator
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public void unboard()
        {
            carCapacity -= 1;
            Console.WriteLine("Elevator " + shaft + " carCapacity Updated To " + carCapacity);
        }

        // Elevator Inputs
        public void openPress() // an agent has pressed 'open' or has blocked the elevator door
        {
            openPressed = true;
            closedPressed = false;
        }
        public void closePress()
        {
            closedPressed = true;
        }
        public void hitFloor(int req_floor) // 0 indexed floor
        {
            if (req_floor != floor) // if the requested floor IS NOT the current floor
            {
                if (currentState == carStates.idle || dir == Direction.none) // if the elevator is idle
                {
                    floorsPressed[req_floor] = true; // enable the requested floor as a stop
                    if (req_floor > floor) // check which direction the elevator is traveling in now
                    {
                        dir = Direction.up; // going up
                    }
                    else
                    {
                        dir = Direction.down; // going down
                    }
                }
                else // if the car is not idle (it is traveling in a direction)
                {
                    if (dir == Direction.up) // going up
                    {
                        if (req_floor > floor && Convert.ToSingle(req_floor) > (pos + catchThresh)) // if the requested floor is in th correct direction and far enough away
                        {
                            floorsPressed[req_floor] = true; // enable the requested floor as a stop
                        }
                    }
                    if (dir == Direction.down) // going down
                    {
                        if (req_floor < floor && Convert.ToSingle(req_floor) < (pos - catchThresh)) // if the requested floor is in th correct direction and far enough away
                        {
                            floorsPressed[req_floor] = true; // enable the requested floor as a stop
                        }
                    }
                }
            }
            else if (carCapacity != capacity) // if the elevator is called to its own floor AND is not full...
            {
                openPress(); // open the elevator
                delayTimer = 0; // reset the delay timer to keep the doors open!
            }
        }
        public void deactivate()
        {
            deactivated = true;
        }

        // Update this method when a floor is removed! CHANGES STATE!
        public void validateFloor(int floorCount)
        {
            if (pos > Convert.ToSingle(floorCount - 1)) // if the elevator is higher than the current max floor
            {
                // put elevator at first floor and have it open
                currentState = carStates.opening;
                Console.WriteLine("Elevator " + shaft + " in State: Opening");
                delayTimer = 0;
                pos = 0.0f;
                floor = 0;
                doorPos = 0.0f;
            }
        }

        // Methods that should be updated each frame IN ORDER!
        public void updateKinematicVars(int u_time, float u_speed, float u_door_speed, float u_catch, int u_framerate, int u_topFloor, int u_capacity) // provide the elevator with its current timer value, movespeed, door speed and framerate ect
        {
            delayTime = u_time * u_framerate; // Passed values update private values
            moveSpeed = u_speed;
            doorSpeed = u_door_speed / Convert.ToSingle(u_framerate);
            catchThresh = u_catch;
            framerate = u_framerate;
            topFloor = u_topFloor;
            capacity = u_capacity;
        }
        public void updateState() // should be run each frame!
        {
            if (deactivated == false) // if this elevator is still in service
            {
                if (currentState == carStates.idle) // transitions from idle
                {
                    delayTimer = 0; // Ensure the timer is reset!
                    if (openPressed) // if the open button is pressed (even while in idle)
                    {
                        currentState = carStates.opening; // start opening the doors
                        Console.WriteLine("Elevator " + shaft + " in State: Opening");
                        delayTimer = 0;
                    }
                    else if (floorsPressed.Any(b => b == true)) // if there is a target floor
                    {
                        nextFloorSet(); // find the next floor to move to!
                        currentState = carStates.moving; // if a target floor is given, the elevator starts moving
                        Console.WriteLine("Elevator " + shaft + " in State: Moving");
                        prevTarget = floor; // set the previous taget
                    }
                    else if (carCapacity != 0) // if the car is somehow idle when it has passengers, it should be opened!
                    {
                        currentState = carStates.opening; // start opening the doors
                        Console.WriteLine("Elevator " + shaft + " in State: Opening");
                        delayTimer = 0;
                    }
                }
                if (currentState == carStates.idle_closing) // transitions from idle_closing
                {
                    if (doorPos <= 0.0f) // if the doors are fully closed
                    {
                        doorPos = 0.0f; // incase there is any overshoot!
                        currentState = carStates.idle; // The car is in the idle state and is ready to be assigned a target
                        Console.WriteLine("Elevator " + shaft + " in State: Idle");
                        delayTimer = 0; // Ensure the timer is reset!
                    }
                    else if (openPressed) // if the doors are not fully closed and open has been pressed
                    {
                        currentState = carStates.opening; // start opening the doors again
                        Console.WriteLine("Elevator " + shaft + " in State: Opening");
                        delayTimer = 0;
                    }
                    if (floorsPressed.Any(b => b == true)) // if there is a target floor
                    {
                        currentState = carStates.closing; // keep closing the elevator but prepare for movement
                        Console.WriteLine("Elevator " + shaft + " in State: Closing");
                    }
                }
                if (currentState == carStates.open) // transitions from open
                {
                    if (closedPressed) // if the close button is pressed
                    {
                        currentState = carStates.closing; // start closing the doors
                        Console.WriteLine("Elevator " + shaft + " in State: Closing");
                    }
                    if (openPressed) // if the open button is pressed (notice this is not an else if)
                    {
                        currentState = carStates.open; // if the open button is pressed the car stays open EVEN if the close button was pressed
                        Console.WriteLine("Elevator " + shaft + " in State: Open");
                        delayTimer = 0; // Ensure the timer is reset!
                    }
                    else if (delayTimer >= delayTime && openPressed == false) // if the delay timer has run out and openPressed == false
                    {
                        if (floorsPressed.All(b => b == false)) // if there are no target floors
                        {
                            currentState = carStates.idle_closing; // start closing the doors and idle the elevator
                            Console.WriteLine("Elevator " + shaft + " in State: Idle Closing");
                        }
                        else // if there are more target floors
                        {
                            currentState = carStates.closing; // start closing the doors for the elevator to move to next target
                            Console.WriteLine("Elevator " + shaft + " in State: Closing");
                        }
                    }
                }
                if (currentState == carStates.opening) // transitions from opening
                {
                    delayTimer = 0; // reset timer
                    if (doorPos >= 1.0f) // if the doors are fully open
                    {
                        doorPos = 1.0f; // incase there is any overshoot!
                        currentState = carStates.open; // The car is in the open state and ready for boarding/unboarding
                        Console.WriteLine("Elevator " + shaft + " in State: Open");
                        delayTimer = 0; // Ensure the timer is reset!
                    }
                }
                if (currentState == carStates.closing) // transitions from closing
                {
                    if (doorPos <= 0.0f) // if the doors are fully closed
                    {
                        doorPos = 0.0f; // incase there is any overshoot!
                        currentState = carStates.closed; // The car is in the closed state and is ready to move to the next target
                        Console.WriteLine("Elevator " + shaft + " in State: Closed");

                        delayTimer = 0; // Ensure the timer is reset!
                    }
                    else if (openPressed) // if the doors are not fully closed and open has been pressed
                    {
                        currentState = carStates.opening; // start opening the doors again
                        Console.WriteLine("Elevator " + shaft + " in State: Opening");
                        delayTimer = 0;
                    }

                }
                if (currentState == carStates.closed) // transitions from closed
                {
                    if (openPressed) // if the open button is pressed...
                    {
                        currentState = carStates.opening; // start opening the doors again
                        Console.WriteLine("Elevator " + shaft + " in State: Opening");
                        delayTimer = 0;
                    }
                    else if (delayTimer >= delayTime / 2) // if the delay timer has run out and openPressed == false
                    {
                        prevTarget = floor; // set the previous taget
                        currentState = carStates.moving; //start moving the elevator
                        nextFloorSet(); // find the next floor to move to!
                        Console.WriteLine("Elevator " + shaft + " in State: Moving");
                    }
                }
                if (currentState == carStates.moving) //transitions from moving
                {
                    if (Math.Abs(pos - Convert.ToSingle(currentTarget)) <= posError) // if the elevator has reached its target
                    {
                        floor = Convert.ToInt32(MathF.Round(pos)); // ensure the floor updates correctly
                        pos = Convert.ToSingle(floor); // round off pos so errors do not accumulate (think of this as the sensor re-tarring)
                        currentState = carStates.opening; // start opening the elevator
                        Console.WriteLine("Elevator " + shaft + " in State: Opening");
                        delayTimer = 0;
                        floorsPressed[floor] = false; //remove this floor from the elevator's targets
                        if (dir == Direction.up)
                        {
                            window.clearUp(floor);
                        }
                        if (dir == Direction.down)
                        {
                            window.clearDown(floor);
                        }
                    }
                    if (searchFail == true) // if somehow the elevator has no targets (previous checks failed)
                    {
                        currentState = carStates.idle; // the elevator is idle instead
                        Console.WriteLine("Elevator " + shaft + " in State: Idle");
                    }
                }
            }
            else // if the car is deactivated
            {
                currentState = carStates.idle; //idle the car
            }
        }
        public void updateTimer()
        {
            delayTimer += 1; // update the timer by one
        }
        public void updateDoorPos()
        {
            if (currentState == carStates.opening) // if the doors are opening
            {
                doorPos += doorSpeed;
            }
            if (currentState == carStates.closing || currentState == carStates.idle_closing) // if the doors are closing
            {
                doorPos -= doorSpeed;
            }
        }
        public void updateCarPos()
        {
            if (currentState == carStates.moving)
            {
                if (dir == Direction.up)// going up
                {
                    if (searchFail == false)
                    {
                        // Update floor from pos
                        floor = (int)Math.Floor(pos); // Round down in this case

                        float currentSpeed = 0f;

                        // Check which half of the floor we're in
                        float floorMidpoint = Convert.ToSingle(floor) + 0.5f;

                        if (pos < floorMidpoint) // in lower half of floor (accelerating region)
                        {
                            if (prevTarget == floor) // starting from previous floor
                            {
                                currentSpeed = (1.1f * moveSpeed) - (4 * moveSpeed * (pos - Convert.ToSingle(floor) - 0.5f) * (pos - Convert.ToSingle(floor) - 0.5f));
                            }
                            else // not starting from previous floor
                            {
                                currentSpeed = moveSpeed;
                            }
                        }
                        else // in upper half of floor (decelerating region)
                        {
                            if (currentTarget == floor + 1) // stopping at next floor
                            {
                                currentSpeed = moveSpeed - (4 * moveSpeed * (pos - Convert.ToSingle(floor) - 0.5f) * (pos - Convert.ToSingle(floor) - 0.5f));
                            }
                            else // not stopping at next floor
                            {
                                currentSpeed = moveSpeed;
                            }
                        }

                        pos += currentSpeed / framerate;
                    }
                }
                if (dir == Direction.down) // going down
                {
                    if (searchFail == false)
                    {
                        // Update floor from pos
                        floor = (int)Math.Ceiling(pos); // Round up in this case

                        float currentSpeed = 0f;

                        // Check which half of the floor we're in
                        float floorMidpoint = Convert.ToSingle(floor) - 0.5f;

                        if (pos > floorMidpoint) // in upper half of floor (accelerating region)
                        {
                            if (prevTarget == floor) // starting from previous floor
                            {
                                currentSpeed = (1.1f * moveSpeed) - (4 * moveSpeed * (Convert.ToSingle(floor) - pos - 0.5f) * (Convert.ToSingle(floor) - pos - 0.5f));
                            }
                            else // not starting from previous floor
                            {
                                currentSpeed = moveSpeed;
                            }
                        }
                        else // in lower half of floor (decelerating region)
                        {
                            if (currentTarget == floor - 1) // stopping at next floor
                            {
                                currentSpeed = moveSpeed - (4 * moveSpeed * (Convert.ToSingle(floor) - pos - 0.5f) * (Convert.ToSingle(floor) - pos - 0.5f));
                            }
                            else // not stopping at next floor
                            {
                                currentSpeed = moveSpeed;
                            }
                        }

                        pos -= currentSpeed / framerate;
                    }
                }
            }
        }
        public void updateCarDirection() // conditions where car should lose direction state
        {
            if (floor == topFloor - 1 && currentState == carStates.opening) // elevator on top floor and open
            {
                dir = Direction.none; // clear the direction flag
            }
            else if (floor == 0 && currentState == carStates.opening) // elevator on bottom floor and open
            {
                dir = Direction.none; // clear the direction flag
            }
            else if (floorsPressed.All(b => b == false)) // if there are no more targets...
            {
                dir = Direction.none; // clear the direction flag
            }
        }
        public void clearFlags()
        {
            closedPressed = false;
            openPressed = false;
            searchFail = false;
        }


        // Helprer function
        private int nextFloorUp() //returns the next target floor going up
        {
            for (int f = floor + 1; f < MainWindow.MaxFloors - 1; f++) // for all index values greater than the current floor
            {
                if (floorsPressed[f]) // search for a pressed floor
                {
                    return f; // retrun the first one found
                }
            }
            return -1; // fail state
        }
        private int nextFloorDown() //returns the next target floor going down
        {
            for (int f = floor - 1; f > -1; f--) // for all index values greater than the current floor
            {
                if (floorsPressed[f]) // search for a pressed floor
                {
                    return f; // retrun the first one found
                }
            }
            return -1; // fail state
        }
        private void nextFloorSet()
        {
            if (dir == Direction.up) // going up
            {
                // Find the next floor
                int tempFloor = nextFloorUp();
                if (tempFloor == -1) // has the eelvator failed to find a target?
                {
                    searchFail = true; // flag the failure
                    Console.WriteLine("Elevator " + shaft + " failed to find target!");
                }
                else // expected outcome
                {
                    currentTarget = tempFloor;
                }
            }
            if (dir == Direction.down) // going down
            {
                // Find the next floor
                int tempFloor = nextFloorDown();
                if (tempFloor == -1) // has the eelvator failed to find a target?
                {
                    searchFail = true; // flag the failure
                    Console.WriteLine("Elevator " + shaft + " failed to find target!");
                }
                else // expected outcome
                {
                    currentTarget = tempFloor;
                }
            }
        }

    } // Controls the logic of each elevator
}
