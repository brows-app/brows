namespace Brows {
    internal class DriveComponentKey : IComponentResourceKey {
        public string For(string key) {
            return nameof(DriveEntry) + "_" + key;
        }
    }
}
