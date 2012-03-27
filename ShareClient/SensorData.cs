using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using OpenNI;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ShareClient
{
    class SensorData
    {
        #region Constants

        /// <summary>
        /// Default configuration file path.
        /// </summary>
        private const string CONFIGURATION = @"../../../../Data/LowResConfig.xml";

        /// <summary>
        /// Horizontal bitmap dpi.
        /// </summary>
        private readonly int DPI_X = 96;

        /// <summary>
        /// Vertical bitmap dpi.
        /// </summary>
        private readonly int DPI_Y = 96;

        #endregion

        #region Members

        /// <summary>
        /// Thread responsible for image and depth camera updates.
        /// </summary>
        private Thread _cameraThread;

        /// <summary>
        /// Indicates whether the thread is running.
        /// </summary>
        private bool _isRunning = true;

        /// <summary>
        /// Image camera source.
        /// </summary>
        private WriteableBitmap _imageBitmap;

        /// <summary>
        /// Depth camera source.
        /// </summary>
        private WriteableBitmap _depthBitmap;

        /// <summary>
        /// Raw image metadata.
        /// </summary>
        private ImageMetaData _imageMD = new ImageMetaData();

        /// <summary>
        /// Depth image metadata.
        /// </summary>
        private DepthMetaData _depthMD = new DepthMetaData();

        private ScriptNode scriptNode;
        private UserGenerator userGenerator;
        private SkeletonCapability skeletonCapbility;
        private PoseDetectionCapability poseDetectionCapability;
        private string calibPose;
        private Dictionary<int, Dictionary<SkeletonJoint, SkeletonJointPosition>> joints;

        public byte[] backgroundImage;
        private bool backgroundDrawn = false;

        private NITE.SessionManager sessionManager;
        public bool playerRecognized = false;

        private bool clientConnected = false;
        private int xStart = int.MaxValue;
        private int yStart = int.MaxValue;
        private int xEnd = int.MinValue;
        private int yEnd = int.MinValue;
        private byte[] playerImage;
        #endregion


        #region Events
        public delegate void UpdatedEventHandler(object sender, Point3D handPoint);
        public event UpdatedEventHandler updated;

        public delegate void SendImageHandler(object sender, WriteableBitmap b, bool playerFound, int xS, int xE, int yS, int yE);
        public event SendImageHandler imageUpdate;
        #endregion

        #region Properties

        #region Bitmap properties

        /// <summary>
        /// Returns the image camera's bitmap source.
        /// </summary>
        public ImageSource RawImageSource
        {
            get
            {
                if (_imageBitmap != null)
                {
                    _imageBitmap.Lock();
                    //If the background has not been saved yet
                    //save it
                    if (!this.backgroundDrawn)
                    {
                        CalcBackground(_imageMD);
                    }
                    unsafe
                    {
                        resetBoundingBox();
                        RGB24Pixel* pImage = (RGB24Pixel*)this.ImageGenerator.ImageMapPtr.ToPointer();
                        ushort* pLabels = (ushort*)this.userGenerator.GetUserPixels(0).LabelMapPtr.ToPointer();
                        //Keep track of backgroundImage array
                        int backgroundImageCounter = 0;
                        for (int y = 0; y < _imageMD.YRes; ++y)
                        {
                            byte* pDest = (byte*)_imageBitmap.BackBuffer.ToPointer() + y * _imageBitmap.BackBufferStride;

                            for (int x = 0; x < _imageMD.XRes; ++x, ++pImage, pDest += 4, backgroundImageCounter += 4)
                            {
                                //Fill in display with background image
                                if (*pLabels == 0 && backgroundDrawn)
                                {
                                    //Setting alpha value to 0
                                    pDest[0] = this.backgroundImage[backgroundImageCounter];
                                    pDest[1] = this.backgroundImage[backgroundImageCounter + 1];
                                    pDest[2] = this.backgroundImage[backgroundImageCounter + 2];
                                    pDest[3] = this.backgroundImage[backgroundImageCounter + 3];
                                }
                            }
                        }

                        pLabels = (ushort*)this.userGenerator.GetUserPixels(0).LabelMapPtr.ToPointer();
                        pImage = (RGB24Pixel*)this.ImageGenerator.ImageMapPtr.ToPointer();
                        ushort* pDepth = (ushort*)this.DepthGenerator.DepthMapPtr.ToPointer();

                        int playerImageCounter = 0;
                        playerRecognized = false;

                        for (int y = 0; y < _imageMD.YRes; ++y)
                        {
                            byte* pDest = (byte*)_imageBitmap.BackBuffer.ToPointer() + y * _imageBitmap.BackBufferStride;
                            for (int x = 0; x < _imageMD.XRes; ++x, ++pImage, ++pDepth, ++pLabels, pDest += 4, playerImageCounter += 4)
                            {
                                // translate from RGB to BGR (windows format) 
                                // Based on distance fill in data
                                //User detected and depth less than 2000mm 
                                //display user RGB data
                                if (*pDepth < 2000 && *pLabels > 0)
                                {
                                    pDest[0] = pImage->Blue;
                                    pDest[1] = pImage->Green;
                                    pDest[2] = pImage->Red;
                                    pDest[3] = 255;
                                    playerRecognized = true;


                                    //Calculate bounding box
                                    if (xStart > x)
                                    {
                                        xStart = x;
                                    }
                                    if (yStart > y)
                                    {
                                        yStart = y;
                                    }
                                    if (xEnd < x)
                                    {
                                        xEnd = x;
                                    }
                                    if (yEnd < y)
                                    {
                                        yEnd = y;
                                    }

                                }
                                //User detected depth lesser than 3000mm grey out user
                                else if (*pLabels > 0 && *pLabels < 3000)
                                {
                                    pDest[0] = pImage->Blue;
                                    pDest[1] = pImage->Green;
                                    pDest[2] = pImage->Red;
                                    pDest[3] = 100;
                                    playerRecognized = true;

                                    //Calculate Bounding box
                                    if (xStart > x)
                                    {
                                        xStart = x;
                                    }
                                    if (yStart > y)
                                    {
                                        yStart = y;
                                    }
                                    if (xEnd < x)
                                    {
                                        xEnd = x;
                                    }
                                    if (yEnd < y)
                                    {
                                        yEnd = y;
                                    }
                                }
                            }
                        }

                    }

                    _imageBitmap.AddDirtyRect(new Int32Rect(0, 0, _imageMD.XRes, _imageMD.YRes));
                
                    _imageBitmap.Unlock();
                }

                if (playerRecognized)
                {
                    imageUpdate(this, _imageBitmap, true, xStart, xEnd, yStart, yEnd);
                }
                else
                {
                    imageUpdate(this, _imageBitmap, false, xStart, xEnd, yStart, yEnd);
                }
                
                return _imageBitmap;
            }
        }

        //Reset the bounding box information
        //so that it can be calculated correctly
        private void resetBoundingBox()
        {
            xStart = int.MaxValue;
            yStart = int.MaxValue;
            xEnd = int.MinValue;
            yEnd = int.MinValue;
        }

        //Store the background image information
        //so that it can be redrawn every frame
        private unsafe void CalcBackground(ImageMetaData imageMD)
        {
            if (this.backgroundDrawn)
            {
                return;
            }
            int arraySize = imageMD.XRes * imageMD.YRes * 4;
            //Initialize data structure
            this.backgroundImage = new byte[arraySize];
            //Keep track of backgroundImage array
            int backgroundImageCounter = 0;

            RGB24Pixel* pImage = (RGB24Pixel*)this.ImageGenerator.ImageMapPtr.ToPointer();
            //Initialize all to 0
            for (int y = 0; y < imageMD.YRes; ++y)
            {
                for (int x = 0; x < imageMD.XRes; ++x, ++pImage, backgroundImageCounter += 4)
                {
                    backgroundImage[backgroundImageCounter] = pImage->Blue;
                    backgroundImage[backgroundImageCounter + 1] = pImage->Green;
                    backgroundImage[backgroundImageCounter + 2] = pImage->Red;
                    backgroundImage[backgroundImageCounter + 3] = 255;
                }
            }
            this.backgroundDrawn = true;

        }

        /// <summary>
        /// Returns the depth camera's bitmap source.
        /// </summary>
        public ImageSource DepthImageSource
        {
            get
            {
                if (_depthBitmap != null)
                {
                    UpdateHistogram(_depthMD);

                    _depthBitmap.Lock();

                    unsafe
                    {
                        ushort* pDepth = (ushort*)DepthGenerator.DepthMapPtr.ToPointer();
                        for (int y = 0; y < _depthMD.YRes; ++y)
                        {
                            byte* pDest = (byte*)_depthBitmap.BackBuffer.ToPointer() + y * _depthBitmap.BackBufferStride;
                            for (int x = 0; x < _depthMD.XRes; ++x, ++pDepth, pDest += 3)
                            {
                                byte pixel = (byte)Histogram[*pDepth];
                                pDest[0] = 0;
                                pDest[1] = pixel;
                                pDest[2] = pixel;
                            }
                        }
                    }

                    _depthBitmap.AddDirtyRect(new Int32Rect(0, 0, _depthMD.XRes, _depthMD.YRes));
                    _depthBitmap.Unlock();
                }

                return _depthBitmap;
            }
        }

        #endregion

        #region OpenNI properties

        /// <summary>
        /// OpenNI main Context.
        /// </summary>
        public Context Context { get; private set; }

        /// <summary>
        /// OpenNI image generator.
        /// </summary>
        public ImageGenerator ImageGenerator { get; private set; }

        /// <summary>
        /// OpenNI depth generator.
        /// </summary>
        public DepthGenerator DepthGenerator { get; private set; }

        /// <summary>
        /// OpenNI histogram.
        /// </summary>
        public int[] Histogram { get; private set; }

        public bool drawBackground = true;
        private NITE.FlowRouter flowRouter;
        private MyBox boxes;
        #endregion
        #endregion
        #region Constructor

        /// <summary>
        /// Creates a new instance of SensorData with the default configuration file.
        /// </summary>
        public SensorData()
            : this(CONFIGURATION)
        {
        }

        /// <summary>
        /// Creates a new instance of SensorData with the specified configuration file.
        /// </summary>
        /// <param name="configuration">Configuration file path.</param>
        public SensorData(string configuration)
        {
            InitializeCamera(configuration);
            InitializeBitmaps();
            InitializeThread();

            //New flow router
            this.flowRouter = new NITE.FlowRouter();

            this.boxes = new MyBox("Box1");
            this.boxes.Leave += new MyBox.LeaveHandler(boxes_Leave);
            this.boxes.Update += new MyBox.UpdateHandler(boxes_Update);
            this.sessionManager.AddListener(this.flowRouter);
            this.sessionManager.SessionStart += new EventHandler<NITE.PositionEventArgs>(sessionManager_SessionStart);
            Console.WriteLine("Initialized Sensor Data");

            this.DepthGenerator.AlternativeViewpointCapability.SetViewpoint(this.ImageGenerator);
            this.userGenerator = new UserGenerator(this.Context);
            this.skeletonCapbility = this.userGenerator.SkeletonCapability;
            this.poseDetectionCapability = this.userGenerator.PoseDetectionCapability;
            this.calibPose = this.skeletonCapbility.CalibrationPose;
            this.poseDetectionCapability.PoseDetected += poseDetectionCapability_PoseDetected;
            this.skeletonCapbility.CalibrationComplete += skeletonCapbility_CalibrationComplete;

            this.skeletonCapbility.SetSkeletonProfile(SkeletonProfile.All);
            this.joints = new Dictionary<int, Dictionary<SkeletonJoint, SkeletonJointPosition>>();

            this.userGenerator.NewUser += userGenerator_NewUser;
            this.userGenerator.LostUser += userGenerator_LostUser;
            this.userGenerator.StartGenerating();
        }

        void boxes_Update(Point3D p)
        {
            updated(this, p);
        }

        void boxes_Leave()
        {
            Console.WriteLine("Left box");
        }

        void sessionManager_SessionStart(object sender, NITE.PositionEventArgs e)
        {
            Console.WriteLine("Session started");
            this.flowRouter.ActiveListener = this.boxes;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Initializes the image and depth camera.
        /// </summary>
        /// <param name="configuration">Configuration file path.</param>
        private void InitializeCamera(string configuration)
        {
            try
            {
                this.Context = Context.CreateFromXmlFile(configuration, out scriptNode);
                this.sessionManager = new NITE.SessionManager(this.Context, "Wave", "RaiseHand");
            }
            catch
            {
                throw new Exception("Configuration file not found.");
            }

            ImageGenerator = Context.FindExistingNode(NodeType.Image) as ImageGenerator;
            DepthGenerator = Context.FindExistingNode(NodeType.Depth) as DepthGenerator;
            DepthGenerator.AlternativeViewpointCapability.SetViewpoint(ImageGenerator);
            Histogram = new int[DepthGenerator.DeviceMaxDepth];
        }

        /// <summary>
        /// Initializes the image and depth bitmap sources.
        /// </summary>
        private void InitializeBitmaps()
        {
            MapOutputMode mapMode = this.ImageGenerator.MapOutputMode;
            int width = (int)mapMode.XRes;
            int height = (int)mapMode.YRes;

            Console.WriteLine("Res: " + width + ":" + height);
            _imageBitmap = new WriteableBitmap(width, height, DPI_X, DPI_Y, PixelFormats.Bgra32, null);
            _depthBitmap = new WriteableBitmap(width, height, DPI_X, DPI_Y, PixelFormats.Rgb24, null);
        }

        /// <summary>
        /// Initializes the background camera thread.
        /// </summary>
        private void InitializeThread()
        {
            _isRunning = true;

            _cameraThread = new Thread(CameraThread);
            _cameraThread.IsBackground = true;
            _cameraThread.Start();
        }

        /// <summary>
        /// Updates image and depth values.
        /// </summary>
        private unsafe void CameraThread()
        {
            while (_isRunning)
            {
                //Context.WaitAndUpdateAll();
                Context.WaitAnyUpdateAll();
                this.sessionManager.Update(this.Context);
                ImageGenerator.GetMetaData(_imageMD);
                DepthGenerator.GetMetaData(_depthMD);
            }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Re-creates the depth histogram.
        /// </summary>
        /// <param name="depthMD"></param>
        public unsafe void UpdateHistogram(DepthMetaData depthMD)
        {
            // Reset.
            for (int i = 0; i < Histogram.Length; ++i)
                Histogram[i] = 0;

            ushort* pDepth = (ushort*)depthMD.DepthMapPtr.ToPointer();

            int points = 0;
            for (int y = 0; y < depthMD.YRes; ++y)
            {
                for (int x = 0; x < depthMD.XRes; ++x, ++pDepth)
                {
                    ushort depthVal = *pDepth;
                    if (depthVal != 0)
                    {
                        Histogram[depthVal]++;
                        points++;
                    }
                }
            }

            for (int i = 1; i < Histogram.Length; i++)
            {
                Histogram[i] += Histogram[i - 1];
            }

            if (points > 0)
            {
                for (int i = 1; i < Histogram.Length; i++)
                {
                    Histogram[i] = (int)(256 * (1.0f - (Histogram[i] / (float)points)));
                }
            }
        }

        void userGenerator_NewUser(object sender, NewUserEventArgs e)
        {
            if (this.skeletonCapbility.DoesNeedPoseForCalibration)
            {
                this.poseDetectionCapability.StartPoseDetection(this.calibPose, e.ID);
            }
            else
            {
                this.skeletonCapbility.RequestCalibration(e.ID, true);
            }
        }

        void userGenerator_LostUser(object sender, UserLostEventArgs e)
        {
            this.joints.Remove(e.ID);
        }

        void skeletonCapbility_CalibrationComplete(object sender, CalibrationProgressEventArgs e)
        {
            if (e.Status == CalibrationStatus.OK)
            {
                this.skeletonCapbility.StartTracking(e.ID);
                this.joints.Add(e.ID, new Dictionary<SkeletonJoint, SkeletonJointPosition>());
            }
            else if (e.Status != CalibrationStatus.ManualAbort)
            {
                if (this.skeletonCapbility.DoesNeedPoseForCalibration)
                {
                    this.poseDetectionCapability.StartPoseDetection(calibPose, e.ID);
                }
                else
                {
                    this.skeletonCapbility.RequestCalibration(e.ID, true);
                }
            }
        }

        void poseDetectionCapability_PoseDetected(object sender, PoseDetectedEventArgs e)
        {
            this.poseDetectionCapability.StopPoseDetection(e.ID);
            this.skeletonCapbility.RequestCalibration(e.ID, true);
        }

        /// <summary>
        /// Releases any resources.
        /// </summary>
        public void Dispose()
        {
            _imageBitmap = null;
            _depthBitmap = null;
            _isRunning = false;
            _cameraThread.Join();
            Context.Dispose();
            _cameraThread = null;
            Context = null;
        }

        #endregion
    }
}
