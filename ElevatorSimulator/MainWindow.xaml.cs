
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

// Use namespaces for classes
using ElevatorSimulator.classes;

// Namespace for main window
namespace ElevatorSimulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Variables for mouse pan in MainScrollView
        private System.Windows.Point _dragStart;
        private bool _isDragging;

        // System variables
        int framerate = 24; // Rendering fps (simulation normalized for fps!)
        public const int MaxFloors = 128; // Simulation Maximum height
        public const int MaxShafts = 32; // Simulation Maximum width
        public const int MaxAgents = 128; // Simulation Maximum Agents
        private DispatcherTimer timer; // Timer used to render frames
        private int prev_floors = 2; // Previous floors (to detect updates)
        private int prev_shafts = 1; // Previous shafts (to detect updates)
        private int spawnTimer = 0;

        // Render variables
        public bool showWalls = true; // These control which layers are active in the view window (check boxes)
        public bool showFrames = true;
        public bool showCarFronts = true;

        // Simulation Variables
        public int capacity = 8; // elevator car capacity
        public float Car_Speed = 1.0f; // floors per second
        public float Door_Speed = 1.5f; // 1/s to open
        public int Delay_Time = 5; // measured in seconds
        public float Catch_Threshold = 0.5f; //number of floors difference needed to "catch" elevator
        ElevatorController[] elevatorControllers = new ElevatorController[MaxShafts]; // Maximum of 32 elevator shafts in this simulation - list of all current controllers
        ElevatorCar[] elevatorCars = new ElevatorCar[MaxShafts]; // Stores the graphical output for an elevator car
        ElevatorShaft[,] elevatorShafts = new ElevatorShaft[MaxShafts, MaxFloors]; // Maximum of 32 elevator shafts and 128 floors in this simulation - list of all current shafts
        AgentController[] agentControllers = new AgentController[MaxAgents]; // Maxiumum of 128 agents by default in this simulation
        Agent[] agents = new Agent[MaxAgents]; // Maxiumum of 128 agents by default in this simulation
        private bool[] upRequests = new bool[MaxFloors]; // All floors that are currently requesting an elevator to go up
        private bool[] downRequests = new bool[MaxFloors]; // All floors that are currently requesting an elevator to go down
        private bool[] pendingUpRequests = new bool[MaxFloors]; // pending array for upRequests
        private bool[] pendingDownRequests = new bool[MaxFloors]; // pending array for downRequests

        // Data Variables
        int currentAgents = 0;
        int previousAgents = 0;
        int totalTime = 0;
        int averageTime = 0;

        public MainWindow()
        {
            InitializeComponent(); // Initialize simulation/application

            // Instantiate Starting Elevator
            elevatorControllers[0] = new ElevatorController(0, this); // Elevator Controller for initial shaft
            elevatorCars[0] = new ElevatorCar(elevatorControllers[0], this); // Spawn the initial elevator car
            elevatorShafts[0, 0] = new ElevatorShaft(elevatorControllers[0], 0, this); // Shaft for first floor
            elevatorShafts[0, 1] = new ElevatorShaft(elevatorControllers[0], 1, this); // Shaft for second floor

            // Initialize Agent information
            agentBox.Text = "Count: " + 0 + " : " + 0;
            timerBox.Text = "Average Time: " +  0;

            // Set canvas the first time
            ResizeCanvas(2, 1); // Using default values

            // Timer and updates (last event in Main Section!)
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000 / framerate); // Starts timer at desired FPS
            timer.Tick += MainLoop; // Updates loop sections of code
            timer.Start(); // Starts timer
        }

        // Getters
        public bool[] UpRequests // access which floors are requesting up
        {
            get { return upRequests; }
        }
        public bool[] DownRequests // access which floors are requesting down
        {
            get { return downRequests; }
        }
        public bool getUp(int index)
        {
            return upRequests[index];
        }
        public bool getDown(int index)
        {
            return downRequests[index];
        }

        // Setters
        public void clearUp(int floor)
        {
            upRequests[floor] = false;
            pendingUpRequests[floor] = false;
        }
        public void clearDown(int floor)
        {
            downRequests[floor] = false;
            pendingDownRequests[floor] = false;
        }
        public void pressUp(int floor)
        {
            upRequests[floor] = true;
        }
        public void pressDown(int floor)
        {
            downRequests[floor] = true;
        }
        public void agentDeactivate(int timer) // when agent is no longer active, decriment active agents, increment past agents, add to total time and find average time
        {
            currentAgents -= 1;
            previousAgents += 1;
            totalTime += timer;
            averageTime = totalTime / previousAgents; // average frames an agent is alive

            // Update Agent information
            agentBox.Text = "Count: " + currentAgents + " : " + previousAgents;
            timerBox.Text = "Average Time: " + Convert.ToString(MathF.Round(Convert.ToSingle(totalTime) / (Convert.ToSingle(previousAgents) * Convert.ToSingle(framerate)),2));
        }

        // Logic for adding agents
        public void spawnAgent()
        {
            int spawn_index = findFirst(); // attempt to find a position for the agent
            if (spawn_index > -1 && shaftUpDown.Value != null && floorUpDown.Value != null) // if a position is available
            {
                agentControllers[spawn_index] = new AgentController(shaftUpDown.Value.Value, floorUpDown.Value.Value, elevatorControllers, this); // spawn an agent controller
                agents[spawn_index] = new Agent(agentControllers[spawn_index], this); // spawn the connected agent

                // Update Agent information
                currentAgents += 1;
                agentBox.Text = "Count: " + currentAgents + " : " + previousAgents;
            }
        }
        public int findFirst()
        {
            for (int a = 0; a < MaxAgents; a++) // for each possible agent position
            {
                if (agentControllers[a] == null || agentControllers[a].get_active() == false) // get the first found open position
                {
                    return a;
                }
            }
            return -1; // if there are no positions, return -1 as an error flag!
        }

        // Logic for adding and removing Floors / Shafts
        public void ResizeCanvas(int floors, int shafts)
        {
            MainCanvas.Height = (floors + 1) * 192; // Fit all floors and service floors in canvas (vertically)
            MainCanvas.Width = shafts * 192; // Fit all shafts in canvas (horizontally)
        }
        public void AddFloor(int floor)
        {
            if (floorUpDown.Value != null && shaftUpDown.Value != null)
            {
                for (int s = 0; s < shaftUpDown.Value.Value; s++)
                {
                    elevatorShafts[s, floor] = new ElevatorShaft(elevatorControllers[s], floor, this);
                }
            }
        }
        public void RemoveFloor(int floor)
        {
            if (floorUpDown.Value != null && shaftUpDown.Value != null)
            {
                upRequests[floorUpDown.Value.Value - 1] = false; // remove requests for floors that are no longer in use!!!
                downRequests[floorUpDown.Value.Value - 1] = false;
                pendingUpRequests[floorUpDown.Value.Value - 1] = false;
                pendingDownRequests[floorUpDown.Value.Value - 1] = false;
                for (int s = 0; s < shaftUpDown.Value; s++) // for all elevators
                {
                    elevatorControllers[s].validateFloor(floorUpDown.Value.Value); // validate the floor
                }
                for (int a = 0; a < MaxAgents; a++) // for all agents
                {
                    if (agentControllers[a] != null) // if the agent exists (yet)
                    {
                        if (agentControllers[a].get_active()) // if the agent is still active
                        {
                            agentControllers[a].ValidateFloor(floorUpDown.Value.Value); // validate the floor
                        }
                    }
                }
            }
        }
        public void AddShaft(int shaft)
        {
            if (floorUpDown.Value != null && shaftUpDown.Value != null)
            {
                elevatorControllers[shaft] = new ElevatorController(shaft, this); // Elevator Controller for shaft
                elevatorCars[shaft] = new ElevatorCar(elevatorControllers[shaft], this); // Elevator car for the controller
                for (int f = 0; f < floorUpDown.Value.Value; f++)
                {
                    elevatorShafts[shaft, f] = new ElevatorShaft(elevatorControllers[shaft], f, this);
                }
            }
        }
        public void RemoveShaft(int shaft)
        {
            if (floorUpDown.Value != null && shaftUpDown.Value != null)
            {
                elevatorControllers[shaft].deactivate(); // Deactivate elevator!
                for (int a = 0; a < MaxAgents; a++)
                {
                    if (agentControllers[a] != null)
                    {
                        agentControllers[a].ValidateShaft(shaftUpDown.Value.Value);
                    }
                }
            }
        }

        // Rendering Behavior
        public void DrawImage(ImageSource src, double x, double y, double width = 192, double height = 192)
        {
            Image img = new Image
            {
                Source = src,
                Width = width,   // Force image width
                Height = height  // Force image height
            };

            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor); // ensures anti-aliasing is turned off - for pixel sharpness!

            Canvas.SetLeft(img, x);
            Canvas.SetTop(img, y);

            MainCanvas.Children.Add(img);
        }
        private void Update_ShaftBacks()
        {
            if (floorUpDown.Value != null && shaftUpDown.Value != null)
            {
                for (int s = 0; s < shaftUpDown.Value.Value; s++)
                {
                    for (int f = 0; f < floorUpDown.Value.Value; f++)
                    {
                        elevatorShafts[s, f].RenderBack();
                    }
                    if (showWalls == false) // if walls are not shown -> show service level (tops)
                    {
                        int x_pos = s * 192;
                        int y_pos = 0;
                        DrawImage(Images.service_back, x_pos, y_pos);
                    }
                }
            }
        }
        private void Update_CarBacks()
        {
            if (shaftUpDown.Value != null)
            {
                for (int s = 0; s < shaftUpDown.Value.Value; s++)
                {
                    elevatorCars[s].RenderBack();
                }
            }
        }
        private void Update_CarFronts()
        {
            if (shaftUpDown.Value != null)
            {
                for (int s = 0; s < shaftUpDown.Value.Value; s++)
                {
                    elevatorCars[s].RenderFront();
                    elevatorCars[s].RenderDoors();
                }
            }
        }
        private void Update_ShaftFronts()
        {
            if (floorUpDown.Value != null && shaftUpDown.Value != null)
            {
                for (int f = 0; f < floorUpDown.Value.Value; f++)
                {
                    for (int s = 0; s < shaftUpDown.Value.Value; s++)
                    {
                        elevatorShafts[s, f].RenderFront();
                    }
                }
            }
        }
        private void Update_ShaftDisplays()
        {
            if (floorUpDown.Value != null && shaftUpDown.Value != null)
            {
                for (int f = 0; f < floorUpDown.Value.Value; f++)
                {
                    for (int s = 0; s < shaftUpDown.Value.Value; s++)
                    {
                        elevatorShafts[s, f].RenderButtons();
                        elevatorShafts[s, f].RenderDisplay();
                    }
                }
            }
        }

        // Window Movement and Zoom Behavior
        private void Scroll_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Get mouse position relative to ScrollViewer
            System.Windows.Point pos = e.GetPosition(MainScrollViewer);

            // Only start dragging if mouse is inside ScrollViewer viewport (ignoring scrollbar area)
            if (pos.X < MainScrollViewer.ViewportWidth && pos.Y < MainScrollViewer.ViewportHeight)
            {
                _isDragging = true;
                _dragStart = pos;
                MainScrollViewer.CaptureMouse();
                MainScrollViewer.Cursor = Cursors.Hand;
                e.Handled = true;
            }
        } //Detects left click for view pan (MainScrollView)
        private void Scroll_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                System.Windows.Point current = e.GetPosition(MainScrollViewer);
                double dx = current.X - _dragStart.X;
                double dy = current.Y - _dragStart.Y;

                MainScrollViewer.ScrollToHorizontalOffset(MainScrollViewer.HorizontalOffset - dx);
                MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset - dy);

                _dragStart = current;
            }
        } //Detects current mouse drag trajectory (MainScrollView)
        private void Scroll_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            MainScrollViewer.ReleaseMouseCapture();
            MainScrollViewer.Cursor = Cursors.Arrow;
        } //Detects end of left click from view pan (MainScrollView)
        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;

            double newScaleX = MainCanvasScale.ScaleX * zoomFactor;
            double newScaleY = MainCanvasScale.ScaleY * zoomFactor;

            // Clamp Zoom
            double minZoom = 0.25;
            double maxZoom = 4.0;
            newScaleX = Math.Clamp(newScaleX, minZoom, maxZoom);
            newScaleY = Math.Clamp(newScaleY, minZoom, maxZoom);

            MainCanvasScale.ScaleX = newScaleX;
            MainCanvasScale.ScaleY = newScaleY;

            e.Handled = true;
        }//Detects mouse wheel for zooming (MainScrollView)

        // Updating section (framerate FPS)
        private void MainLoop(object sender, EventArgs e)
        {
            // Clear last frame
            MainCanvas.Children.Clear();

            // Prevent impossible requests incase they are set
            if (floorUpDown.Value != null)
            {
                downRequests[0] = false;
                upRequests[floorUpDown.Value.Value - 1] = false;
            }

            // Detect Canvas Resizing Event
            if (floorUpDown.Value != null && shaftUpDown.Value != null) // If the two dimensions are not null...
            {
                if (prev_floors != floorUpDown.Value || prev_shafts != shaftUpDown.Value) // User has changed simulation dimensions!
                {
                    ResizeCanvas(floorUpDown.Value.Value, shaftUpDown.Value.Value); // Resize the canvas to fit the new dimensions
                }
            }
            else // If a dimension value becomes null - reset to default values
            {
                floorUpDown.Value = 2;
                shaftUpDown.Value = 1;
            }

            //Update simulation dimensions!
            if (floorUpDown.Value != null && shaftUpDown.Value != null) // If the two dimensions are not null...
            {
                if (prev_floors != floorUpDown.Value.Value)
                {
                    if (prev_floors < floorUpDown.Value.Value)
                    { // Add floors
                        for (int f = prev_floors; f < floorUpDown.Value.Value; f++)
                            AddFloor(f);
                    }
                    else
                    { // Remove floors
                        for (int f = prev_floors - 1; f >= floorUpDown.Value.Value; f--)
                            RemoveFloor(f);
                    }
                }
                if (prev_shafts != shaftUpDown.Value.Value)
                {
                    if (prev_shafts < shaftUpDown.Value.Value)
                    { // Add shafts
                        for (int s = prev_shafts; s < shaftUpDown.Value.Value; s++)
                            AddShaft(s);
                    }
                    else
                    { // Remove shafts
                        for (int s = prev_shafts - 1; s >= shaftUpDown.Value.Value; s--)
                            RemoveShaft(s);
                    }
                }
            }

            //Update elevator controllers
            if (floorUpDown.Value != null && shaftUpDown.Value != null)
            {
                for (int s = 0; s < shaftUpDown.Value.Value; s++) // for each elevator controller
                {
                    elevatorControllers[s].updateKinematicVars(Delay_Time, Car_Speed, Door_Speed, Catch_Threshold, framerate, floorUpDown.Value.Value, capacity); // pass simulation variables to cars
                    elevatorControllers[s].updateState(); // update controller state machine
                    elevatorControllers[s].updateTimer(); // update car timers
                    elevatorControllers[s].updateDoorPos(); // update door positions
                    elevatorControllers[s].updateCarPos(); // update car positions
                    elevatorControllers[s].updateCarDirection(); // ensure car switches direction at top and bottom floors
                    elevatorControllers[s].clearFlags(); // reset openPressed and closePressed
                }
            }

            //Update agent controllers
            for (int a = 0; a < MaxAgents; a++) // for all agents
            {
                if (agentControllers[a] != null) // if the agent exists (yet)
                {
                    if (agentControllers[a].get_active() && shaftUpDown.Value != null) // if the agent is still active
                    {
                        agentControllers[a].UpdateState(shaftUpDown.Value.Value, framerate); // Update agent controller states
                        agentControllers[a].UpdatePos(shaftUpDown.Value.Value, framerate); // Update agent positions
                    }
                }
            }

            // Render everything again!
            Update_ShaftBacks();
            Update_CarBacks();
            //Render boarded agents
            for (int a = 0; a < MaxAgents; a++) // for all agents
            {
                if (agentControllers[a] != null) // if the agent exists (yet)
                {
                    if (agentControllers[a].get_active() && agentControllers[a].get_boarded()) // if the agent is still active and in its elevator
                    {
                        agents[a].RenderAgent();
                    }
                }
            }
            Update_CarFronts();
            Update_ShaftFronts();
            Update_ShaftDisplays();
            //Render non-boarded agents
            for (int a = 0; a < MaxAgents; a++) // for all agents
            {
                if (agentControllers[a] != null) // if the agent exists (yet)
                {
                    if (agentControllers[a].get_active() && agentControllers[a].get_boarded() == false) // if the agent is still active and NOT in its elevator
                    {
                        agents[a].RenderAgent();
                    }
                }
            }

            //Spawn Agents
            if (spawnUpDown.Value != null) // if there is a selected spawn rate
            {
                if (spawnUpDown.Value.Value != 0) // if the spawn timer is NOT 0 (spawning allowed)
                {
                    if(spawnBox.SelectedIndex == 0) // agents per second (faster)
                    {
                        if (spawnTimer >= framerate / spawnUpDown.Value.Value)
                        {
                            spawnAgent();
                            spawnTimer = 0; // reset spawn timer after spawning an agent
                        }
                    }
                    if(spawnBox.SelectedIndex == 1) // seconds per agent (slower)
                    {
                        if (spawnTimer >= spawnUpDown.Value.Value * framerate)
                        {
                            spawnAgent();
                            spawnTimer = 0; // reset spawn timer after spawning an agent
                        }
                    }
                    spawnTimer += 1; // increment the spawn timer
                }
            }

            //Manage Elevators using user selected algorithm
            if (SCANbox.SelectedItem is ComboBoxItem item)
            {

                if (SCANbox.SelectedIndex == 0) greedyDISK(); // minimize idle use
                else if (SCANbox.SelectedIndex == 2) aggressiveDISK(); // maximize idle use
                else if (SCANbox.SelectedIndex == 1) balancedDISK(); // maximize efficiency
            }

            // Update settings
            if (showFrameBox.IsChecked != null) showFrames = showFrameBox.IsChecked.Value;
            if (showWallsBox.IsChecked != null) showWalls = showWallsBox.IsChecked.Value;
            if (showCarsBox.IsChecked != null) showCarFronts = showCarsBox.IsChecked.Value;

            // Last update! - set new previous values for dimensions
            if (floorUpDown.Value != null && shaftUpDown.Value != null)
            {
                prev_floors = floorUpDown.Value.Value;
                prev_shafts = shaftUpDown.Value.Value;
            }
        }

        // Elevator Algorithm (DISK algorithm) - updates each frame - tries to minimize idle usage!
        private void greedyDISK()
        {
            clearJobs(); // This clears jobs that have just been satisfied
            if (shaftUpDown.Value != null && floorUpDown.Value != null)
            {
                for (int r = 0; r < floorUpDown.Value.Value; r++) // for each possible request r...
                {
                    if (upRequests[r] == true && pendingUpRequests[r] == false) // there is a waiting up request of floor r
                    {
                        for (int s = 0; s < shaftUpDown.Value.Value; s++) // for all valid elevator shafts...
                        {
                            // PASS 0 - FOR FIRST FLOOR GOING UP
                            if (r == 0 && elevatorControllers[s].get_floor() == 0 && elevatorControllers[s].get_dir() == 2) // can this non-idle elevator take request?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, true);
                                    break; // only assign 1 car
                                }
                            }
                            // PASS 1 - MOVING CARS
                            else if (elevatorControllers[s].get_pos() < Convert.ToSingle(r) - Catch_Threshold && elevatorControllers[s].get_dir() == 1) // can this non-idle elevator take request?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, true);
                                    break; // only assign 1 car
                                }
                            }
                            // PASS 2 - IDLE CARS
                            else if (elevatorControllers[s].get_dir() == 2) // can this idle elevator take request if none-other is available?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, true);
                                    break; // only assign 1 car
                                }
                            }
                        }
                    }
                    else if (downRequests[r] == true && pendingDownRequests[r] == false) // there is a waiting down request of floor r
                    {
                        for (int s = 0; s < shaftUpDown.Value.Value; s++) // for all valid elevator shafts...
                        {
                            // PASS 1 - MOVING CARS
                            if (elevatorControllers[s].get_pos() > Convert.ToSingle(r) + Catch_Threshold && elevatorControllers[s].get_dir() == 0) // can this elevator take request?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, false);
                                    break; // only assign 1 car
                                }
                            }
                            // PASS 2 - IDLE CARS
                            else if (elevatorControllers[s].get_dir() == 2) // can this elevator take request?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, false);
                                    break; // only assign 1 car
                                }
                            }
                        }
                    }
                }
            }
        }
        // Elevator Algorithm (DISK algorithm) - updates each frame - tries to maximize idle usage!
        private void aggressiveDISK()
        {
            clearJobs(); // This clears jobs that have just been satisfied
            if (shaftUpDown.Value != null && floorUpDown.Value != null)
            {
                for (int r = 0; r < floorUpDown.Value.Value; r++) // for each possible request r...
                {
                    if (upRequests[r] == true && pendingUpRequests[r] == false) // there is a waiting up request of floor r
                    {
                        for (int s = 0; s < shaftUpDown.Value.Value; s++) // for all valid elevator shafts...
                        {
                            // PASS 0 - FOR FIRST FLOOR GOING UP
                            if (r == 0 && elevatorControllers[s].get_floor() == 0 && elevatorControllers[s].get_dir() == 2) // can this non-idle elevator take request?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, true);
                                    break; // only assign 1 car
                                }
                            }
                            // PASS 1 - IDLE CARS
                            else if (elevatorControllers[s].get_dir() == 2) // can this idle elevator take request if none-other is available?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, true);
                                    break; // only assign 1 car
                                }
                            }
                            // PASS 2 - MOVING CARS
                            else if (elevatorControllers[s].get_pos() < Convert.ToSingle(r) - Catch_Threshold && elevatorControllers[s].get_dir() == 1) // can this non-idle elevator take request?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, true);
                                    break; // only assign 1 car
                                }
                            }
                        }
                    }
                    else if (downRequests[r] == true && pendingDownRequests[r] == false) // there is a waiting down request of floor r
                    {
                        for (int s = 0; s < shaftUpDown.Value.Value; s++) // for all valid elevator shafts...
                        {
                            // PASS 1 - IDLE CARS
                            if (elevatorControllers[s].get_dir() == 2) // can this elevator take request?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, false);
                                    break; // only assign 1 car
                                }
                            }
                            // PASS 2 - MOVING CARS
                            else if (elevatorControllers[s].get_pos() > Convert.ToSingle(r) + Catch_Threshold && elevatorControllers[s].get_dir() == 0) // can this elevator take request?
                            {
                                if (elevatorControllers[s].get_full() == false) // ensure the controller is not full
                                {
                                    assignCar(s, r, false);
                                    break; // only assign 1 car
                                }
                            }
                        }
                    }
                }
            }
        }
        // Elevator Algorithm (DISK algorithm) - updates each frame - tries to optimize for car position
        private void balancedDISK()
        {
            clearJobs(); // This clears jobs that have just been satisfied
            if (shaftUpDown.Value != null && floorUpDown.Value != null)
            {
                for (int r = 0; r < floorUpDown.Value.Value; r++) // for each possible request r...
                {
                    if (upRequests[r] == true && pendingUpRequests[r] == false) // there is a waiting up request of floor r
                    {
                        for (int s = 0; s < shaftUpDown.Value.Value; s++) // for all valid elevator shafts...
                        {
                            int bestShaft = -1; // the best candidate shaft
                            float currentScore = 1000.0f; // set to something impossibly high

                            if (elevatorControllers[s].get_pos() < Convert.ToSingle(r) - Catch_Threshold && elevatorControllers[s].get_dir() == 0) // is this moving elevator a candidate?
                            {
                                float candScore = Convert.ToSingle(r) - elevatorControllers[s].get_pos(); // how close is the candidate?
                                if(candScore < currentScore) // if this is the closest shaft...
                                {
                                    currentScore = candScore; // update the best score
                                    bestShaft = s; // update the best shaft
                                }
                            }
                            if (elevatorControllers[s].get_dir() == 2) // is this candidate idle?
                            {
                                float candScore = Convert.ToSingle(r) - elevatorControllers[s].get_pos() + 0.5f; // how close is the candidate? (add 0.5f so moving cars win ties)
                                if (candScore < currentScore) // if this is the closest shaft...
                                {
                                    currentScore = candScore; // update the best score
                                    bestShaft = s; // update the best shaft
                                }
                            }
                            if(bestShaft > -1) assignCar(bestShaft, r, true); // assign the best car if there is one
                        }
                    }
                    else if (downRequests[r] == true && pendingDownRequests[r] == false) // there is a waiting down request of floor r
                    {
                        for (int s = 0; s < shaftUpDown.Value.Value; s++) // for all valid elevator shafts...
                        {
                            int bestShaft = -1; // the best candidate shaft
                            float currentScore = 1000.0f; // set to something impossibly high

                            if (elevatorControllers[s].get_pos() < Convert.ToSingle(r) + Catch_Threshold && elevatorControllers[s].get_dir() == 0) // is this moving elevator a candidate?
                            {
                                float candScore = Convert.ToSingle(r) - elevatorControllers[s].get_pos(); // how close is the candidate?
                                if (candScore < currentScore) // if this is the closest shaft...
                                {
                                    currentScore = candScore; // update the best score
                                    bestShaft = s; // update the best shaft
                                }
                            }
                            if (elevatorControllers[s].get_dir() == 2) // is this candidate idle?
                            {
                                float candScore = Convert.ToSingle(r) - elevatorControllers[s].get_pos() + 0.5f; // how close is the candidate? (add 0.5f so moving cars win ties)
                                if (candScore < currentScore) // if this is the closest shaft...
                                {
                                    currentScore = candScore; // update the best score
                                    bestShaft = s; // update the best shaft
                                }
                            }
                            if (bestShaft > -1) assignCar(bestShaft, r, true); // assign the best car if there is one
                        }
                    }
                }
            }
        }
        private void clearJobs()
        {
            if (shaftUpDown.Value != null) // if there is a current number of shafts (there should be)
            {
                int shaftCount = shaftUpDown.Value.Value; // define the number of shafts

                for (int s = 0; s < shaftCount; s++) // for all elevators
                {
                    if (elevatorControllers[s].get_deactivated() == false) // the elevator is still active
                    {
                        // Doors fully open? Then this car is servicing its current floor now.
                        if (elevatorControllers[s].get_doorPos() == 1.0f)
                        {

                            // If the elevator is going up - clear the up request on this floor
                            if (elevatorControllers[s].get_dir() == 1)
                            {
                                upRequests[elevatorControllers[s].get_floor()] = false;
                                pendingUpRequests[elevatorControllers[s].get_floor()] = false;
                            }
                            // If the elevator is going down - clear the up request on this floor
                            if (elevatorControllers[s].get_dir() == 0)
                            {
                                downRequests[elevatorControllers[s].get_floor()] = false;
                                pendingDownRequests[elevatorControllers[s].get_floor()] = false;
                            }
                            // if elevator dir == 2 (idle) DO NOT CLEAR FLOOR REQUEST! this will be done when agent presses button and sets direction!
                        }
                    }
                }
            }
        }
        private void assignCar(int shaft, int floor, bool up) // This assigns a job to an elevator and keeps track of which jobs were previously assigned
        {
            elevatorControllers[shaft].hitFloor(floor); // select an elevator
            if (up) // if this elevator is going up
            {
                pendingUpRequests[floor] = true; // this floor is now assigned up
                Console.WriteLine("Elevator " + shaft + " has been assigned to floor " + floor + " : Up");
                for (int a = 0; a < MaxAgents; a++) // for all agents
                {
                    if (agentControllers[a] != null) // if the agent exists (yet)
                    {
                        if (agentControllers[a].get_active() && shaftUpDown.Value != null && floorUpDown.Value != null) // if the agent is still active
                        {
                            agentControllers[a].UpdateWaitShaft(shaftUpDown.Value.Value, floorUpDown.Value.Value); // update the wait shaft for the agent
                        }
                    }
                }
            }
            else // if this elevator is going down
            {
                pendingDownRequests[floor] = true; // this floor is now assigned down
                Console.WriteLine("Elevator " + shaft + " has been assigned to floor " + floor + " : Down");
                for (int a = 0; a < MaxAgents; a++) // for all agents
                {
                    if (agentControllers[a] != null) // if the agent exists (yet)
                    {
                        if (agentControllers[a].get_active() && shaftUpDown.Value != null && floorUpDown.Value != null) // if the agent is still active
                        {
                            agentControllers[a].UpdateWaitShaft(shaftUpDown.Value.Value, floorUpDown.Value.Value); // update the wait shaft for the agent
                        }
                    }
                }
            }
        }

        // Functions for Manual Input Click Buttons
        private void Manual_Send(object sender, RoutedEventArgs e)
        {
            if (manfUpDown.Value != null && mancUpDown.Value != null && floorUpDown.Value != null && shaftUpDown.Value != null) // if there are valid values in the manual numeric inputs
            {
                if (manfUpDown.Value.Value <= floorUpDown.Value.Value && mancUpDown.Value.Value <= shaftUpDown.Value.Value)
                {
                    elevatorControllers[mancUpDown.Value.Value - 1].hitFloor(manfUpDown.Value.Value - 1); // hit the requested floor inside of the requested elevator car
                }
            }
        }
        private void Manual_Up(object sender, RoutedEventArgs e)
        {
            if (manfUpDown.Value != null && mancUpDown.Value != null && floorUpDown.Value != null) // if there are valid values in the manual numeric inputs
            {
                if (manfUpDown.Value.Value <= floorUpDown.Value.Value)
                {
                    upRequests[manfUpDown.Value.Value - 1] = true; // add the selected floor to upRequests (someone on that floor wants to go up)
                }
            }
        }
        private void Manual_Down(object sender, RoutedEventArgs e)
        {
            if (manfUpDown.Value != null && mancUpDown.Value != null && floorUpDown.Value != null) // if there are valid values in the manual numeric inputs
            {
                if (manfUpDown.Value.Value <= floorUpDown.Value.Value)
                {
                    downRequests[manfUpDown.Value.Value - 1] = true; // add the selected floor to downRequests (someone on that floor wants to go down)
                }
            }
        }
        private void Manual_Open(object sender, RoutedEventArgs e)
        {
            if (mancUpDown.Value != null) // if there are valid values in the manual numeric inputs
            {
                elevatorControllers[mancUpDown.Value.Value - 1].openPress(); // open press the selected elevator
            }
        }
        private void Manual_Close(object sender, RoutedEventArgs e)
        {
            if (mancUpDown.Value != null) // if there are valid values in the manual numeric inputs
            {
                elevatorControllers[mancUpDown.Value.Value - 1].closePress(); // open press the selected elevator
            }
        }
    }
}