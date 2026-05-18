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
    public class ElevatorShaft
    {
        // External References
        private ElevatorController controller; // Reference to active controller
        private MainWindow mainWindow; // Reference to main window
        private int floor; // Reference to current floor

        public ElevatorShaft(ElevatorController this_controller, int this_floor, MainWindow this_mainWindow) // constructor to instantiate an elevator shaft
        {
            controller = this_controller; // an elevator controller is REQUIRED for this to function correctly
            floor = this_floor; // sets the shaft segment's floor to the current floor
            mainWindow = this_mainWindow; // pass a reference to the main window
        }

        public void RenderBack()
        {
            if (mainWindow.floorUpDown.Value != null) // if there is a current number of floors...
            {
                int x_pos = controller.get_shaft() * 192;
                int y_pos = ((mainWindow.floorUpDown.Value.Value) * 192) - floor * 192;

                mainWindow.DrawImage(Images.floor_back, x_pos, y_pos);
            }
        }
        public void RenderFront()
        {
            if (mainWindow.floorUpDown.Value != null) // if there is a current number of floors...
            {
                int x_pos = controller.get_shaft() * 192;
                int y_pos = ((mainWindow.floorUpDown.Value.Value) * 192) - floor * 192;

                if (mainWindow.showWalls) mainWindow.DrawImage(Images.wall, x_pos, y_pos);
                if (mainWindow.showFrames) mainWindow.DrawImage(Images.frame, x_pos, y_pos);
                mainWindow.DrawImage(Images.floor_front, x_pos, y_pos);
            }
        }
        public void RenderButtons()
        {
            if (mainWindow.floorUpDown.Value != null && mainWindow.shaftUpDown.Value != null) // if there is a current number of floors and shafts
            {
                if (mainWindow.showFrames || mainWindow.showWalls) // if either walls or frames are shown, render the buttons!
                {
                    int x_pos = controller.get_shaft() * 192;
                    int y_pos = ((mainWindow.floorUpDown.Value.Value) * 192) - floor * 192;

                    if (controller.get_shaft() == 0) // if this is the left-most elevator
                    {
                        // Draw summon terminals
                        if (floor == 0) mainWindow.DrawImage(Images.button_l_up, x_pos, y_pos); // bottom floor
                        else if (floor == mainWindow.floorUpDown.Value.Value - 1) mainWindow.DrawImage(Images.button_l_down, x_pos, y_pos); // top floor
                        else mainWindow.DrawImage(Images.button_l_both, x_pos, y_pos); // all other floors

                        // Draw lights
                        if (mainWindow.UpRequests[floor] == true && floor != mainWindow.floorUpDown.Value.Value - 1) mainWindow.DrawImage(Images.button_l_act_up, x_pos, y_pos);
                        if (mainWindow.DownRequests[floor] == true && floor != 0) mainWindow.DrawImage(Images.button_l_act_down, x_pos, y_pos);
                    }
                    if (controller.get_shaft() == mainWindow.shaftUpDown.Value.Value - 1 && controller.get_shaft() != 0) // if this is the right-most elevator (and there are more than 1 elevators)
                    {
                        // Draw summon terminals
                        if (floor == 0) mainWindow.DrawImage(Images.button_r_up, x_pos, y_pos); // bottom floor
                        else if (floor == mainWindow.floorUpDown.Value.Value - 1) mainWindow.DrawImage(Images.button_r_down, x_pos, y_pos); // top floor
                        else mainWindow.DrawImage(Images.button_r_both, x_pos, y_pos); // all other floors

                        // Draw lights
                        if (mainWindow.UpRequests[floor] == true && floor != mainWindow.floorUpDown.Value.Value - 1) mainWindow.DrawImage(Images.button_r_act_up, x_pos, y_pos);
                        if (mainWindow.DownRequests[floor] == true && floor != 0) mainWindow.DrawImage(Images.button_r_act_down, x_pos, y_pos);
                    }
                }
            }
        }
        public void RenderDisplay()
        {
            if (mainWindow.floorUpDown.Value != null && mainWindow.shaftUpDown.Value != null)
            {
                int x_pos = controller.get_shaft() * 192;
                int y_pos = ((mainWindow.floorUpDown.Value.Value) * 192) - floor * 192;

                if (mainWindow.showFrames) // All display elements are rendered on the frame!
                {

                    // If frames are enabled, each floor renders doors which are always closed unless they belong to the elevator... these are the other doors :)
                    if (controller.get_pos() != Convert.ToSingle(floor)) // if the elevator is not EXACTLY aligned with the floor...
                    {
                        mainWindow.DrawImage(Images.door_left, x_pos, y_pos); // Left door
                        mainWindow.DrawImage(Images.door_right, x_pos, y_pos); // Right door
                    }


                    if (controller.get_idle() == false) // the elevator is NOT idle
                    {
                        if (controller.get_dir() == 0) // the elevator is going down
                        {
                            mainWindow.DrawImage(Images.top_act_down, x_pos, y_pos); // enable down indicator light
                        }
                        if (controller.get_dir() == 1) // the elevator is going up
                        {
                            mainWindow.DrawImage(Images.top_act_up, x_pos, y_pos); // enable up indicator light
                        }
                    }

                    // Display on seven segment displays
                    int car_floor = controller.get_floor() + 1; // floors start at 0 but are labeled as starting at 1!
                    if (car_floor > 9)
                    {
                        if (car_floor > 99) // floor is bigger than 99 (3 digits)
                        {
                            int two_digits = car_floor % 100;
                            int ones = two_digits % 10;
                            int tens = (two_digits - ones) / 10;
                            int hund = (floor - two_digits) / 100;

                            mainWindow.DrawImage(Decoder(car_floor % 10), x_pos, y_pos); // Display 1's place normally
                            mainWindow.DrawImage(Decoder(tens), x_pos - 5, y_pos); // Display 10's place
                            mainWindow.DrawImage(Decoder(hund), x_pos - 10, y_pos); // Display 10o's place
                        }
                        else //floor is between 9 and 100 (2 digits)
                        {
                            int ones = car_floor % 10;
                            int tens = (car_floor - ones) / 10;

                            mainWindow.DrawImage(Decoder(car_floor % 10), x_pos, y_pos); // Display 1's place normally
                            mainWindow.DrawImage(Decoder(tens), x_pos - 5, y_pos); // Display 10's place
                        }
                    }
                    else // floor is less than 9 (1 digit)
                    {
                        mainWindow.DrawImage(Decoder(car_floor), x_pos, y_pos); // Only one place to display!
                    }
                }
            }
        }
        public ImageSource Decoder(int digit)
        {
            if (digit == 0) return Images.seven_seg_0;
            else if (digit == 1) return Images.seven_seg_1;
            else if (digit == 2) return Images.seven_seg_2;
            else if (digit == 3) return Images.seven_seg_3;
            else if (digit == 4) return Images.seven_seg_4;
            else if (digit == 5) return Images.seven_seg_5;
            else if (digit == 6) return Images.seven_seg_6;
            else if (digit == 7) return Images.seven_seg_7;
            else if (digit == 8) return Images.seven_seg_8;
            else if (digit == 9) return Images.seven_seg_9;
            else return Images.seven_seg_0;
        }

    } // Graphical element that controls tiles (elevator bays)
    public class ElevatorCar // Graphical element that controls an elevator car (one car per shaft)
    {
        // External References
        private ElevatorController controller; // Reference to active controller
        private MainWindow mainWindow; // Reference to main window

        public ElevatorCar(ElevatorController this_controller, MainWindow this_mainWindow) // constructor to instantiate an elevator shaft
        {
            controller = this_controller; // an elevator controller is REQUIRED for this to function correctly
            mainWindow = this_mainWindow; // pass a reference to the main window
        }

        public void RenderBack()
        {
            if (mainWindow.floorUpDown.Value != null && controller.get_deactivated() == false) // if there is a current number of floors and controller is active...
            {
                int x_pos = controller.get_shaft() * 192 + 28;
                double y_pos = ((mainWindow.floorUpDown.Value.Value) * 192) - controller.get_pos() * 192 + 18;

                mainWindow.DrawImage(Images.car_back, x_pos, y_pos, 136, 165);
                mainWindow.DrawImage(Images.car, x_pos, y_pos, 136, 165);
                if (mainWindow.showWalls == false) { mainWindow.DrawImage(Images.car_top, x_pos, y_pos - 101, 136, 165); }
            }
        }
        public void RenderFront()
        {
            if (mainWindow.floorUpDown.Value != null && controller.get_deactivated() == false) // if there is a current number of floors and controller is active...
            {
                int x_pos = controller.get_shaft() * 192 + 28;
                double y_pos = ((mainWindow.floorUpDown.Value.Value) * 192) - controller.get_pos() * 192 + 18;

                if (mainWindow.showCarFronts) mainWindow.DrawImage(Images.car_front, x_pos, y_pos, 136, 165);
                if (mainWindow.showCarFronts) mainWindow.DrawImage(Images.door_slider, x_pos - 28, y_pos - 18); //remove offset because this image is normal size
            }
        }
        public void RenderDoors()
        {
            if (mainWindow.floorUpDown.Value != null && controller.get_deactivated() == false) // if there is a current number of floors and controller is active...
            {
                int x_pos = controller.get_shaft() * 192;
                int y_pos = ((mainWindow.floorUpDown.Value.Value) * 192) - Convert.ToInt32(controller.get_pos()) * 192;
                double offset = controller.get_doorPos() * 32.0;

                if (mainWindow.showCarFronts || mainWindow.showFrames) mainWindow.DrawImage(Images.door_left, x_pos - offset, y_pos);
                if (mainWindow.showCarFronts || mainWindow.showFrames) mainWindow.DrawImage(Images.door_right, x_pos + offset, y_pos);
            }
        }
    }
    public class Agent // Graphical element of each agent
    {
        // External References
        private AgentController controller; // Reference to active controller
        private MainWindow mainWindow; // Reference to main window
        private ImageSource img; // Stores the image for the agent

        public Agent(AgentController this_controller, MainWindow this_mainWindow) // constructor to instantiate an agent
        {
            controller = this_controller; // an elevator controller is REQUIRED for this to function correctly
            mainWindow = this_mainWindow; // pass a reference to the main window
            img = setImage(controller.get_color()); // set the correct image as img
        }

        public ImageSource setImage(int color)
        {
            if (color == 0) return Images.agent_blue;
            else if (color == 1) return Images.agent_green;
            else if (color == 2) return Images.agent_magenta;
            else if (color == 3) return Images.agent_mint;
            else if (color == 4) return Images.agent_orange;
            else if (color == 5) return Images.agent_pink;
            else if (color == 6) return Images.agent_red;
            else if (color == 7) return Images.agent_salmon;
            else if (color == 8) return Images.agent_teal;
            else if (color == 9) return Images.agent_violet;
            else return Images.agent_blue;
        }

        public void RenderAgent()
        {
            if (mainWindow.floorUpDown.Value != null && controller.get_active()) // if there is a current number of floors...
            {
                double x_pos = controller.get_xpos() * 192;
                double y_pos = (mainWindow.floorUpDown.Value.Value - controller.get_ypos()) * 192;

                mainWindow.DrawImage(img, x_pos, y_pos);
            }
        }
    }
}
