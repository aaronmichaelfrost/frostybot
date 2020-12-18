using System.Numerics;
using System.Windows.Media;
using Bot.Utilities.Processed.BallPrediction;
using Bot.Utilities.Processed.FieldInfo;
using Bot.Utilities.Processed.Packet;
using RLBotDotNet;

using System.Collections.Generic;


// FROSTYBOT is a 1v1 BOT ONLY



namespace Bot
{

    
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


    // We want to our bot to derive from Bot, and then implement its abstract methods.
    class FrostyBot : RLBotDotNet.Bot
    {

        List<Boost> boosts = new List<Boost>();
        private int opponentIndex;



        // We want the constructor for our Bot to extend from RLBotDotNet.Bot, but we don't want to add anything to it.
        // You might want to add logging initialisation or other types of setup up here before the bot starts.
        public FrostyBot(string botName, int botTeam, int botIndex) : base(botName, botTeam, botIndex) {
            SetOpponentIndex();
            InitializeBoostLocations();
        }


        void InitializeBoostLocations()
        {
            int n = GetFieldInfo().BoostPads.Length;
            for (int i = 0; i < n; i++)
            {
                BoostPad b = GetFieldInfo().BoostPads[i];
                boosts.Add(new Boost(b.Location, b.IsFullBoost, i));
            }
        }


        private void SetOpponentIndex()
        {
            opponentIndex = index == 1 ? 0 : 1;
        }


        Boost IdealFullBoostPad(Packet gameTickPacket, Vector3 carLocation)
        {

            // Return the closest full and active boost pad to the player

            Boost bestOption = boosts[0];

            for (int i = 0; i < GetFieldInfo().BoostPads.Length; i++)
            {

                Boost currentPad = boosts[i];

                if ((currentPad.isFull && boosts[i].isActive && Vector3.Distance(currentPad.location, carLocation) < Vector3.Distance(bestOption.location, carLocation)) 
                    || (!bestOption.isFull || !bestOption.isActive))
                    bestOption = currentPad;
            }

            
            Renderer.DrawString3D("Ideal Boost", Colors.Blue, bestOption.location, 3, 3);

            return bestOption;
        }


        void UpdateBoostStates(Packet packet)
        {
            for (int i = 0; i < boosts.Count; i++)
            {
                boosts[i].isActive = packet.BoostPadStates[i].IsActive;
                boosts[i].timer = packet.BoostPadStates[i].Timer;
            }
        }

            


            
        
        

        

        public override Controller GetOutput(rlbot.flat.GameTickPacket gameTickPacket)
        {
            // We process the gameTickPacket and convert it to our own internal data structure.
            Packet packet = new Packet(gameTickPacket);


            UpdateBoostStates(packet);

            // Get prediction for ball 25 game ticks in the future
            //GetBallPrediction().Slices[25].Physics.Location;


            Vector3 ballLocation = packet.Ball.Physics.Location;
            Vector3 ballVelocity = packet.Ball.Physics.Velocity;


            Vector3 carLocation = packet.Players[index].Physics.Location;
            Orientation carRotation = packet.Players[index].Physics.Rotation;


            Vector3 opponentLocation = packet.Players[opponentIndex].Physics.Location;
            Orientation opponentRotation = packet.Players[opponentIndex].Physics.Rotation;


            // Find where the ball is relative to us.
            Vector3 ballRelativeLocation = Orientation.RelativeLocation(carLocation, ballLocation, carRotation);

            Vector3 idealFullBoostRelativeLocation = Orientation.RelativeLocation(carLocation, IdealFullBoostPad(packet, carLocation).location, carRotation);


            // Decide which way to steer in order to get to the ball.
            // If the ball is to our left, we steer left. Otherwise we steer right.
            float steer;
            if (idealFullBoostRelativeLocation.Y > 0)
                steer = 1;
            else
                steer = -1;




            // Get boost
            //Vector3 nearestBoostLocation = 
            



            // Examples of rendering in the game
            Renderer.DrawString3D("Ball", Colors.Black, ballLocation, 3, 3);
            Renderer.DrawString3D(steer > 0 ? "Right" : "Left", Colors.Aqua, carLocation, 3, 3);
            Renderer.DrawLine3D(Colors.Red, carLocation, ballLocation);

            Renderer.DrawLine3D(Colors.Red, carLocation, opponentLocation);

            // This controller will contain all the inputs that we want the bot to perform.
            return new Controller
            {
                // Set the throttle to 1 so the bot can move.
                Throttle = 1,
                Steer = steer,


                Jump = true

            };
        }
        // Hide the old methods that return Flatbuffers objects and use our own methods that
        // use processed versions of those objects instead.
        internal new FieldInfo GetFieldInfo() => new FieldInfo(base.GetFieldInfo());
        internal new BallPrediction GetBallPrediction() => new BallPrediction(base.GetBallPrediction());

    }
}