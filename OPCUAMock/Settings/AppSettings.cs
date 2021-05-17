using Newtonsoft.Json.Linq;
using OPCUAMock.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OPCUAMock
{
    public class AppSettings
    {

        public static AppSettings Load(string path)
        {
            return JObject.Parse(File.ReadAllText(path)).ToObject<AppSettings>();
        }

        public List<NodeToCreate> NodesToCreate { get; set; } = new List<NodeToCreate>();
    }
}
