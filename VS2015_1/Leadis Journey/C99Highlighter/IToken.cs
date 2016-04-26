namespace Leadis_Journey
{
    public sealed partial class C99Highlighter : IHighlighter
    {
        private enum Pstate
        {
            None           = 0x00000000,
            NoneNoPP       = 0x00000001,
            JumpToDefineAE = 0x00000002,
            JumpToUndefAE  = 0x00000003,
            Comment        = 0x10000000,
        }

        private interface IToken
        {
            Pstate State { get; }
        }

        private class NoneToken : IToken
        {
            public Pstate State { get { return Pstate.None; } }
        }
    }
}
