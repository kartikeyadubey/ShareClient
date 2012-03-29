using System;
using System.Collections.Generic;
using System.Linq;
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
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;

namespace ShareClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WriteableBitmap serverImage;
        private const int TRANSMIT_IMAGE_SIZE = 320 * 240 * 4;
        private const int TRANSMIT_WIDTH = 320;
        private const int TRANSMIT_HEIGHT = 240;
        private const int DPI_X = 96;
        private const int DPI_Y = 96;
        private const string ipAddress = "127.0.0.1"; //"169.254.68.64, 172.16.0.248";
        private bool imageDrawn = false;
        BackgroundWorker _clientThread;
        private const int portReceive = 3000;
        private const int portSend = 4000;

        private bool backgroundReceived = false;
        byte[] backgroundImage;
        byte[] partialPlayerImage;
        byte[] playerImage;

        private int xStart;
        private int xEnd;
        private int yStart;
        private int yEnd;


        SensorData _sensor;
        private bool connectedToServer = false;
        TcpClient client;
        IPEndPoint serverEndPoint;
        NetworkStream clientStream;
        BackgroundWorker _worker;
        private bool clientConnected = false;
        Socket s;

        WriteableBitmap clientImage;
        bool backgroundSent = false;
        int imageSendCounter;

        public MainWindow()
        {
            InitializeComponent();

            CompositionTarget.Rendering += new EventHandler(CompositionTarget_Rendering);

            _clientThread = new BackgroundWorker();
            _clientThread.DoWork += new DoWorkEventHandler(clientThread_DoWork);

            serverImage = new WriteableBitmap(TRANSMIT_WIDTH, TRANSMIT_HEIGHT, DPI_X, DPI_Y, PixelFormats.Bgra32, null);
            _sensor = new SensorData();
            _sensor.updated += new SensorData.UpdatedEventHandler(_sensor_updated);
            _sensor.imageUpdate += new SensorData.SendImageHandler(_sensor_imageUpdate);
            _worker = new BackgroundWorker();
            _worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            imageSendCounter = 1;
        }

        void _sensor_imageUpdate(object sender, WriteableBitmap b, bool playerFound, int xStart, int xEnd, int yStart, int yEnd)
        {
            if (!clientConnected)
            {
                return;
            }
            else
            {
                //Background has been sent and there is a player
                //in the image send a message to the client
                //with the size of the bounding box and the image
                //Send every sixth frame that we receive
                if (backgroundSent && playerFound)
                {
                    //Send the client a flag
                    ASCIIEncoding encoder = new ASCIIEncoding();
                    byte[] buffer = encoder.GetBytes("playerimage");

                    byte[] completeMessage = new byte[65536];

                    byte[] readyToReceive = encoder.GetBytes("rcomplete");
                    clientStream.Write(readyToReceive, 0, readyToReceive.Length);
                    clientStream.Flush();

                    int k = s.Receive(completeMessage);
                    while (k == 0)
                    {
                        k = s.Receive(completeMessage);
                    }
                    //Read only the first 9 bytes since
                    //rcomplete might have been appended to avoid a deadlock due to 
                    //a blocking call
                    string tempMessage = encoder.GetString(completeMessage, 0, 9);
                    if (!tempMessage.Equals("rcomplete"))
                    {
                        Console.WriteLine("Message received: " + encoder.GetString(completeMessage, 0, k));
                        return;
                    }

                    //int imageSize = ((xEnd - xStart) * (yEnd - yStart) * 4);
                    //byte[] playerImage = new byte[(imageSize)];
                    clientImage = b.Resize(320, 240, RewritableBitmap.Interpolation.Bilinear);
                    double tmpXStart = (xStart / 2);
                    double tmpYStart = (yStart / 2);
                    double tmpXEnd = (xEnd / 2);
                    double tmpYEnd = (yEnd / 2);

                    xStart = Convert.ToInt32(Math.Floor(tmpXStart));
                    xEnd = Convert.ToInt32(Math.Floor(tmpXEnd));
                    yStart = Convert.ToInt32(Math.Floor(tmpYStart));
                    yEnd = Convert.ToInt32(Math.Floor(tmpYEnd));


                    int smallWidth = (xEnd - xStart);
                    int smallHeight = (yEnd - yStart);


                    int imgSize = smallWidth * smallHeight * 4;
                    //Console.WriteLine("Image size: " + imgSize);
                    byte[] transmitPlayerImage = new byte[imgSize];


                    clientImage.CopyPixels(new Int32Rect(xStart, yStart, smallWidth, smallHeight), transmitPlayerImage, (smallWidth * 4), 0);
                    //b.CopyPixels(new Int32Rect(xStart, yStart, (xEnd - xStart), (yEnd - yStart)), playerImage, ((xEnd - xStart) * 4), 0);


                    //Send the actual size of the bounding box
                    byte[] xS = BitConverter.GetBytes(xStart);
                    byte[] xE = BitConverter.GetBytes(xEnd);
                    byte[] yS = BitConverter.GetBytes(yStart);
                    byte[] yE = BitConverter.GetBytes(yEnd);
                    byte[] playerImageSize = BitConverter.GetBytes(transmitPlayerImage.Length);

                    //Image is too big don't try to send the data
                    if (encoder.GetString(completeMessage, 0, k) != "rcomplete")
                    {
                        return;
                    }

                    try
                    {
                        //Console.WriteLine("Status of socket: " + s.Blocking);
                        s.Send(buffer);

                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine("Error: " + e.ToString());
                    }

                    s.Send(xS);

                    s.Send(xE);

                    s.Send(yS);

                    s.Send(yE);

                    s.Send(playerImageSize);
                    imageSendCounter = 1;
                    s.Send(transmitPlayerImage);
                    //Console.WriteLine("Image sent, size of image: " + transmitPlayerImage.Length + "," + xStart + ", " + xEnd + ", " + yStart + ", " + yEnd);
                }
                else if (!backgroundSent)
                {
                    clientImage = b.Resize(320, 240, RewritableBitmap.Interpolation.Bilinear);
                    byte[] smallBackgroundImage = new byte[TRANSMIT_IMAGE_SIZE];
                    clientImage.CopyPixels(new Int32Rect(0, 0, 320, 240), smallBackgroundImage, 320 * 4, 0);
                    s.Send(smallBackgroundImage);
                    backgroundSent = true;
                    Console.WriteLine("Background sent");
                }
                else
                {
                    imageSendCounter++;
                }

            }
        }

        void _sensor_updated(object sender, OpenNI.Point3D handPoint)
        {
            Console.WriteLine("Hand point updated");
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate
            {
                image2.Source = _sensor.RawImageSource;
            });

            if (!clientConnected && connectedToServer)
            {
                IPAddress ipAddress = IPAddress.Any;
                TcpListener listener = new TcpListener(ipAddress, portSend);
                listener.Start();
                Console.WriteLine("Server is running");
                Console.WriteLine("Listening on port " + portSend);
                Console.WriteLine("Waiting for connections...");
                while (!clientConnected)
                {
                    s = listener.AcceptSocket();
                    s.SendBufferSize = 256000;
                    //s.NoDelay = true;
                    Console.WriteLine("Connection accepted from " + s.RemoteEndPoint);
                    byte[] b = new byte[65535];
                    int k = s.Receive(b);
                    Console.WriteLine("Received:");
                    ASCIIEncoding enc = new ASCIIEncoding();
                    //Ensure the client is who we want
                    if (enc.GetString(b, 0, k) == "hello" || enc.GetString(b, 0, k) == "hellorcomplete")
                    {
                        clientConnected = true;
                        Console.WriteLine(enc.GetString(b, 0, k));
                    }
                }
            }
        }

        void clientThread_DoWork(object sender, DoWorkEventArgs e)
        {
            ASCIIEncoding encoder = new ASCIIEncoding();
            byte[] buffer = encoder.GetBytes("hello");
            if (!connectedToServer)
            {
                client = new TcpClient();
                serverEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), portReceive);

                client.Connect(serverEndPoint);
                clientStream = client.GetStream();

                clientStream.Write(buffer, 0, buffer.Length);
                clientStream.Flush();
                Console.WriteLine("Client said hello");

                //Write to buffer
                buffer = encoder.GetBytes("rcomplete");
                clientStream.Write(buffer, 0, buffer.Length);
                clientStream.Flush();
                connectedToServer = true;
            }

            byte[] backgroundImageData = new byte[TRANSMIT_IMAGE_SIZE];
            byte[] serverMessage = new byte[65535];
            byte[] xS = new byte[4];
            byte[] xE = new byte[4];
            byte[] yS = new byte[4];
            byte[] yE = new byte[4];
            int bytesRead;

            while (true)
            {
                bytesRead = 0;

                try
                {
                    if (!client.Connected)
                    {
                        client.Connect(serverEndPoint);
                    }
                    clientStream.Flush();
                    //blocks until a server replies with a message
                    if (!backgroundReceived)
                    {
                        bytesRead = clientStream.Read(backgroundImageData, 0, TRANSMIT_IMAGE_SIZE);
                    }
                    else
                    {
                        bytesRead = clientStream.Read(serverMessage, 0, serverMessage.Length);
                    }
                }
                catch
                {
                    //a socket error has occured
                    Console.WriteLine("Socket error has occured");
                }

                if (bytesRead == 0)
                {
                    //the client has disconnected from the server
                    Console.WriteLine("Player has disconnected from the server");
                }

                //If the background has not been received yet
                //Copy it and store it for future use
                if (!backgroundReceived)
                {
                    backgroundImage = new byte[backgroundImageData.Length];
                    Array.Copy(backgroundImageData, backgroundImage, backgroundImageData.Length);
                    backgroundReceived = true;
                    Console.WriteLine("Background received");
                    clientStream.Flush();
                    drawPlayer();
                }
                else if (encoder.GetString(serverMessage, 0, bytesRead) == "playerimage")
                {
                    clientStream.Flush();
                    bytesRead = clientStream.Read(xS, 0, 4);
                    xStart = BitConverter.ToInt32(xS, 0);

                    clientStream.Flush();
                    bytesRead = clientStream.Read(xE, 0, 4);
                    xEnd = BitConverter.ToInt32(xE, 0);

                    clientStream.Flush();
                    bytesRead = clientStream.Read(yS, 0, 4);
                    yStart = BitConverter.ToInt32(yS, 0);

                    clientStream.Flush();
                    bytesRead = clientStream.Read(yE, 0, 4);
                    yEnd = BitConverter.ToInt32(yE, 0);

                    clientStream.Flush();
                    byte[] byteImgSize = new byte[4];
                    bytesRead = clientStream.Read(byteImgSize, 0, 4);
                    int imgSize = BitConverter.ToInt32(byteImgSize, 0);

                    clientStream.Flush();
                    partialPlayerImage = new byte[imgSize];
                    playerImage = new byte[imgSize];

                    bytesRead = 0;
                    while (bytesRead != imgSize)
                    {
                        int tmpBytesRead = clientStream.Read(partialPlayerImage, 0, imgSize);
                        Buffer.BlockCopy(partialPlayerImage, 0, playerImage, bytesRead, tmpBytesRead);
                        bytesRead += tmpBytesRead;
                    }

                    drawPlayer();
                    clientStream.Write(buffer, 0, buffer.Length);
                    clientStream.Flush();
                    //Console.WriteLine("Image received, size of image: " + imgSize + "," + xStart + ", " + xEnd + ", " + yStart + ", " + yEnd);
                }
            }
        }

        void drawPlayer()
        {
            unsafe
            {
                Dispatcher.Invoke((Action)delegate
                {
                    serverImage.Lock();
                    int dataCounter = 0;
                    //Draw background image
                    for (int y = 0; y < TRANSMIT_HEIGHT; ++y)
                    {
                        byte* pDest = (byte*)serverImage.BackBuffer.ToPointer() + y * serverImage.BackBufferStride;
                        for (int x = 0; x < TRANSMIT_WIDTH; ++x, pDest += 4, dataCounter += 4)
                        {
                            pDest[0] = backgroundImage[dataCounter];
                            pDest[1] = backgroundImage[dataCounter + 1];
                            pDest[2] = backgroundImage[dataCounter + 2];
                            pDest[3] = backgroundImage[dataCounter + 3];
                        }
                    }

                    dataCounter = 0;
                    //WORKING CODE UNCOMMENT AFTER TESTING COMPRESSION
                    if (playerImage != null)
                    {
                        //Draw new updated image
                        for (int y = yStart; y < yEnd; ++y)
                        {
                            byte* pDest = (byte*)serverImage.BackBuffer.ToPointer() + (y * serverImage.BackBufferStride);
                            pDest += 4 * xStart;
                            for (int x = xStart; x < xEnd; ++x, pDest += 4, dataCounter += 4)
                            {
                                pDest[0] = playerImage[dataCounter];
                                pDest[1] = playerImage[dataCounter + 1];
                                pDest[2] = playerImage[dataCounter + 2];
                                pDest[3] = playerImage[dataCounter + 3];
                            }
                        }
                    }

                    serverImage.AddDirtyRect(new Int32Rect(0, 0, TRANSMIT_WIDTH, TRANSMIT_HEIGHT));
                    //Console.WriteLine("Draw Image now");
                    image1.Source = serverImage;
                    //imageDrawn = true;
                    serverImage.Unlock();
                });
            }
        }

        void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (!_clientThread.IsBusy)
            {
                _clientThread.RunWorkerAsync();
            }
            if (!_worker.IsBusy)
            {
                _worker.RunWorkerAsync();
            }
        }

        void client_updated(object sender, byte[] data)
        {
            Console.WriteLine("Draw Image");
            unsafe
            {
                serverImage.Lock();
                int dataCounter = 0;
                for (int y = 0; y < TRANSMIT_WIDTH; ++y)
                {
                    byte* pDest = (byte*)serverImage.BackBuffer.ToPointer() + y * serverImage.BackBufferStride;
                    for (int x = 0; x < TRANSMIT_HEIGHT; ++x, pDest += 4, dataCounter += 4)
                    {
                        pDest[0] = data[dataCounter];
                        pDest[1] = data[dataCounter + 1];
                        pDest[2] = data[dataCounter + 2];
                        pDest[3] = data[dataCounter + 3];
                    }
                }
                serverImage.Unlock();
            }
            image1.Source = serverImage;
        }
    }
}
