namespace Brows {
    public class CommandContextFlag : ICommandContextFlag {
        public bool PersistInput { get; set; }
        public bool RefreshInput { get; set; }
        public int SelectInputStart { get; set; }
        public int SelectInputLength { get; set; }
        public string SetInput { get; set; }
    }
}
