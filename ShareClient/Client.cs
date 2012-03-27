using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace ShareClient
{
    class Client
    {
        public Client()
        {
            TcpClient client = new TcpClient();

            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3001);

            client.Connect(serverEndPoint);

            NetworkStream clientStream = client.GetStream();

            ASCIIEncoding encoder = new ASCIIEncoding();
            byte[] buffer = encoder.GetBytes("Hello Server!");

            clientStream.Write(buffer, 0, buffer.Length);
            clientStream.Flush();
            Console.WriteLine("Client said Hello Server!");

            int len = 640 * 480 * 4;
            byte[] imgData = new byte[len];
            int bytesRead;
            while (true)
            {
                bytesRead = 0;

                try
                {
                    //blocks until a server replies with a message
                    bytesRead = clientStream.Read(imgData, 0, len);
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

                Console.WriteLine("Client received image data!");
                Console.WriteLine(imgData[10]);
                if (updated != null)
                {
                    updated(this, imgData);
                }
                
                clientStream.Flush();
                client.Close();

            }
        }

        #region Events
        public delegate void ImageUpdatedEventHandler(object sender, byte[] data);
        public event ImageUpdatedEventHandler updated;
        #endregion
    }
}
