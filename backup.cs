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
        WriteableBitmap image;
        private const int size = 640 * 480 * 4;
        private const int WIDTH = 640;
        private const int HEIGHT = 480;
        private const int DPI_X = 96;
        private const int DPI_Y = 96;
        private bool imageDrawn = false;
        BackgroundWorker clientThread;
        private const int port = 3000;
        private bool backgroundReceived = false;
        byte[] backgroundImage;
        byte[] playerImage;

        private int xStart;
        private int xEnd;
        private int yStart;
        private int yEnd;

        public MainWindow()
        {
            InitializeComponent();

            CompositionTarget.Rendering += new EventHandler(CompositionTarget_Rendering);

            clientThread = new BackgroundWorker();
            clientThread.DoWork += new DoWorkEventHandler(clientThread_DoWork);
            
            image = new WriteableBitmap(WIDTH, HEIGHT, DPI_X, DPI_Y, PixelFormats.Bgra32, null);
        }

        void clientThread_DoWork(object sender, DoWorkEventArgs e)
        {
            if (!imageDrawn)
            {
                TcpClient client = new TcpClient();

                IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);

                client.Connect(serverEndPoint);

                NetworkStream clientStream = client.GetStream();

                ASCIIEncoding encoder = new ASCIIEncoding();
                byte[] buffer = encoder.GetBytes("hello");

                clientStream.Write(buffer, 0, buffer.Length);
                clientStream.Flush();
                Console.WriteLine("Client said hello");

                byte[] backgroundImageData = new byte[size];
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
                        //blocks until a server replies with a message
                        if (!backgroundReceived)
                        {
                            bytesRead = clientStream.Read(backgroundImageData, 0, size);
                        }
                        else
                        {
                            bytesRead = clientStream.Read(serverMessage, 0, serverMessage.Length);
                        }
                    }
                    catch
                    {
                        //a socket error has occured
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        //the client has disconnected from the server
                        break;
                    }

                    //message has successfully been received
                    //System.Diagnostics.Debug.WriteLine(encoder.GetString(message, 0, bytesRead));

                    //Console.WriteLine("Client received image data!");
                    //Console.WriteLine("Image data containts" + imgData[10]);
                    //imgData = Compressor.Decompress(imgData);
                    
                    //If the background has not been received yet
                    //Copy it and store it for future use
                    if (!backgroundReceived)
                    {
                        backgroundImage = new byte[backgroundImageData.Length];
                        Array.Copy(backgroundImageData, backgroundImage, backgroundImageData.Length);
                        backgroundReceived = true;
                        Console.WriteLine("Background received");
                        clientStream.Flush();
                    }
                    else if (encoder.GetString(serverMessage, 0, bytesRead) == "playerimage")
                    {
                        Console.WriteLine("Player image received by the client");
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

                        playerImage = new byte[imgSize];
                        bytesRead = clientStream.Read(playerImage, 0, imgSize);
                    }

                    Dispatcher.BeginInvoke((Action)delegate
                    {
                        unsafe
                        {
                            image.Lock();
                            int dataCounter = 0;

                            //Draw background image
                            for (int y = 0; y < HEIGHT; ++y)
                            {
                                byte* pDest = (byte*)image.BackBuffer.ToPointer() + y * image.BackBufferStride;
                                for (int x = 0; x < WIDTH; ++x, pDest += 4, dataCounter += 4)
                                {
                                    pDest[0] = backgroundImage[dataCounter];
                                    pDest[1] = backgroundImage[dataCounter + 1];
                                    pDest[2] = backgroundImage[dataCounter + 2];
                                    pDest[3] = backgroundImage[dataCounter + 3];
                                }
                            }

                            dataCounter = 0;

                            //for (int y = 0; y < HEIGHT; ++y)
                            //{
                            //    byte* pDest = (byte*)image.BackBuffer.ToPointer() + y * image.BackBufferStride;
                            //    for (int x = 0; x < WIDTH; ++x, pDest += 4, dataCounter += 4)
                            //    {
                            //        pDest[0] = imgData[dataCounter];
                            //        pDest[1] = imgData[dataCounter + 1];
                            //        pDest[2] = imgData[dataCounter + 2];
                            //        pDest[3] = imgData[dataCounter + 3];
                            //    }
                            //}

                            //Draw new updated image
                            for (int y = 0; y < (yEnd - yStart); ++y)
                            {
                                byte* pDest = (byte*)image.BackBuffer.ToPointer() + y * image.BackBufferStride;
                                for (int x = 0; x < (yEnd - yStart); ++x, pDest += 4, dataCounter += 4)
                                {
                                    //pDest[0] = playerImage[dataCounter];
                                    //pDest[1] = playerImage[dataCounter + 1];
                                    //pDest[2] = playerImage[dataCounter + 2];
                                    //pDest[3] = playerImage[dataCounter + 3];


                                    pDest[0] = 0;
                                    pDest[1] = 0;
                                    pDest[2] = 0;
                                    pDest[3] = 255;
                                }
                            }
                            image.AddDirtyRect(new Int32Rect(0, 0, WIDTH, HEIGHT));
                            
                        }
                        //Console.WriteLine("Draw Image now");
                        image1.Source = image;
                        //imageDrawn = true;
                        image.Unlock();
                    });

                    clientStream.Flush();
                }
            }
        }

        void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (!clientThread.IsBusy)
            {
                clientThread.RunWorkerAsync();
            }
        }

        void client_updated(object sender, byte[] data)
        {
            Console.WriteLine("Draw Image");
            unsafe
            {
                image.Lock();
                int dataCounter = 0;
                for (int y = 0; y < WIDTH; ++y)
                {
                    byte* pDest = (byte*)image.BackBuffer.ToPointer() + y * image.BackBufferStride;
                    for (int x = 0; x < HEIGHT; ++x, pDest += 4, dataCounter += 4)
                    {
                        pDest[0] = data[dataCounter];
                        pDest[1] = data[dataCounter + 1];
                        pDest[2] = data[dataCounter + 2];
                        pDest[3] = data[dataCounter + 3];
                    }
                }
                image.Unlock();
            }
            image1.Source = image;
        }
    }
}

