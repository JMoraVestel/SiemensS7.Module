using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vNode.SiemensS7.ChannelConfig
{
    public class InvalidChannelConfigException : ApplicationException
    {
        public InvalidChannelConfigException(Exception ex) : base("Invalid Channel Configuration", ex)
        {
        }
    }
}
