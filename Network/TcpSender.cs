//#############################################################################
//
//    TcpSender
//    Copyright (c) 2019 Seven3131
//    This software is released under the MIT License.
//    http://opensource.org/licenses/mit-license.php
//
//    If you uncomment following "ENABLE_LOG", debug message will be shown.
//#############################################################################

//#define ENABLE_LOG

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
#if UNITY_2018
using UnityEngine;
#endif

namespace Network
{
    public class TcpSender
    {
        private class TcpClientData
        {
            public IPEndPoint IPEndPoint;
            public TcpClient Client;
            public Stream Stream;
            public PacketQueue Queue;
        }

        public Action<Exception> OnErrorAction = delegate(Exception exception) { };

        private readonly ConcurrentQueue<TcpClientData> _queue = new ConcurrentQueue<TcpClientData>();

        private bool _isSendingMessage;
        
        private const int WRITE_TIMEOUT = 10000;
        
        public TcpSender()
        {
            _isSendingMessage = true;
            Task.Run(SendingLoop);
        }

        public void Close()
        {
            _isSendingMessage = false;
            
            while (_queue.Count != 0)
            {
                if(_queue.TryDequeue(out var data));
                Disconnect(data);
            }
        }

        /// <summary>
        /// Send string message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ipEndPoint"></param>
        public void SendMessage(byte[] message, IPEndPoint ipEndPoint)
        {
            var queue = new PacketQueue();
            queue.Enqueue(message, message.Length);
            
            var clientData = new TcpClientData()
            {
                IPEndPoint = ipEndPoint,
                Queue = queue
            };
            _queue.Enqueue(clientData);
        }
        
        private async Task SendingLoop()
        {
            while (_isSendingMessage)
            {
                try
                {
                    _queue.TryDequeue(out var data);
                    if (data == null) continue;
                        
                    var socket = data.Client;
                    if (data.Queue.Offset != 0)
                    {
                        if (socket == null)
                        {
                            var client = new TcpClient();
                                
                            try
                            {
                                client.Connect(data.IPEndPoint);
                            }
                            catch (Exception e)
                            {
                                //_queue.Enqueue(data);
                                OnErrorAction.Invoke(e);
#if ENABLE_LOG
    #if UNITY_2018
                                Debug.LogError($"[TcpSender] Error : {e}");
    #else
                                Console.WriteLine($"[TcpSender] Error : {e}");
    #endif
#endif
                                continue;
                            }

                            var stream = client.GetStream();
                            stream.WriteTimeout = WRITE_TIMEOUT;
                            
                            data.Client = client;
                            data.Stream = stream;
                        }

                        try
                        {
                            await SendData(data);
                        }
                        catch (Exception e)
                        {
                            _queue.Enqueue(data);
                            OnErrorAction.Invoke(e);

#if ENABLE_LOG
    #if UNITY_2018
                            Debug.LogError($"[TcpSender] Error : {e}");
    #else
                            Console.WriteLine($"[TcpSender] Error : {e}");
    #endif
#endif
                        }
                    }
                    else if (socket.Connected && socket.Client.Poll(1000, SelectMode.SelectWrite) &&
                             socket.Available == 0)
                    {
                        Disconnect(data);
                    }
                }
                catch (Exception e)
                {
                    _isSendingMessage = false;
                    OnErrorAction.Invoke(e);

#if ENABLE_LOG
    #if UNITY_2018
                    Debug.LogError($"[TcpSender] Error : {e}");
    #else
                    Console.WriteLine($"[TcpSender] Error : {e}");
    #endif
#endif
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Sending data with tcp network stream
        /// </summary>
        /// <remarks>
        /// Must be "thread safe"
        /// </remarks>>
        /// <param name="data"></param>
        private async Task SendData(TcpClientData data)
        {
            try
            {
                var size = data.Queue.Offset;
                var buf = new byte[size];
                var packetSize = data.Queue.Dequeue(ref buf, size);
                
                while (packetSize > 0)
                {
                    await data.Stream.WriteAsync(buf, 0, packetSize);
                    packetSize = data.Queue.Dequeue(ref buf, size);
#if ENABLE_LOG
    #if UNITY_2018
                    Debug.Log($"[TcpSender] TcpSender send : {buf.Length} bytes");
    #else
                    Console.WriteLine($"[TcpSender] TcpSender send : {buf.Length} bytes");
    #endif
#endif
                }
                
                Disconnect(data);
            }
            catch(Exception e)
            {
                OnErrorAction.Invoke(e);
                throw;
            }
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
            var client = data?.Client;
            if (client == null) return;
            if (!client.Connected || !client.Client.Connected) return;
            
            data.Stream.Close();
            data.Client.Close();
        }
    }
}