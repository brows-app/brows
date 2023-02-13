namespace Brows {
    using Composition.Hosting;

    internal class ProgramContext : IProgramContext {
        private Composer Composer =>
            _Composer ?? (
            _Composer = new Composer());
        private Composer _Composer;

        public IProgramComposition Composition =>
            _Composition ?? (
            _Composition = Composer.ProgramComposition());
        private IProgramComposition _Composition;

        public IProgramCommand Command { get; }
        public IProgramConsole Console { get; }

        public ProgramContext(IProgramCommand command, IProgramConsole console) {
            Command = command;
            Console = console;
        }
    }
}
