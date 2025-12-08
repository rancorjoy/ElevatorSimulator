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
                if(patientceTimer / framerate >= patience) // if agent has waited too long...
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
                    if(xpos < (shaftCount - 0.5)/2) // agent closer to the left
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