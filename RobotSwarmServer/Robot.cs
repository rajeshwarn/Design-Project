﻿/*
 * 
 * Robot
 * Is used to define the robots. This includes all parameters that describes the 
 * state of the robot. It also includes the functions used by the simulation to 
 * calculate the behaviour of each robot by the use of an active control strategy.
 * 
 */
using System;
using System.Collections.Generic;

using System.IO;

using AForge;
using RobotSwarmServer.Control_Strategies;


using RobotSwarmServer.Control_Strategies.Strategies;

using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobotSwarmServer
{
    public partial class Robot
    {
        // -------------------- Constants --------------------
        private const int numberOfSavedPositions = 3;

        //$$$$$Changes/Additions for RC car$$$$$//
        private const int numberOfSavedDelta = 1;
        private const int lR = 32; // length between middle of rear wheel and CG in pixels (3.5 cm = 3.5*9 pixels)
        private const int lF = 27;  // length between middle of front wheel and CG in pixels (3 cm=3*9 pixels)
        //$$$$$$$$$$//

        // ----------------- Robot variables -----------------
        private int ID;
        private DoublePoint position = new DoublePoint(0, 0);
        private List<DoublePoint> oldPositions = new List<DoublePoint>();
        private List<int> oldTimes = new List<int>();
        private List<double> deltaList = new List<double>();
        private DoublePoint heading = new DoublePoint(0, 0);
        private DoublePoint referenceHeading = new DoublePoint(0, 0);
        private double speed;
        private double referenceSpeed;
        private int[] motorSignals = new int[2]; // L,R
        private bool detected = false;
        private bool blocked = false;
        private int previousTime = Environment.TickCount;
        private IntPoint[] cornerArray = new IntPoint[4];

        double finalPositionX = 0;
        double finalPositionY = 0;
        private bool blocktime = false;

        int[] tempMotorSignals = new int[2];

        public ControlStrategy currentStrategy;

        // ----------------- Environment variables -----------------

        private List<Robot> neighbors = new List<Robot>();

        // -------------------- Constructor ------------------------

        /// <summary>Robot constructor</summary>
        public Robot(int newID)
        {
            setID(newID);

        }

        // ----------------- ID methods -----------------
        /// <summary>Set robots ID.</summary>
        public void setID(int newID)
        {
            ID = newID;
        }

        /// <summary>Get robots ID.</summary>
        public int getID()
        {
            return ID;
        }

        // ---------------- Detection methods -------------
        public void setDetected(bool newDetected)
        {
            detected = newDetected;
        }

        public bool getDetected()
        {
            return detected;
        }

        // ----------------- Position methods -----------------
        /// <summary>Set robots current position X,Y.</summary>
        public void setPosition(DoublePoint newPosition)
        {
            if (oldPositions.Count == numberOfSavedPositions)
            {
                oldPositions.RemoveAt(0);
                oldTimes.RemoveAt(0);
            }
            oldPositions.Add(position);
            oldTimes.Add(Environment.TickCount - previousTime);
            previousTime = Environment.TickCount;
            position = newPosition;
        }

        /// <summary>Get robots current position X,Y.</summary>
        public DoublePoint getPosition()
        {
            return position;
        }

        //$$$$$Changes/Additions for RC car$$$$$//
        // ----------------- delta methods -----------------
        /// <summary>Saving delta in list.</summary>
        public void saveDelta(double delta)
        {
            if (deltaList.Count == numberOfSavedDelta)
            {
                deltaList.RemoveAt(0);
            }
            deltaList.Add(delta);
        }
        //$$$$$$$$$$//

        // ----------------- Heading methods -----------------
        /// <summary>Set robots current heading.</summary>
        public void setHeading(DoublePoint newHeading)
        {
            // Normalize.
            heading = newHeading / newHeading.EuclideanNorm();
        }

        /// <summary>Get robots current heading.</summary>
        public DoublePoint getHeading()
        {
            return heading;
        }

        // ----------------- CornerArray methods -----------------
        /// <summary>Get robots current heading.</summary>
        public void setCornerArray(IntPoint[] cornerArray)
        {
            cornerArray.CopyTo(this.cornerArray, 0);
        }

        /// <summary>Get robots current heading.</summary>
        public IntPoint[] getCornerArray()
        {
            return this.cornerArray;
        }

        // ----------------- Speed methods -----------------
        /// <summary>Set (calculate) robots current speed.</summary>
        public void calculateSpeed()
        {
            double tempDistance = 0;
            int tempTime = 0;

            if (oldTimes.Count != 1)
            {
                for (int i = 0; i < oldPositions.Count - 1; i++)
                {
                    tempDistance += oldPositions[i].DistanceTo(oldPositions[i + 1]);
                    tempTime += oldTimes[i] + oldTimes[i + 1];
                }
            }
            else
            {
                tempTime = oldTimes[0];
            }

            speed = 1000 * tempDistance / tempTime;
        }

        /// <summary>Get robots current speed.</summary>
        public double getSpeed()
        {
            return speed;
        }

        // ----------------- Neighbor methods -----------------
        /// <summary>Set robots current neighbors.</summary>
        public void setNeighbors(List<Robot> newNeighbors)
        {
            neighbors.Clear();
            neighbors.AddRange(newNeighbors);
        }

        /// <summary>Get robots current neighbors.</summary>
        public List<Robot> getNeighbors()
        {
            return neighbors;
        }

        // ----------------- Motor signal methods -----------------
        /// <summary>Set robots current motor signals [L,R].</summary>
        public void setMotorSignals(int[] newMotorSignals)
        {
            motorSignals = newMotorSignals;
        }

        /// <summary>Get robots current motor signal [L,R].</summary>
        public int[] getMotorSignals()
        {
            if (getDetected())
            {
                return motorSignals;
            }
            else
            {
                //uncomment below lines if using m3pi robts
                //return new int[2] { 0, 0 };   
                //return motorSignals;

                //$$$$$Changes/Additions made for RC cars$$$$$//
                return new int[2] { Program.neutralSpeed, motorSignals[1] };
                //$$$$$$$$$$//
            }
        }

        // ----------------- Tick method -----------------
        /// <summary> Make a tick with robot brain.</summary>
        public void updateRobot()
        {
            if (currentStrategy != null)
            {
                currentStrategy.calculateNextMove(position, speed, heading, neighbors, out referenceSpeed, out referenceHeading);
            }
            else
            {
                setStrategy(new StandStill());
            }

            // Run collision avoidance code to check if robot is blocked.
            blocked = false;
            blocked = isBlocked();

            if (blocked)
            {
                //setMotorSignals(new int[2] { 0, 0 }); //uncomment if using m3pi robots

                //$$$$$Changes/Additions for RC car$$$$$//
                setMotorSignals(new int[2] { Program.neutralSpeed, motorSignals[1] });
                if (neighbors[0].getID() != 0)  //The updateRobot() function is called separately for each robot; therefore a new obstacle avoidance strategy needs to be set only for glyph 0.                          
                 {
                     if (!blocktime) //The first time obstacle is detected, calculate the final point until which obstacle avoidance strategy needs to be valid.
                     {
                         finalPositionX = 2 * neighbors[0].getPosition().X - position.X;
                         finalPositionY = 2 * neighbors[0].getPosition().Y - position.Y;
                         blocktime = true;
                     }
                     int nrPointsObsAvoid = 15;
                     DoublePoint positionObst = neighbors[0].getPosition();
                     
                      if (Math.Abs(finalPositionX - position.X) > 300 || Math.Abs(finalPositionY - position.Y) > 300)
                      {
                          setStrategy(new FollowPath("Avoid Obstacle", FollowPath.createEllipsePoints(200,100, positionObst, nrPointsObsAvoid)));  //obstacle avoidance path.
                      }
                      else
                      {
                          currentStrategy = Program.strategyList.ElementAt(Program.activeStrategyId);   //After completing half-circle, the robot should continue on it's previous path as set in GUI.
                      }
                  }
                  else
                  {
                      setStrategy(new StandStill());  //For the obstacle, keep the strategy as StandStill.   
                 }     
                //$$$$$$$$$$//
            }
            //else  //uncomment if using m3pi robots
            //{ //uncomment if using m3pi robots
              //  setMotorSignals(controller(speed, heading, referenceSpeed, referenceHeading));    //uncomment if using m3pi robots
                setMotorSignals(controller(speed, heading, referenceHeading));
               
            //} //uncomment if using m3pi robots
        }
            // ----------------- Collision Avoidance method -----------------

        /// <summary> Method used to determine if robot is blocked </summary>

        public bool isBlocked()
        {
            bool block = false;

            double angle = Math.Atan2(heading.Y, heading.X);
            double angleLeft = angle - Math.PI / 4;
            double angleRight = angle + Math.PI / 4;

            if (Math.Abs(angleLeft) > Math.PI)
            {
                angleLeft = angleLeft - Math.Sign(angleLeft) * 2 * Math.PI;
            }

            if (Math.Abs(angleRight) > Math.PI)
            {
                angleRight = angleRight - Math.Sign(angleRight) * 2 * Math.PI;
            }

            DoublePoint HeadingCornerLeft = new DoublePoint(Math.Cos(angleLeft), Math.Sin(angleLeft));
            DoublePoint HeadingCornerRight = new DoublePoint(Math.Cos(angleRight), Math.Sin(angleRight));

            DoublePoint EdgePointLeft = position + HeadingCornerLeft * Program.robotRadius;
            DoublePoint EdgePointRight = position + HeadingCornerRight * Program.robotRadius;
            DoublePoint EdgePointMiddle = position + heading * Program.robotRadius;

            foreach (Robot neighbor in neighbors)
            {
                if (EdgePointLeft.DistanceTo(neighbor.getPosition()) < Program.robotRadius || EdgePointRight.DistanceTo(neighbor.getPosition()) < Program.robotRadius || EdgePointMiddle.DistanceTo(neighbor.getPosition()) < Program.robotRadius)
                {
                    block = true;
                }
            }
            return block;
        }
        
        //$$$$$Changes/Additions made for RC cars$$$$$//
        // ----------------- Controller method -----------------NOT DONE YET
        /// <summary>RC car controller, bicycle model</summary>
        private int[] controller(double speed, DoublePoint heading, DoublePoint referenceHeading)// , double referenceSpeed, DoublePoint referenceHeading)
        {
            double psi;
            double theta;
            double delta;
            double beta;
            double angleControl;
            string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // calculate psi
            psi = Math.Atan2(heading.Y, heading.X);   // angle in radians -pi<=psi<=pi
            // theta does it matter if this is in different directions????
            theta = Math.Atan2(referenceHeading.Y, referenceHeading.X); // angle in radians -pi<=theta<=pi
            if (Double.IsNaN(psi) || Double.IsNaN(theta))
            {
                delta = double.NaN;
            }
            else
            {
                beta = theta - psi;
                if (Math.Abs(beta) > Math.PI)
                {
                    beta = beta - Math.Sign(beta) * 2 * Math.PI;
                }


                // write to textfile TEST
                // Set a variable to the My Documents path. 
               

                FileStream fs1 = new FileStream(mydocpath + @"\beta.txt", FileMode.OpenOrCreate, FileAccess.Write);
                StreamWriter writer = new StreamWriter(fs1);
                writer.BaseStream.Seek(0, SeekOrigin.End);
                writer.WriteLine(beta);
                writer.Close();



                double deltaTemp = Math.Atan((Math.Tan(beta) * (lF + lR)) / lR);  // angle in radians -pi/2<=delta<=pi/2
                if (Double.IsNaN(deltaTemp))
                {
                    delta = double.NaN;
                }
                else
                {
                    delta = deltaTemp;
                }
            }

            // write to textfile TEST
            // Set a variable to the My Documents path. 
            // string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            FileStream fs2 = new FileStream(mydocpath + @"\delta.txt", FileMode.OpenOrCreate, FileAccess.Write);
            StreamWriter writer2 = new StreamWriter(fs2);
            writer2.BaseStream.Seek(0, SeekOrigin.End);
            writer2.WriteLine(delta);
            writer2.Close();



                       //##########################################################################################################
            // ------------------------------DO WE NEED TO LOOK IF ANGLES CHANGES SIGN?---------------------------------

            // converting angle to voltage depending on right or left steering
            if (double.IsNaN(delta))
            {
                tempMotorSignals[0] = (int)Program.neutralSpeed;
                tempMotorSignals[1] = (int)Program.neutralSteer;
                Console.WriteLine("delta is NaN");
                return tempMotorSignals;
            }
            else if (delta == 0)
            {
                angleControl = Program.neutralSteer;
                Console.WriteLine("delta is 0");
                //angleControl = neutralSteer;
            }
            else
            {
                double angleVoltage;
                //angleVoltage = (delta + 0.2026) / 0.1413; //car2
                angleVoltage = (delta + 0.6372) / 0.4971;   //car1
                Console.WriteLine("delta changes");
                // convert to PWM
                angleControl = angleVoltage * 51;
            }

            // check if value is in the right range 
            if (angleControl < Program.minSteer)
            {
                angleControl = Program.minSteer;
            }
            else if (angleControl > Program.maxSteer)
            {
                angleControl = Program.maxSteer;
            }

            double speedControl = Program.testSpeed;
            // Saturate speed contol if larger than max allowed value
            if (speedControl > Program.maxSpeed)
            {
                speedControl = Program.maxSpeed;
            }
            else if (speedControl < Program.minSpeed)
            {
                speedControl = Program.minSpeed;
            }

            tempMotorSignals[0] = (int)speedControl;
            tempMotorSignals[1] = (int)angleControl;
            // Fix contol signals if smaller/larger than min/max allowed value

            return tempMotorSignals;
        }
        //$$$$$$$$$$//


        //Uncomment the below section if using m3pi robots//
        /// <summary>Robot speed and angle control.</summary>
        /*private int[] controller(double speed, DoublePoint heading, double referenceSpeed, DoublePoint referenceHeading)
        {
            int[] tempMotorSignals = new int[2];
            const int zeroControl = 2;
            const int minControl = 25;
            const int maxControl = 127;
            const int maxSpeedControl = 70;

            // Regulator P constants.
            double pAngle = 20;
            double pSpeed = 1.2;

            // Calculate angels from vectors.
            double angle = Math.Atan2(heading.Y, heading.X);
            double referenceAngle = Math.Atan2(referenceHeading.Y, referenceHeading.X);

            // Calculate errors and correct for if angle changes sign.
            double angleError = referenceAngle - angle;
            if (Math.Abs(angleError) > Math.PI)
            {
                angleError = angleError - Math.Sign(angleError) * 2 * Math.PI;
            }
            //double speedError = referenceSpeed - speed // NO REAL SPEED CONTROLLER!!!
            double speedError = referenceSpeed;

            double angleControl = pAngle * angleError;
            double speedControl = pSpeed * speedError;

            // Saturate speed contol if larger than max allowed value
            if (Math.Abs(speedControl) > maxSpeedControl)
            {
                speedControl = Math.Sign(speedControl) * maxSpeedControl;
            }

            // Merge angle and speed part of control signal.
            tempMotorSignals[0] = (int)(speedControl + angleControl / 2);
            tempMotorSignals[1] = (int)(speedControl - angleControl / 2);

            // Fix contol signals if smaller/larger than min/max allowed value
            if (Math.Abs(tempMotorSignals[0]) < minControl && Math.Abs(tempMotorSignals[0]) > zeroControl || Math.Abs(tempMotorSignals[1]) < minControl && Math.Abs(tempMotorSignals[1]) > zeroControl)
            {
                if (Math.Abs(tempMotorSignals[0]) < Math.Abs(tempMotorSignals[1]))
                {
                    tempMotorSignals[1] = tempMotorSignals[1] + Math.Sign(tempMotorSignals[1]) * (minControl - Math.Abs(tempMotorSignals[0]));
                    tempMotorSignals[0] = Math.Sign(tempMotorSignals[0]) * minControl;
                }
                else
                {
                    tempMotorSignals[0] = tempMotorSignals[0] + Math.Sign(tempMotorSignals[0]) * (minControl - Math.Abs(tempMotorSignals[1]));
                    tempMotorSignals[1] = Math.Sign(tempMotorSignals[1]) * minControl;
                }
            }

            // Saturate control signal or set to zero if its smaller than the min allowed value.
            if (Math.Abs(tempMotorSignals[0]) > maxControl)
            {
                tempMotorSignals[0] = Math.Sign(tempMotorSignals[0]) * maxControl;
            }
            else if (Math.Abs(tempMotorSignals[0]) <= zeroControl)
            {
                tempMotorSignals[0] = 0;
            }

            if (Math.Abs(tempMotorSignals[1]) > maxControl)
            {
                tempMotorSignals[1] = Math.Sign(tempMotorSignals[1]) * maxControl;
            }
            else if (Math.Abs(tempMotorSignals[1]) <= zeroControl)
            {
                tempMotorSignals[1] = 0;
            }

            return tempMotorSignals;

        }*/


        // ----------------- Update GUI method -----------------
        public void setStrategy(ControlStrategy strategy)
        {
            currentStrategy = strategy.cloneStrategy();
        }
    }
}
