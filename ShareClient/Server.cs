using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Windows.Media.Imaging;
using System.Net;
using System.Windows;

namespace ShareClient
{
    class Server
    {
        #region Constants
        private const int TRANSMIT_IMAGE_SIZE = 320 * 240 * 4;
        private const int TRANSMIT_WIDTH = 320;
        private const int TRANSMIT_HEIGHT = 240;
        #endregion

        #region Server Variables
        Socket s;
        public bool clientConnected = false;
        bool backgroundSent = false;
        WriteableBitmap clientImage;
        int imageSendCounter = 1;
        private int portSend;
        public NetworkStream clientStream;
        #endregion

        public void setPort(int port)
        {
            this.portSend = port;
        }

        //Connect to the client
        public void connect()
        {
            if (!clientConnected)
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
                    Console.WriteLine("Connection accepted from " + s.RemoteEndPoint);
                    byte[] b = new byte[65535];
                    int k = s.Receive(b);
                    ASCIIEncoding enc = new ASCIIEncoding();
                    Console.WriteLine("Received:" + enc.GetString(b, 0, k) + "..");
                    //Ensure the client is who we want
                    if (enc.GetString(b, 0, k) == "hello" || enc.GetString(b, 0, k) == "hellorcomplete")
                    {
                        clientConnected = true;
                        Console.WriteLine(enc.GetString(b, 0, k));
                    }
                }
            }
        }

        public void sendData(WriteableBitmap b, bool playerFound, int xStart, int xEnd, int yStart, int yEnd)
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
                if (backgroundSent && playerFound && imageSendCounter%3 == 0)
                {
                    //Send the client a flag
                    ASCIIEncoding encoder = new ASCIIEncoding();
                    byte[] buffer = encoder.GetBytes("playerimage");

                    byte[] readyToReceive = encoder.GetBytes("rcomplete");
                    clientStream.Write(readyToReceive, 0, readyToReceive.Length);
                    clientStream.Flush();
                    byte[] completeMessage = new byte[65536];

                    int k = s.Receive(completeMessage);
                    while (k == 0)
                    {
                        k = s.Receive(completeMessage);
                    }
                    //Get the string from the first 9 bytes since
                    //rcomplete may have been appended to ensure there is no deadlock
                    string tempMessage = encoder.GetString(completeMessage, 0, 9);
                    if (!tempMessage.Equals("rcomplete"))
                    {
                        Console.WriteLine("Message received: " + encoder.GetString(completeMessage, 0, k));
                        return;
                    }

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


                    s.Send(buffer);

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
    }
}
