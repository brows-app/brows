using System.Collections.ObjectModel;
using System.Linq;

namespace Brows {
    internal class OperationInfo {
        private ReadOnlyCollection<string> DescriptionDataCollection { get; }

        public string Name { get; }
        public string DescriptionFormat { get; }
        public string[] DescriptionData => DescriptionDataCollection.ToArray();
        public string Description => string.Format(DescriptionFormat, DescriptionData);

        public OperationInfo(string name, string descriptionFormat, params string[] descriptionData) {
            Name = name;
            DescriptionFormat = descriptionFormat;
            DescriptionDataCollection = new ReadOnlyCollection<string>(descriptionData);
        }
    }
}
