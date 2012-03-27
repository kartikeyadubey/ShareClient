using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenNI;
using NITE;

namespace ShareClient
{
    class MyBox : PointControl
    {

        public MyBox(string name) :
            base()
        {
            Console.WriteLine("MyBox created");
            this.name = name;
            pushDetector = new PushDetector();
            swipeDetector = new SwipeDetector();
            steadyDetector = new SteadyDetector();
            flowRouter = new FlowRouter();
            broadcaster = new Broadcaster();

            broadcaster.AddListener(pushDetector);
            broadcaster.AddListener(flowRouter);

            pushDetector.Push += new EventHandler<VelocityAngleEventArgs>(pushDetector_Push);
            steadyDetector.Steady += new EventHandler<SteadyEventArgs>(steadyDetector_Steady);
            swipeDetector.GeneralSwipe += new EventHandler<DirectionVelocityAngleEventArgs>(swipeDetector_GeneralSwipe);

            PrimaryPointCreate += new EventHandler<HandFocusEventArgs>(MyBox_PrimaryPointCreate);
            PrimaryPointDestroy += new EventHandler<IdEventArgs>(MyBox_PrimaryPointDestroy);
            PrimaryPointUpdate += new EventHandler<HandEventArgs>(MyBox_PrimaryPointUpdate);
            OnUpdate += new EventHandler<UpdateMessageEventArgs>(MyBox_OnUpdate);
        }

        void MyBox_PrimaryPointUpdate(object sender, HandEventArgs e)
        {
            //Console.WriteLine("Point Updated" + e.Hand.Position.ToString());
            Update(e.Hand.Position);
        }


        void MyBox_OnUpdate(object sender, UpdateMessageEventArgs e)
        {
            broadcaster.UpdateMessage(e.Message);
        }

        void MyBox_PrimaryPointDestroy(object sender, IdEventArgs e)
        {
            Console.WriteLine("Point destroyed");
            //this.box.BackColor = Color.LightBlue;
        }

        void MyBox_PrimaryPointCreate(object sender, HandFocusEventArgs e)
        {
            Console.WriteLine("PrimaryPointCreate");
            flowRouter.ActiveListener = steadyDetector;
        }

        void swipeDetector_GeneralSwipe(object sender, DirectionVelocityAngleEventArgs e)
        {
            Console.WriteLine("{0}: Swipe {1}", this.name, e.Direction);
            flowRouter.ActiveListener = steadyDetector;
        }

        void steadyDetector_Steady(object sender, SteadyEventArgs e)
        {
            Console.WriteLine("Steady {0} ({1})", e.ID, PrimaryID);
            if (e.ID == PrimaryID)
            {
                flowRouter.ActiveListener = swipeDetector;
            }
        }

        void pushDetector_Push(object sender, VelocityAngleEventArgs e)
        {
            Leave();
        }

        #region Leave Event
        public delegate void LeaveHandler();
        public event LeaveHandler Leave;
        #endregion

        #region Update Event
        public delegate void UpdateHandler(Point3D p);
        public event UpdateHandler Update;
        #endregion

        private PushDetector pushDetector;
        private SwipeDetector swipeDetector;
        private SteadyDetector steadyDetector;
        private FlowRouter flowRouter;
        private Broadcaster broadcaster;
        private string name;
    }
}
