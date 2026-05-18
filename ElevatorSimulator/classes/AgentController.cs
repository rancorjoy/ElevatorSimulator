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
    public class AgentController // Controlls the logic of each agent
    {
        // Enum to store agent state
        private enum AgentState
        {
            Pressing, // going to press a button
            Waiting, // waiting on initial floor
            Pursuing, // pursuing an open elevator on the initial floor
            Boarded, // on an elevator waiting to be released
            Leaving // on target floor -> leaving simulation space
        }

        // Reference to MainWindow
        MainWindow window;

        // Agent state variables
        int targetFloor = 2; // which floor is the agent heading to?
        int initialFloor = 1; // which floor is the agent coming from?
        int waitShaft = 0; // which shaft is the agent currently waiting at?
        AgentState currentState = AgentState.Pressing; // which state is the agent in?
        float xpos = 0; // where is the agent?
        float ypos = 0;
        ElevatorController[] controllers = new ElevatorController[MainWindow.MaxShafts]; // store a copy of all elevator controller references

        // External Facing Variables
        private bool Boarded = false; // needed for drawing order
        private bool Active = true; // used to disable agents when they leave

        // Agent operation variables
        bool initialLR = false; // 0: start on left, 1: start on right
        bool targetLR = false; // 0: exit on left, 1: exit on right
        bool atButton = false; // is the agent near a call button?
        int color = 0; // valid 0 -> 9 (10 assets)
        float speed = 0.5f; // valid 0.5 -> 1 (walk speed)
        float waitPos = 0f; // valid 0 -> 1 (where they stand on platform)
        float carPos = 0; // valid 0 -> 1 (where they stand in the car)
        int lifeTimer = 0; // how many frames did this agent live?
        int patience = 4; // how many seconds until press again?
        int patientceTimer = 0;

        // Make a random in top level so that only one is needed!
        Random random = new Random();

        // Create an agent
        public AgentController(int shaftCount, int floorCount, ElevatorController[] controllers, MainWindow window) // feed raw updown values into the two integers
        {
            // Assign random variables
            assignRandomVars(shaftCount, floorCount);

            // Determine which elevator agent initially waits next to
            if (initialLR) waitShaft = shaftCount - 1;
            else waitShaft = 0;

            // Get array of current elevator controllers
            this.controllers = controllers;
            // Get MainWindow
            this.window = window;
            // Set state to pressing
            currentState = AgentState.Pressing;
        }

        // Assign Agent's random parameters
        private void assignRandomVars(int shaftCount, int floorCount)
        {
            // Assign state vairables
            initialFloor = random.Next(floorCount);
            targetFloor = random.Next(floorCount);
            while (initialFloor == targetFloor) // if the initial and target are the same...
            {
                targetFloor = random.Next(floorCount); // regenerate the target floor
            }

            ypos = Convert.ToSingle(initialFloor); // update the starting y position!

            // Assign initialLR
            double coin = random.NextDouble();
            if (coin < 0.5f) initialLR = false;
            else initialLR = true;

            if (initialLR) { xpos = (shaftCount - 1) + 0.5f; } // start position for right
            else { xpos = -0.5f; } // start position for left

            // Assign targetLR
            coin = random.NextDouble();
            if (coin < 0.5f) targetLR = false;
            else targetLR = true;

            // Assign random float variables
            speed = Convert.ToSingle(random.NextDouble());
            if (speed < 0.3f) speed = 0.3f; //minimum speed to prevent extremely slow agents
            waitPos = Convert.ToSingle(random.NextDouble());
            carPos = Convert.ToSingle(random.NextDouble());

            // Assign color
            color = random.Next(10); // assigns a number 0-9
        }

        // Getters
        public float get_xpos() { return xpos; }
        public float get_ypos() { return ypos; }
        public bool get_boarded() { return Boarded; }
        public int get_color() { return color; }
        public bool get_active() { return Active; }

        // Update when simulation dimensions change! // STATE CHANGES!
        public void UpdateControllers(ElevatorController[] controllers) // refresh controllers array
        {
            this.controllers = controllers;
        }
        public void ValidateFloor(int floorCount) // change target floor if current target is removed
        {
            if (targetFloor > floorCount - 1) // if the current target is no longer available...
            {
                if (currentState == AgentState.Waiting || currentState == AgentState.Pressing) // if the agent is still waiting or pressing
                {
                    targetFloor = random.Next(floorCount); // choose a new target
                    while (initialFloor == targetFloor) // if the initial and target are the same...
                    {
                        targetFloor = random.Next(floorCount); // regenerate the target floor
                    }
                }
                if (currentState == AgentState.Pursuing) // if the agent is pursuing a car
                {
                    currentState = AgentState.Waiting; // the agent should not enter the car
                    targetFloor = targetFloor = random.Next(floorCount); // choose a new target floor and wait
                }
                if (currentState == AgentState.Boarded) // if the agent is in a car
                {
                    targetFloor = 0; // get off on the first floor (where the elevator spawns)
                    initialFloor = 1; // ensure the initial floor is not the target floor to prevent bugs (there is always a floor 1)
                }
                if (currentState == AgentState.Leaving) // if the agent is on the target floor
                {
                    targetFloor -= 1; // the agent should "target" the previous floor
                    ypos -= 1.0f; // the agent is teleported onto the previous floor so that it is not floating
                }
            }
            if (initialFloor > floorCount - 1)
            {
                if (currentState == AgentState.Waiting || currentState == AgentState.Pressing) // if the agent is still waiting or pressing
                {
                    ypos -= 1; // move agent to the next floor down
                    initialFloor -= 1;  // move agent's intialFloor down
                }
            }
        }
        public void ValidateShaft(int shaftCount) // logic for if agent is in a shaft segment that is removed...
        {
            if (waitShaft > shaftCount - 1) // if the current shaft position is no longer available
            {
                if (currentState == AgentState.Waiting || currentState == AgentState.Pressing) // if the agent is still waiting or pressing the call button
                {
                    waitShaft -= 1; // move to the left one shaft
                    xpos -= 1.0f; // move to the left one shaft
                }
                if (currentState == AgentState.Pursuing) // if the agent is pursuing a car
                {
                    currentState = AgentState.Waiting; // the agent should not enter the car
                    waitShaft -= 1; // move to the left one shaft
                    xpos -= 1.0f; // move to the left one shaft
                }
                if (currentState == AgentState.Boarded) // if the agent is in a car
                {
                    currentState = AgentState.Leaving; // the agent should leave the simulation
                    waitShaft -= 1; // move to the left one shaft
                    xpos -= 1.0f; // move to the left one shaft
                    ypos = 0.0f; // put agent on bottom floor
                    targetFloor = 1; // have the agent exit the simulation
                }
                if (currentState == AgentState.Leaving) // if the agent is on the target floor
                {
                    xpos -= 1.0f; // move to the left one shaft
                }
            }
        }

        // Update when car assignment
        public void UpdateWaitShaft(int shaftCount, int floorCount) // agent decides which elevator will arrive first - this only matter when agent is waiting
        {
            if (currentState == AgentState.Waiting && Active) // if the agent is active and waiting
            {
                if (initialFloor < targetFloor) // is the agent going up?
                {
                    for (int s = 0; s < shaftCount; s++) // for each elevator...
                    {
                        if (initialFloor == 0 && controllers[s].get_doorPos() == 1.0f) // is the agent on floor 0 and there is an open elevator (best case)
                        {
                            waitShaft = s; // wait next to this elevator
                            break; // stop searching
                        }
                        if (initialFloor == 0 && controllers[s].get_idle() == false) // is the agent on floor 0 and there is a moving elevator anywhere?
                        {
                            waitShaft = s; // wait next to this elevator
                            break; // stop searching
                        }
                        if (controllers[s].get_dir() == 1 && controllers[s].get_pos() < ypos) // is this elevator going up and is below the agent?
                        {
                            waitShaft = s; // wait next to this elevator
                            break; // stop searching
                        }
                        if (controllers[s].get_dir() == 2 && controllers[s].get_pos() == ypos) // is the elevator idle on this floor? (last case)
                        {
                            waitShaft = s; // wait next to this elevator
                            break; // stop searching
                        }
                    }
                }
                else // is agent going down?
                {
                    for (int s = 0; s < shaftCount; s++) // for each elevator...
                    {
                        if (initialFloor == (floorCount - 1) && controllers[s].get_doorPos() == 1.0f) // is the agent on the top floor and there is an open elevator anywhere? (best case)
                        {
                            waitShaft = s; // wait next to this elevator
                            break; // stop searching
                        }
                        if (initialFloor == (floorCount - 1) && controllers[s].get_idle() == false) // is the agent on the top floor and there is a moving elevator anywhere?
                        {
                            waitShaft = s; // wait next to this elevator
                            break; // stop searching
                        }
                        if (controllers[s].get_dir() == 1 && controllers[s].get_pos() < ypos) // is this elevator going up and is below the agent?
                        {
                            waitShaft = s; // wait next to this elevator
                            break; // stop searching
                        }
                        if (controllers[s].get_dir() == 2 && controllers[s].get_pos() == ypos) // is the elevator idle on this floor? (last case)
                        {
                            waitShaft = s; // wait next to this elevator
                            break; // stop searching
                        }
                    }
                }
            }
        }

        // Update every frame
        public void UpdateState(int shaftCount, int framerate)
        {
            if (currentState == AgentState.Pressing)
            {
                if (atButton) // if the agent is at a button
                {
                    currentState = AgentState.Waiting;
                }
            }
            else if (currentState == AgentState.Waiting)
            {
                if (controllers[waitShaft].get_floor() == initialFloor && controllers[waitShaft].get_doorPos() == 1.0f) // if waited elevator is here and open
                {
                    currentState = AgentState.Pursuing;
                }
                if (patientceTimer / framerate >= patience) // if agent has waited too long...
                {
                    patientceTimer = 0; // reset patience timer
                    currentState = AgentState.Pursuing; // press the button again!
                }
            }
            else if (currentState == AgentState.Pursuing)
            {
                if (MathF.Abs(xpos - waitShaft) < 0.1) // if the car is here and agent is close enough to board...
                {
                    if (controllers[waitShaft].board())
                    {
                        currentState = AgentState.Boarded;
                        controllers[waitShaft].hitFloor(targetFloor);
                        Boarded = true;
                    }
                }
                else if (controllers[waitShaft].get_doorPos() == 0.0f) // if waited elevator is/was here not open anymore
                {
                    currentState = AgentState.Pressing;
                    if (xpos < (shaftCount - 0.5) / 2) // agent closer to the left
                    {
                        waitShaft = 0; // reset the wait shaft
                    }
                    else // agent closer to the right
                    {
                        waitShaft = shaftCount - 1; // reset the wait shaft
                    }
                }
            }
            else if (currentState == AgentState.Boarded)
            {
                if (controllers[waitShaft].get_floor() == targetFloor && controllers[waitShaft].get_doorPos() == 1.0f) // if the elevator has arrived at the target floor
                {
                    controllers[waitShaft].unboard(); // remove the agent from the elevator capacity!
                    currentState = AgentState.Leaving;
                    Boarded = false;
                }
            }
            else if (currentState == AgentState.Leaving)
            {
                if (targetLR == false) // leaving on the left
                {
                    if (xpos <= -0.5) // if the agent has arrived
                    {
                        Active = false;
                        window.agentDeactivate(lifeTimer);
                    }
                }
                if (targetLR == true) // leaving on the right
                {
                    if (xpos >= shaftCount - 0.5) // if the agent has arrived
                    {
                        Active = false;
                        window.agentDeactivate(lifeTimer);
                    }
                }
            }
        }
        public void UpdatePos(int shaftCount, int framerate)
        {
            if (Active) // if this agent is still active
            {
                // Update the timer!
                lifeTimer += 1;
                patientceTimer += 1;

                // Get button targets
                float leftButtonPos = -0.4f;
                float rightButtonPos = (shaftCount - 1) + 0.4f;
                float buttonRange = 0.05f;

                // Determine if agent is near button(s) or not
                if (shaftCount > 1) // if there are multiple elevators --> right button
                {
                    if (MathF.Abs(xpos - rightButtonPos) < buttonRange) // if agent is near right button
                    {
                        atButton = true;
                    }
                }
                if (MathF.Abs(xpos - leftButtonPos) < buttonRange) // if agent is near left button
                {
                    atButton = true;
                }

                // Set (arbitrary) error tollerance)
                float errorTol = 0.02f;

                // Get current speed
                float currentSpeed = speed / Convert.ToSingle(framerate);

                // Get wait target
                float waitTarget = Convert.ToSingle(waitShaft); // this would have user wait right on shaft center like a prick (possible)
                if (initialLR == false || shaftCount == 1) // if this agent waits to the left
                {
                    waitTarget -= waitPos / 2;
                }
                else if (initialLR) // if this agent waits to the right
                {
                    waitTarget += waitPos / 2;
                }

                // Get board target
                float boardPos = Convert.ToSingle(waitShaft); // this would have the agent stand in the center of the car (possible)
                if (carPos > 0.5f) // if this agent stands to the left
                {
                    boardPos -= (carPos * 0.2f);
                }
                else if (carPos < 0.5f) // if this agent stands to the right
                {
                    boardPos += (carPos - 0.5f) * 0.2f;
                }

                // STATE LOGIC
                if (currentState == AgentState.Pressing)
                {
                    // Determine current button target
                    float buttonTarget = leftButtonPos; // assume agent is closer to left button
                    if (shaftCount > 1) // if there are multiple elevators --> right button is possible
                    {
                        if (MathF.Abs(xpos - rightButtonPos) < MathF.Abs(xpos - leftButtonPos)) // if agent is closer to the right button...
                        {
                            buttonTarget = rightButtonPos;
                        }
                    }

                    // Determine agent direction
                    if (xpos > buttonTarget) // agent is to the right of the button
                    {
                        xpos -= currentSpeed;
                    }
                    else // agent is to the left (on on) the button
                    {
                        xpos += currentSpeed;
                    }

                    // Press the button
                    if (atButton) // if the agent is near a button...
                    {
                        if (initialFloor < targetFloor) // going up?
                        {
                            window.pressUp(initialFloor); // cal an elevator to go down
                        }
                        if (initialFloor > targetFloor) // going down?
                        {
                            window.pressDown(initialFloor); // call an elevator to go down
                        }
                    }
                }
                else if (currentState == AgentState.Waiting)
                {
                    if (xpos > waitTarget && MathF.Abs(xpos - waitTarget) > errorTol) // if the agent is right of its waiting target
                    {
                        xpos -= currentSpeed;
                    }
                    if (xpos < waitTarget && MathF.Abs(xpos - waitTarget) > errorTol) // if the agent is left of its waiting target
                    {
                        xpos += currentSpeed;
                    }
                }
                else if (currentState == AgentState.Pursuing)
                {
                    if (xpos > waitShaft && MathF.Abs(xpos - waitShaft) > errorTol) // if the agent is right of its waiting target
                    {
                        xpos -= currentSpeed;
                    }
                    if (xpos < waitShaft && MathF.Abs(xpos - waitShaft) > errorTol) // if the agent is left of its waiting target
                    {
                        xpos += currentSpeed;
                    }
                    if (MathF.Abs(xpos - waitShaft) < 0.15 && controllers[waitShaft].canBoard()) // if the agent is close enough and elevator has room
                    {
                        controllers[waitShaft].openPress(); // open the elevator
                    }
                }
                else if (currentState == AgentState.Boarded)
                {
                    Boarded = true;
                    if (controllers[waitShaft].get_floor() == targetFloor)
                    {
                        controllers[waitShaft].openPress(); // ensure doors open in this state!
                    }
                    else
                    {
                        controllers[waitShaft].hitFloor(targetFloor); // some times it does not hit the first time if the car has dir = none
                    }
                    if (xpos > boardPos && MathF.Abs(xpos - boardPos) > errorTol) // if the agent is right of its waiting target
                    {
                        xpos -= currentSpeed;
                    }
                    if (xpos < boardPos && MathF.Abs(xpos - boardPos) > errorTol) // if the agent is left of its waiting target
                    {
                        xpos += currentSpeed;
                    }
                    ypos = controllers[waitShaft].get_pos(); // move up/down with the elevator!
                }
                else if (currentState == AgentState.Leaving)
                {
                    ypos = targetFloor; // to fix any accumulated error from elevator ypos!
                    Boarded = false;
                    if (targetLR == false) // leaving to the left
                    {
                        xpos -= currentSpeed;
                    }
                    if (targetLR == true) // leaving to the right
                    {
                        xpos += currentSpeed;
                    }
                }
            }
        }
    }
}
