//#############################################################################
//
//    TcpReceiver
//    Copyright (c) 2019 Seven3131
//    This software is released under the MIT License.
//    http://opensource.org/licenses/mit-license.php
//
//    If you uncomment following "ENABLE_LOG", debug message will be shown.
//#############################################################################

//#define ENABLE_LOG

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
#if UNITY_2018
using UnityEngine;
#endif

namespace Network
{
    public class TcpReceiver
    {
        public Action<byte[]> OnDataReceived = delegate { };
        
        private class TcpClientData
        {
            public TcpClient Client;
            public NetworkStream Stream;
        }

        private readonly ConcurrentQueue<TcpClientData> _connectionQueue = new ConcurrentQueue<TcpClientData>();
        
        private TcpListener _tcpListener;
        private IPEndPoint _ipEndPoint;

        private bool _isLaunchServer;
        private bool _isFindConnection;
        private bool _isReceivingMessage;

        private const int RECEIVE_BUFFER_SIZE = 150000;
        private const int READ_TIMEOUT = 10000;
        
        public readonly Action<Exception> OnErrorAction = delegate { };

        public void LaunchServer(IPEndPoint ipEndPoint)
        {
            if (_isLaunchServer) return;

            try
            {
                _tcpListener = new TcpListener(ipEndPoint);
                
                _tcpListener.Start();
                _isLaunchServer = true;

#if ENABLE_LOG
    #if UNITY_2018
                Debug.Log($"[TcpReceiver] TcpReceiver launched.");
    #else
                Console.WriteLine($"[TcpReceiver] TcpReceiver launched.");
    #endif
#endif
            }
            catch (Exception e)
            {
                OnErrorAction.Invoke(e);
                
#if ENABLE_LOG
    #if UNITY_2018
                Debug.Log($"[TcpReceiver] Error : {e}");
    #else
                Console.WriteLine($"[TcpReceiver] Error : {e}");
    #endif
#endif
                throw;
            }
        }

        public void StartProcess()
        {
            if (!_isLaunchServer) return;

            // Start find connection
            if (!_isFindConnection)
            {
                _isFindConnection = true;

#if ENABLE_LOG
    #if UNITY_2018
                Debug.Log($"[TcpReceiver] TcpReceiver start listening.");
    #else
                Console.WriteLine($"[TcpReceiver] TcpReceiver start listening.");
    #endif
#endif

                Task.Run(FindConnection);
            }

            if (!_isReceivingMessage)
            {
                _isReceivingMessage = true;
                
#if ENABLE_LOG
    #if UNITY_2018
                Debug.Log($"[TcpReceiver] TcpReceiver accept receiving data.");
    #else
                Console.WriteLine($"[TcpReceiver] TcpReceiver accept receiving data.");
    #endif
#endif

                Task.Run(ReceivingLoop);
            }
        }

        public void StopProcess()
        {
            if (!_isLaunchServer) return;

            if (_isFindConnection)
            {
                // Stop find connection
                _isFindConnection = false;

#if ENABLE_LOG
    #if UNITY_2018
                Debug.Log($"[TcpReceiver] TcpReceiver stop listening.");
    #else
                Console.WriteLine($"[TcpReceiver] TcpReceiver stop listening.");
    #endif
#endif
            }

            if (_isReceivingMessage)
            {
                _isReceivingMessage = false;
                
#if ENABLE_LOG
    #if UNITY_2018
            Debug.Log($"[TcpReceiver] TcpReceiver ignore receiving data.");
    #else
            Console.WriteLine($"[TcpReceiver] TcpReceiver ignore receiving data.");
    #endif
#endif 
            }
        }

        public void Close()
        {
            if (!_isLaunchServer) return;

            StopProcess();

            while (_connectionQueue.Count > 0)
            {
                _connectionQueue.TryDequeue(out var data);
                if (data != null)
                    Disconnect(data);
            }
            
#if ENABLE_LOG
    #if UNITY_2018
            Debug.Log($"[TcpReceiver] TcpReceiver stopped.");
    #else
            Console.WriteLine($"[TcpReceiver] TcpReceiver stopped.");
    #endif
#endif

            _isLaunchServer = false;
        }

        /// <summary>
        /// Disconnect from network stream
        /// </summary>
        /// <remarks>
        /// Must be "thread safe"
        /// </remarks>>
        /// <param name="data"></param>
        private static void Disconnect(TcpClientData data)
        {
            var client = data.Client;
            if (!client.Connected || !client.Client.Connected) return;
            
            data.Stream.Close();
            data.Client.Close();
        }

        /// <summary>
        /// Try finding & connection to TcpSender
        /// </summary>
        /// <returns></returns>
        private async Task FindConnection()
        {
            while (_isFindConnection)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    
                    if (!client.Connected) continue;
                    
                    var stream = client.GetStream();
                    stream.ReadTimeout = READ_TIMEOUT;
                    
                    var data = new TcpClientData()
                    {
                        Client = client,
                        Stream = stream
                    };

#if ENABLE_LOG
    #if UNITY_2018
                    Debug.Log($"[TcpReceiver] Connected client.");
    #else
                    Console.WriteLine($"[TcpReceiver] Connected client.");
    #endif
#endif
                    
                    _connectionQueue.Enqueue(data);
                }
                catch (ObjectDisposedException e)
                {
                    _isFindConnection = false;
                    
                    OnErrorAction.Invoke(e);
#if ENABLE_LOG
    #if UNITY_2018
                    Debug.LogError($"[TcpReceiver] Error : {e}");
    #else
                    Console.WriteLine($"[TcpReceiver] Error : {e}");
    #endif
#endif
                }
            }
        }
        
        private async Task ReceivingLoop()
        {
            while (_isReceivingMessage)
            {
                try
                {
                    _connectionQueue.TryDequeue(out var data);
                    if (data == null) continue;
                    
                    var client = data.Client;
                    if (client != null && client.Connected)
                    {
                        var socket = client.Client;
                        if (socket.Connected && socket.Poll(1000, SelectMode.SelectRead) &&
                            socket.Available == 0)
                        {
                            Disconnect(data);
                        }
                        else
                        {
                            ReceiveData(data);
                        }
                    }
                }
                catch (Exception e)
                {
                    _isReceivingMessage = false;
                    
                    OnErrorAction.Invoke(e);
#if ENABLE_LOG
    #if UNITY_2018
                    Debug.LogError($"[TcpReceiver] Error : {e}");
    #else
                    Console.WriteLine($"[TcpReceiver] Error : {e}");
    #endif
#endif
                }
            }
        }

        /// <summary>
        /// Receiving Data from Network stream
        /// </summary>
        /// <remarks>
        /// Must be "thread safe"
        /// </remarks>>
        /// <param name="data"></param>
        private void ReceiveData(TcpClientData data)
        {
            var packetQueue = new PacketQueue();
            while (data.Client.Client.Poll(1000, SelectMode.SelectRead))
            {
                var packetSize = 0;
                var buf = new byte[RECEIVE_BUFFER_SIZE];
                
                try
                {
                    packetSize = data.Stream.Read(buf, 0, buf.Length);
                }
                catch (Exception e)
                {
                    OnErrorAction.Invoke(e);
                    
#if ENABLE_LOG
    #if UNITY_2018
                    Debug.LogError($"[TcpReceiver] Error : {e}");
    #else
                    Console.WriteLine($"[TcpReceiver] Error : {e}");
    #endif
#endif
                }

                if (packetSize == 0)
                {
                    break;
                }
                else
                {
                    packetQueue.Enqueue(buf, packetSize);
                }
            }
            
            Disconnect(data);
            
            if (packetQueue.Offset > 0)
            {
                // Finish receiving
#if ENABLE_LOG
    #if UNITY_2018
                Debug.Log($"[TcpReceiver] Received data. {packetQueue.Offset} bytes exist.");
    #else
                Console.WriteLine($"[TcpReceiver] Received data. {packetQueue.Offset} bytes exist.");
    #endif
#endif
                
                OnDataReceived.Invoke(packetQueue);
            }
        }
    }
}