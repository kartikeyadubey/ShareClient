using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;

namespace ShareClient
{
    class Client
    {
        #region Constants
        private const int IMAGE_WIDTH = 640;
        private const int IMAGE_HEIGHT = 480;
        private const int IMAGE_SIZE = 640 * 480 * 4;
        private const int DPI_X = 96;
        private const int DPI_Y = 96;
        private const int TRANSMIT_IMAGE_SIZE = 320 * 240 * 4;
        private const int TRANSMIT_WIDTH = 320;
        private const int TRANSMIT_HEIGHT = 240;
        #endregion

        #region Client variables
        private int portReceive;
        public NetworkStream clientStream;
        public bool connectedToServer = false;
        public WriteableBitmap serverImage;
        
        byte[] playerImage;
        byte[] backgroundImage;
        private int xStart;
        private int xEnd;
        private int yStart;
        private int yEnd;
                
        TcpClient client;
        IPEndPoint serverEndPoint;
        
        private const string ipAddress = "127.0.0.1";
        private bool backgroundReceived = false;
        byte[] partialPlayerImage;


        private byte[] COMPLETE_MESSAGE;
        private byte[] HELLO_MESSAGE;
        #endregion

        #region Receiving data variables
        ASCIIEncoding encoder;
        byte[] backgroundImageData;
        byte[] serverMessage;
        byte[] xS;
        byte[] xE;
        byte[] yS;
        byte[] yE;
        int bytesRead;
        #endregion

        public Client()
        {
            serverImage = new WriteableBitmap(TRANSMIT_WIDTH, TRANSMIT_HEIGHT, DPI_X, DPI_Y, PixelFormats.Bgra32, null);
            ASCIIEncoding enc = new ASCIIEncoding();
            COMPLETE_MESSAGE = enc.GetBytes("rcomplete");
            HELLO_MESSAGE = enc.GetBytes("hello");
            encoder = new ASCIIEncoding();
            backgroundImageData = new byte[TRANSMIT_IMAGE_SIZE];
            serverMessage = new byte[65535];
            xS = new byte[4];
            xE = new byte[4];
            yS = new byte[4];
            yE = new byte[4];
        }

        public void setPort(int port)
        {
            this.portReceive = port;
        }

        public byte[] getBackgroundImage()
        {
            return this.backgroundImage;
        }

        public byte[] getPlayerImage()
        {
            return this.playerImage;
        }

        public int getXStart()
        {
            return this.xStart;
        }

        public int getXEnd()
        {
            return this.xEnd;
        }

        public int getYStart()
        {
            return this.yStart;
        }

        public int getYEnd()
        {
            return this.yEnd;
        }

        public void connect()
        {
            client = new TcpClient();
            serverEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), portReceive);

            //Sleep for 5ms
            System.Threading.Thread.Sleep(5000);
            client.Connect(serverEndPoint);
            clientStream = client.GetStream();

            clientStream.Write(HELLO_MESSAGE, 0, HELLO_MESSAGE.Length);
            clientStream.Flush();
            Console.WriteLine("Client said hello");

            clientStream.Write(COMPLETE_MESSAGE, 0, COMPLETE_MESSAGE.Length);
            clientStream.Flush();
            connectedToServer = true;
        }

        public void receiveData()
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
                clientStream.Write(COMPLETE_MESSAGE, 0, COMPLETE_MESSAGE.Length);
                clientStream.Flush();
                //Console.WriteLine("Image received, size of image: " + imgSize + "," + xStart + ", " + xEnd + ", " + yStart + ", " + yEnd);
            }
        }
    }
}
