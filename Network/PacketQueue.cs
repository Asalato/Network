//#############################################################################
//
//    PacketQueue
//    Copyright (c) 2019 Seven3131
//    This software is released under the MIT License.
//    http://opensource.org/licenses/mit-license.php
//
//#############################################################################

using System;
using System.Collections.Generic;
using System.IO;

namespace Network
{
    public class PacketQueue
    {
        private struct Information
        {
            public int offset;
            public int size;
            public Information(int offset, int size)
            {
                this.offset = offset;
                this.size = size;
            }
        };

        private readonly MemoryStream _stream = new MemoryStream();
        private readonly List<Information> _informationList = new List<Information>();

        public int DataSize => _stream.Length;
        
        public int Enqueue(byte[] data, int size)
        {
            _informationList.Add(new Information(DataSize, size));
            
            _stream.Position = DataSize;
            _stream.Write(data, 0, size);
            _stream.Flush();

            return size;
        }

        public int Dequeue(ref byte[] buffer, int size)
        {
            if (_informationList.Count <= 0) return -1;

            var info = _informationList[0];
            
            var dataSize = size > info.size ? info.size : size;
            _stream.Position = info.offset;
            var recvSize = _stream.Read(buffer, 0, dataSize);
            
            if (recvSize > 0)
            {
                _informationList.RemoveAt(0);
            }

            return recvSize;
        }
    }
}