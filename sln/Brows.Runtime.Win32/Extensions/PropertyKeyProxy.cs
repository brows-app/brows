using Domore.Runtime.Win32;
using System;

namespace Brows.Extensions {
    internal class PropertyKeyProxy {
        public string Name { get; }
        public PropertyKey Agent { get; }
        public Tuple<Guid, int> ID { get; }

        public PropertyKeyProxy(string name, PropertyKey agent) {
            Name = name;
            Agent = agent;
            ID = Tuple.Create(Agent.FmtID, Agent.PID);
        }

        public PROPERTYKEY Native =>
            new PROPERTYKEY { fmtid = ID.Item1, pid = (uint)ID.Item2 };
    }
}
