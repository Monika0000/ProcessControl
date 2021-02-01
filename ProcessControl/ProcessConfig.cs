using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ProcessControl {
    //public enum BoolEnum { True, False }

    public enum Config
    {
        Unknown, Close, VeryLow, Low, Normal, High, VeryHigh, RealTime
    }

    public enum ProcessSort
    {
        Name, Priority, System
    }

    public class ProcessConfig {
        public ProcessConfig(string name, Config priority, bool system)
        {
            ProcessName = name;
            Priority = priority;
            System = system;
        }

        public string ProcessName { get; set; } = "Unnamed";
        public Config Priority { get; set; } = Config.Normal;
        public bool System { get; set; } = false;
    }
}
