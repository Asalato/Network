//#############################################################################
//
//    UdpSender
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
    public class UdpSender
    {
        private bool _isSendingMessage;
        private readonly ConcurrentQueue<UdpClientData> _queue = new ConcurrentQueue<UdpClientData>();
        
        public Action<Exception> OnErrorAction = delegate { };
        
        private class UdpClientData
        {
            public IPEndPoint IPEndPoint;
            public PacketQueue Queue;
        }
        
        public UdpSender()
        {
            _isSendingMessage = true;
            Task.Run(SendingLoop);
        }

        public void Close()
        {
            _isSendingMessage = false;
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
            
            var clientData = new UdpClientData()
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
                    if (_queue.Count == 0) continue;

                    if (!_queue.TryDequeue(out var data) || data == null) continue;
                   
                    if (data.Queue.Offset != 0)
                    {
                        await SendData(data);
                    }
                }
                catch (Exception e)
                {
                    _isSendingMessage = false;
                    
                    OnErrorAction.Invoke(e);
#if ENABLE_LOG
    #if UNITY_2018
                    Debug.LogError($"[UdpSender] Error : {e}");
    #else
                    Console.WriteLine($"[UdpSender] Error : {e}");
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
        private async Task SendData(UdpClientData data)
        {
            try
            {
                var size = data.Queue.Offset;
                var buf = new byte[size];
                var packetSize = data.Queue.Dequeue(ref buf, size);

                using (var client = new UdpClient())
                {
                    while (packetSize > 0)
                    {
#if ENABLE_LOG
    #if UNITY_2018
                        Debug.Log($"[UdpSender] UdpSender send : {buf.Length} bytes");
    #else
                        Console.WriteLine($"[UdpSender] UdpSender send : {buf.Length} bytes");
    #endif
#endif
                        await client.SendAsync(buf, packetSize, data.IPEndPoint);
                        packetSize = data.Queue.Dequeue(ref buf, size);
                    }

                    client.Close();
                }
            }
            catch(Exception e)
            {
                OnErrorAction.Invoke(e);
                
#if ENABLE_LOG
    #if UNITY_2018
                Debug.LogError($"[UdpSender] Error : {e}");
    #else
                Console.WriteLine($"[UdpSender] Error : {e}");
    #endif
#endif
            }
        }
    }
}