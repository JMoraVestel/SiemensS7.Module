using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vNode.SiemensS7.TagConfig;

namespace vNode.SiemensS7.Scheduler
{
    public class TagReadBatchItem
    {
        public TagReadBatchItem(SiemensTagWrapper tag, DateTime readDueTime)
        {
            Tag = tag;
            ReadDueTime = readDueTime;
        }

        public string Address => Tag.Config.Address; // Dirección del tag en el PLC

        public DateTime ReadDueTime { get; set; }
        public DateTime ActualReadTime { get; set; }

        public SiemensTagWrapper Tag { get; private set; }

        public ushort Size => Tag.Config.GetSize(); // Tamaño del tag basado en su configuración
    }
}
