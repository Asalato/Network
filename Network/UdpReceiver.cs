//#############################################################################
//
//    UdpReceiver
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
    public class UdpReceiver
    {
        public Action<byte[]> OnDataReceived = delegate { };
        
        private bool _isReceivingMessage;
        private UdpClient _udpClient;
        private int _currentIndex = 0;
        
        public readonly Action<Exception> OnErrorAction = delegate { };

        public UdpReceiver()
        {
            
        }

        public void LaunchServer(IPEndPoint ipEndPoint)
        {
            _udpClient = new UdpClient(ipEndPoint);
        }

        public void StartProcess()
        {
            _isReceivingMessage = true;
            Task.Run(ReceivingLoop);
#if ENABLE_LOG
    #if UNITY_2018
            Debug.Log($"[UdpReceiver] UdpReceiver launched.");
    #else
            Console.Writeline($"[UdpReceiver] UdpReceiver launched.");
    #endif
#endif
        }

        public void Close()
        {
            _isReceivingMessage = false;
#if ENABLE_LOG
    #if UNITY_2018
            Debug.Log($"[UdpReceiver] UdpReceiver stop listening.");
    #else
            Console.WriteLine($"[UdpReceiver] UdpReceiver stop listening.");
    #endif
#endif
        }
        
        private async Task ReceivingLoop()
        {
            while (_isReceivingMessage)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    if (result.Buffer == null) continue;
                    
                    var buf = result.Buffer;

#if ENABLE_LOG
    #if UNITY_2018
                    Debug.Log($"[UdpReceiver] Received data. {buf.Length} bytes exist.");
    #else
                    Console.Writeline($"[UdpReceiver] Received data. {buf.Length} bytes exist.");
    #endif
#endif

                    var packetQueue = new PacketQueue();
                    packetQueue.Enqueue(buf, buf.Length);
                    
                    OnDataReceived.Invoke(packetQueue);
                }
                catch (Exception e)
                {
                    _isReceivingMessage = false;
                    
                    OnErrorAction.Invoke(e);
#if ENABLE_LOG
    #if UNITY_2018
                    Debug.LogError($"[UdpReceiver] Error : {e}");
    #else
                    Console.Writeline($"[UdpReceiver] Error : {e}");
    #endif
#endif
                }
            }
        }
    }
}