using System;
using System.Net;
using System.Text;

namespace Network
{
    public static class Util
    {
        /// <summary>
        /// Return Current IPAddress
        /// </summary>
        /// <returns></returns>
        public static IPAddress SetAddress()
        {
            IPAddress addr;

            try
            {
                var hostname = Dns.GetHostName();
                var addr_arr = Dns.GetHostAddresses(hostname);

                addr = IPAddress.None;
                foreach ( var add in addr_arr )
                {
                    var addr_str = add.ToString();
                    
                    if ( addr_str.IndexOf( ".", StringComparison.Ordinal) > 0 && !addr_str.StartsWith( "127." ) )
                    {
                        addr = add;
                        break;
                    }
                }
            }
            catch (Exception)
            {
                addr = IPAddress.None;
            }

            return addr;
        }
    }
}