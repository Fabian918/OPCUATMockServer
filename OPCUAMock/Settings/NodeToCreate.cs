using System;
using System.Collections.Generic;
using System.Text;

namespace OPCUAMock.Settings
{
    public class NodeToCreate
    {
        public string Id { get; set; }

        public string Path_s { get; set; }

        public uint Path_i { get; set; }

        public string Name { get; set; }
    }
}
