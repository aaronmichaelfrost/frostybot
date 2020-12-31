using System.Numerics;
using System.Windows.Media;
using Bot.Utilities.Processed.BallPrediction;
using Bot.Utilities.Processed.FieldInfo;
using Bot.Utilities.Processed.Packet;
using RLBotDotNet;

using System;
using System.Diagnostics;

using System.Collections.Generic;


// FROSTYBOT is a 1v1 BOT ONLY




namespace Bot
{


    public class BehaviorController
    {
        public Controller controller;
        private FrostyBot agent;
        private List<Behavior> behaviors = new List<Behavior>();
        private Behavior defaultBehavior = new TakeShot();
        public Behavior currentBehavior;

        public BehaviorController(ref FrostyBot agent) 
        { 
            this.agent = agent;

            // Add behaviors to list in order of priority
            behaviors.Add(new TakeShot());
            behaviors.Add(new GetBoost());
        }

        private Behavior SelectBehavior()
        {
            if(behaviors.Count > 0)
                for (int i = 0; i < behaviors.Count; i++)
                    if (behaviors[i].IsViable(agent))
                    {
                        ResetController();
                        return behaviors[i];
                    }
                        

            return defaultBehavior;
        }

        public void Behave()
        {
            currentBehavior = SelectBehavior();
            currentBehavior.Behave(ref agent);
        }

        public void ResetController()
        {
            agent.controller = new Controller();
        }
    }


    public abstract class Behavior
    {
        public abstract string Name();
        public abstract bool IsViable(FrostyBot agent);
        public abstract void Behave(ref FrostyBot agent);
        
    }


    public class TakeShot : Behavior
    {
        public override string Name() { return "SetupShot"; }
        public override bool IsViable(FrostyBot agent)
        {
            return agent.boostAmount > 40;
        }
        public override void Behave(ref FrostyBot agent)
        {
            //agent.ApproachTarget(agent.setupPosition, false, 1);
            agent.controller.Throttle = 1f;
            agent.SteerTo(agent.field.ballLocation);
            if (Vector3.Distance(agent.transform.location, agent.field.ballLocation) < 1000 && agent.field.ballLocation.Y - 100 < agent.transform.location.Y)
            {
                
                agent.controller.Boost = true;
  
            }
            else
            {
                agent.controller.Boost = false;
            }
        }
    }


    public class GetBoost : Behavior
    {
        public override string Name() { return "GetBoost"; }
        public override bool IsViable(FrostyBot agent)
        {
            if (agent.boostAmount < 80)
                return true;
            return false;
        }
        public override void Behave(ref FrostyBot agent)
        {
            agent.controller.Throttle = 1;
            agent.controller.Boost = true;
            agent.SteerTo(agent.NearestBoost().location);
        }
    }


    public class Boost
    {
        public Vector3 location;
        public bool isFull, isActive;
        public int index;
        public float timer;


        public Boost(Vector3 _location, bool _isFull, int _index)
        {
            location = _location;
            isFull = _isFull;
            index = _index;
            isActive = true;
            timer = 0;
        }
    }


    public struct Opponent
    {
        public Transform transform;
        public int index;
    }


    public struct Field
    {
        public List<Boost> boosts;
        public Vector3 ballLocation;
        public Vector3 ballRelativeLocation;
        public Vector3 ballVelocity;
        public BallPrediction ballPrediction;
    }


    public struct Transform
    {
        public Vector3 location;
        public Orientation rotation;
    }


    public class FrostyBot : RLBotDotNet.Bot
    {

        public Field field;
        public Opponent opponent;
        public Transform transform;
        public Controller controller;
        public int boostAmount;
        public Packet packet;

        public float forwardsVelocity;

        public bool ballInLineWithEnemyGoal;
        public Vector3 setupPosition;
        

        private BehaviorController behaviorController;


        Vector2 origin = new Vector2();
        Vector2 midpoint = new Vector2();
        Vector2 position2D = new Vector2();

        public void SteerTo(Vector3 targetLocation)
        {
            // Set steer value so that we will travel along the shortest circle that reaches the destination

            float slopeTangent = transform.rotation.Forward.Y / transform.rotation.Forward.X;

            float slopePerpendicular = -1 / slopeTangent;
            float interceptPerpedicular = transform.location.Y + transform.location.X / slopeTangent;

            midpoint.X = (transform.location.X + targetLocation.X) / 2;
            midpoint.Y = (transform.location.Y + targetLocation.Y) / 2;

            float slopeChord = (transform.location.Y - targetLocation.Y) / (transform.location.X - targetLocation.X);

            float slopePerpendicularBisector = -1 / slopeChord;
            float interceptPerpendicularBisector = midpoint.Y + midpoint.X / slopeChord;

            origin.X = (interceptPerpendicularBisector - interceptPerpedicular) / (slopePerpendicular - slopePerpendicularBisector);
            origin.Y = slopePerpendicular * origin.X + interceptPerpedicular;

            position2D.X = transform.location.X;
            position2D.Y = transform.location.Y;

            float circleRadius = Vector2.Distance(origin, position2D);

            Renderer.DrawCenteredRectangle3D(Colors.Blue, new Vector3(origin.X, origin.Y, 10), 20, 20, true);

            float steer = (float)TurnRadius(forwardsVelocity) / circleRadius;

            if (steer > 1)
                steer = 1;
            if (steer < -1)
                steer = -1;

            if (field.ballRelativeLocation.Y < 0)
                steer *= -1;

            controller.Steer = steer;

            // Powerslide if needed
            if (Vector3.Dot(forwardsDirection, targetLocation - transform.location) < .5)
                controller.Handbrake = true;
            else
                controller.Handbrake = false;
        }



        public FrostyBot(string botName, int botTeam, int botIndex) : base(botName, botTeam, botIndex) {

            Init();
            FrostyBot me = this;
            behaviorController = new BehaviorController(ref me);

        }

        public bool BallInLineWithEnemyGoal()
        {
            float m = (transform.location.Y - field.ballLocation.Y) / (transform.location.X - field.ballLocation.X);

            float b = transform.location.Y - m * transform.location.X;

            float intersectX;

            if (team == 0)
            {
                intersectX = (-5120 - b) / m;

                if (field.ballLocation.Y < transform.location.Y)
                    return false;
            }
            else
            {
                intersectX = (5120 - b) / m;

                if (field.ballLocation.Y > transform.location.Y)
                    return false;
            }

            

            if (intersectX > -800.25 && intersectX < 800.25)
                return true;

                

            return false;
        }

        double Curvature(float forwardVelocity)
        {
            if (0.0 <= forwardVelocity && forwardVelocity < 500.0)
                return 0.006900 - 5.84e-6 * forwardVelocity;
            if (500.0 <= forwardVelocity && forwardVelocity < 1000.0)
                return 0.005610 - 3.26e-6 * forwardVelocity;
            if (1000.0 <= forwardVelocity && forwardVelocity < 1500.0)
                return 0.004300 - 1.95e-6 * forwardVelocity;
            if (1500.0 <= forwardVelocity && forwardVelocity < 1750.0)
                return 0.003025 - 1.1e-6 * forwardVelocity;
            if (1750.0 <= forwardVelocity && forwardVelocity < 2500.0)
                return 0.001800 - 4e-7 * forwardVelocity;

            return 0.0;
        }

        double TurnRadius(float forwardVelocity)
        {
            if (forwardVelocity == 0)
                return 0;
            return 1.0 / Curvature(forwardVelocity);
        }


        void Init()
        {
            opponent.index = (index == 1) ? 0 : 1;

            field.boosts = new List<Boost>();
            FieldInfo fieldInfo = GetFieldInfo();
            int n = fieldInfo.BoostPads.Length;
            for (int i = 0; i < n; i++)
            {
                BoostPad b = fieldInfo.BoostPads[i];
                field.boosts.Add(new Boost(b.Location, b.IsFullBoost, i));
            }
        }


        // Return the closest full and active boost pad to the player
        public Boost NearestBoost()
        {

            Boost bestOption = field.boosts[0];

            for (int i = 0; i < GetFieldInfo().BoostPads.Length; i++)
            {

                Boost currentPad = field.boosts[i];

                if ((currentPad.isFull && field.boosts[i].isActive && Vector3.Distance(currentPad.location, transform.location) < Vector3.Distance(bestOption.location, transform.location)) 
                    || (!bestOption.isFull || !bestOption.isActive))
                    bestOption = currentPad;
            }

            
            Renderer.DrawString3D("Ideal Boost", Colors.Blue, bestOption.location, 3, 3);

            return bestOption;
        }


        public Vector3 forwardsDirection = new Vector3();

        // Update the bot's vision
        void CollectInfo()
        {
            for (int i = 0; i < field.boosts.Count; i++)
            {
                field.boosts[i].isActive = packet.BoostPadStates[i].IsActive;
                field.boosts[i].timer = packet.BoostPadStates[i].Timer;
            }

            transform.location = packet.Players[index].Physics.Location;
            transform.rotation = packet.Players[index].Physics.Rotation;


            double x = Math.Cos(transform.rotation.Pitch) * Math.Cos(transform.rotation.Yaw);
            double y = Math.Cos(transform.rotation.Pitch) * Math.Sin(transform.rotation.Yaw);
            double z = Math.Sin(transform.rotation.Pitch);



            forwardsDirection = new Vector3((float)x, (float)y, (float)z);
            forwardsVelocity = Vector3.Dot(forwardsDirection, packet.Players[index].Physics.Velocity);
            Renderer.DrawString2D("FVel " + forwardsVelocity, Colors.Red, currentStateTextPos + new Vector2(0, 50), 2, 2);

            boostAmount = packet.Players[index].Boost;

            //opponent.transform.location = packet.Players[opponent.index].Physics.Location;
            //opponent.transform.rotation = packet.Players[opponent.index].Physics.Rotation;

            field.ballLocation = packet.Ball.Physics.Location;
            field.ballRelativeLocation = Orientation.RelativeLocation(transform.location, field.ballLocation, transform.rotation);
            field.ballVelocity = packet.Ball.Physics.Velocity;

            field.ballPrediction = GetBallPrediction();

        }


        public override Controller GetOutput(rlbot.flat.GameTickPacket gameTickPacket)
        {
            packet = new Packet(gameTickPacket);


            CollectInfo();

            behaviorController.Behave();

            Render();

            return controller;
        }




        Vector2 currentStateTextPos = new Vector2(0, 0);


        private void Render()
        {
            Renderer.DrawString3D("Ball", Colors.Black, field.ballLocation, 3, 3);
            Renderer.DrawString3D(controller.Steer > 0 ? "Right" : "Left", Colors.Aqua, transform.location, 3, 3);
            Renderer.DrawLine3D(Colors.Red, transform.location, field.ballLocation);
            Renderer.DrawLine3D(Colors.Red, transform.location, opponent.transform.location);


            Renderer.DrawString2D("Steer " + controller.Steer, Colors.Red, currentStateTextPos, 2, 2);

            //Renderer.DrawString2D("Current State: " + behaviorController.currentBehavior.Name(), Colors.Red, currentStateTextPos, 2, 2);
        }



        // Hide the old methods that return Flatbuffers objects and use our own methods that
        // use processed versions of those objects instead.
        internal new FieldInfo GetFieldInfo() => new FieldInfo(base.GetFieldInfo());
        internal new BallPrediction GetBallPrediction() => new BallPrediction(base.GetBallPrediction());

    }
}