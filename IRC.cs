using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace TwitchPlaysBot
{
    public delegate void ConnectionStatus(bool isConnected, bool hasJoinedChannel);
    public delegate void DataReceived(string data);
    public delegate void MessageReceived(string username, string message);

    class IRC
    {
        private string server;
        private int port;
        private TcpClient connection;
        private NetworkStream network;
        private StreamReader reader;
        private StreamWriter writer;

        private Listener listener;
        private Thread listenThread;

        public event ConnectionStatus eventConnectionStatus;
        public event DataReceived eventRecievingData;
        public event MessageReceived eventRecievingMessage;

        public bool IsConnected
        {
            get { return (connection != null) ? connection.Connected : false; }
        }
        public bool hasJoinedChannel;

        public IRC(string server, int port)
        {
            this.server = server;
            this.port = port;
        }

        public void Connect()
        {
            System.Diagnostics.Debug.WriteLine("Connecting to Twitch IRC...");

            try
            {
                // initialise IRC connection
                connection = new TcpClient(server, port);
                network = connection.GetStream();
                reader = new StreamReader(network, Encoding.UTF8);
                writer = new StreamWriter(network, Encoding.UTF8) { NewLine = "\r\n", AutoFlush = true };

                // notify watchers that the connection status has changed
                notifyConnectionStatusChange();

                System.Diagnostics.Debug.WriteLine("Creating a new listen thread...");

                // read data coming from IRC in a separate thread
                listener = new Listener(this);
                listenThread = new Thread(() => listener.Listen()) { IsBackground = true };
                listenThread.Start();

                // decrypt user password
                string password;
                IntPtr valuePtr = IntPtr.Zero;
                try
                {
                    valuePtr = Marshal.SecureStringToGlobalAllocUnicode(Properties.Settings.Default.Password);
                    password = Marshal.PtrToStringUni(valuePtr);
                }
                finally
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
                }

                System.Diagnostics.Debug.WriteLine("Authenticating...");

                // authenticate and join the channel
                writer.WriteLine();
                writer.WriteLine(String.Format("USER {0} 0 * :TwitchPlaysBot", Properties.Settings.Default.Username.ToLower()));
                writer.WriteLine(String.Format("PASS {0}", password));
                writer.WriteLine(String.Format("NICK {0}", Properties.Settings.Default.Username.ToLower()));
                writer.WriteLine(String.Format("JOIN {0}", Properties.Settings.Default.Channel));
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
        }

        public void SendMessage(string message)
        {
            if (IsConnected && hasJoinedChannel && message.Length > 0)
            {
                writer.WriteLine(String.Format("PRIVMSG {0} :{1}", Properties.Settings.Default.Channel, message));
            }
        }

        public void Disconnect()
        {
            System.Diagnostics.Debug.WriteLine("Closing sockets...");

            hasJoinedChannel = false;
            listener.Stop();
            writer.Close();
            reader.Close();
            connection.Close();
            notifyConnectionStatusChange();

            System.Diagnostics.Debug.WriteLine("listenThread isAlive: " + listenThread.IsAlive);
            System.Diagnostics.Debug.WriteLine("Disconnected");
        }

        private void notifyConnectionStatusChange()
        {
            if (eventConnectionStatus != null) { eventConnectionStatus(IsConnected, hasJoinedChannel); }
        }

        private class Listener
        {
            private IRC irc;
            private volatile bool listening;

            public Listener(IRC irc)
            {
                this.irc = irc;
            }

            public void Listen()
            {
                listening = true;

                while (listening)
                {
                    try
                    {
                        string data = "";

                        while ((data = irc.reader.ReadLine()) != null)
                        {
                            string nick = "";
                            string type = "";
                            string channel = "";
                            string message = "";
                            string[] parts = data.Split(new char[] { ' ' }, 4);

                            // notify of event for receiving data
                            if (irc.eventRecievingData != null) { irc.eventRecievingData(data); }

                            // remove the leading colon
                            if (parts[0].Substring(0, 1) == ":")
                            {
                                parts[0] = parts[0].Remove(0, 1);
                            }

                            // play ping pong
                            if (parts[0] == "PING")
                            {
                                irc.writer.WriteLine(String.Format("PONG {0}", parts[1]));
                            }
                            // server message
                            else if (parts[0] == "tmi.twitch.tv" || parts[0] == Properties.Settings.Default.Username.ToLower() + ".tmi.twitch.tv")
                            {

                            }
                            // normal message
                            else
                            {
                                nick = parts[0].Split('!')[0];
                                type = parts[1];
                                channel = parts[2];
                                if (parts.Length == 4)
                                {
                                    // remove the colon at the beginning
                                    message = parts[3].Remove(0, 1);
                                }

                                // received a user message from the actual channel
                                if (type == "PRIVMSG" && channel.StartsWith("#"))
                                {
                                    if (irc.eventRecievingMessage != null) { irc.eventRecievingMessage(nick, message); }
                                }
                                // joined the channel successfully
                                else if (type == "JOIN" && channel == Properties.Settings.Default.Channel.ToLower())
                                {
                                    irc.hasJoinedChannel = true;
                                    irc.notifyConnectionStatusChange();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(e.ToString());
                    }
                }

                System.Diagnostics.Debug.WriteLine("End of listen loop");
            }

            public void Stop()
            {
                listening = false;
            }
        }
    }
}
