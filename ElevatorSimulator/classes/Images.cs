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
    public static class Images // This class deals with loading images from the "Assets" folder
    {
        // Loading in all images:
        public readonly static ImageSource agent_blue = LoadImage("Agent_blue.png"); // Load in all agent colors
        public readonly static ImageSource agent_green = LoadImage("Agent_green.png");
        public readonly static ImageSource agent_magenta = LoadImage("Agent_magenta.png");
        public readonly static ImageSource agent_mint = LoadImage("Agent_mint.png");
        public readonly static ImageSource agent_orange = LoadImage("Agent_orange.png");
        public readonly static ImageSource agent_pink = LoadImage("Agent_pink.png");
        public readonly static ImageSource agent_red = LoadImage("Agent_red.png");
        public readonly static ImageSource agent_salmon = LoadImage("Agent_salmon.png");
        public readonly static ImageSource agent_teal = LoadImage("Agent_teal.png");
        public readonly static ImageSource agent_violet = LoadImage("Agent_violet.png");
        public readonly static ImageSource button_l_act_down = LoadImage("button_l_act_down.png"); // Left button down activated
        public readonly static ImageSource button_l_act_up = LoadImage("button_l_act_up.png"); // Left button up activated
        public readonly static ImageSource button_l_both = LoadImage("button_l_both.png"); // Left button up and down (middle floors)
        public readonly static ImageSource button_l_down = LoadImage("button_l_down.png"); // Left button down (for top floor)
        public readonly static ImageSource button_l_up = LoadImage("button_l_up.png"); // Left button down (for bottom floor)
        public readonly static ImageSource button_r_act_down = LoadImage("button_r_act_down.png"); // Right button down activated
        public readonly static ImageSource button_r_act_up = LoadImage("button_r_act_up.png"); // Right button up activated
        public readonly static ImageSource button_r_both = LoadImage("button_r_both.png"); // Right button up and down (middle floors)
        public readonly static ImageSource button_r_down = LoadImage("button_r_down.png"); // Right button down (for top floor)
        public readonly static ImageSource button_r_up = LoadImage("button_r_up.png"); // Right button down (for bottom floor)
        public readonly static ImageSource car = LoadImage("car.png"); // Middle layer of elevator car
        public readonly static ImageSource car_back = LoadImage("car_back.png"); // Back layer of elevator car
        public readonly static ImageSource car_front = LoadImage("car_front.png"); // Front layer of elevator car
        public readonly static ImageSource car_top = LoadImage("car_top.png"); // Top details of elevator car
        public readonly static ImageSource door_left = LoadImage("door_left.png"); // Elevator left door (default closed)
        public readonly static ImageSource door_right = LoadImage("door_right.png"); // Elevator right door (default closed)
        public readonly static ImageSource door_slider = LoadImage("door_slider.png"); // Elevator right door (default closed)
        public readonly static ImageSource empty = LoadImage("empty.png"); // Empty image (for disabling layers)
        public readonly static ImageSource floor_back = LoadImage("floor_back.png"); // The base image for the floorplan
        public readonly static ImageSource floor_front = LoadImage("floor_front.png"); // Elevator right door (default closed)
        public readonly static ImageSource frame = LoadImage("frame.png"); // Elevator doorframe (with 3x 7-segment and up/down lights)
        public readonly static ImageSource service_back = LoadImage("service_back.png"); // Base image for service level above top floor
        public readonly static ImageSource seven_seg_0 = LoadImage("seven_seg_0.png"); // Seven-segment display centered at 1's place on frame - 0
        public readonly static ImageSource seven_seg_1 = LoadImage("seven_seg_1.png"); // Seven-segment display centered at 1's place on frame - 1
        public readonly static ImageSource seven_seg_2 = LoadImage("seven_seg_2.png"); // Seven-segment display centered at 1's place on frame - 2
        public readonly static ImageSource seven_seg_3 = LoadImage("seven_seg_3.png"); // Seven-segment display centered at 1's place on frame - 3
        public readonly static ImageSource seven_seg_4 = LoadImage("seven_seg_4.png"); // Seven-segment display centered at 1's place on frame - 4
        public readonly static ImageSource seven_seg_5 = LoadImage("seven_seg_5.png"); // Seven-segment display centered at 1's place on frame - 5
        public readonly static ImageSource seven_seg_6 = LoadImage("seven_seg_6.png"); // Seven-segment display centered at 1's place on frame - 6
        public readonly static ImageSource seven_seg_7 = LoadImage("seven_seg_7.png"); // Seven-segment display centered at 1's place on frame - 7
        public readonly static ImageSource seven_seg_8 = LoadImage("seven_seg_8.png"); // Seven-segment display centered at 1's place on frame - 8
        public readonly static ImageSource seven_seg_9 = LoadImage("seven_seg_9.png"); // Seven-segment display centered at 1's place on frame - 9
        public readonly static ImageSource top_act_down = LoadImage("top_act_down.png"); // Doorframe indicator for down direction
        public readonly static ImageSource top_act_up = LoadImage("top_act_up.png"); // Doorframe indicator for up direction
        public readonly static ImageSource wall = LoadImage("wall.png"); // Wall layer to obscure elevator shafts
        private static ImageSource LoadImage(string filename) // This function loads the png images that I made (in the Assets folder)
        {
            return new BitmapImage(new Uri($"Assets/{filename}", UriKind.Relative));
        }
    }
}
