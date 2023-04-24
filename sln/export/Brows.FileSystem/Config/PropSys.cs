namespace Brows.Config {
    internal class PropSys {
        public PropCollection Prop {
            get => _Prop ?? (_Prop = new PropCollection());
            set => _Prop = value;
        }
        private PropCollection _Prop;
    }
}
