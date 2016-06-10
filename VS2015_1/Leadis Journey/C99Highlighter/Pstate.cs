using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leadis_Journey
{
    public partial class C99Highlighter : IHighlighter
    {
        enum Ltype
        {
            Eof = -1,
            Unknown,
            Identifier,
            KwAuto,
            KwBreak,
            KwCase,
            KwChar,
            KwConst,
            KwContinue,
            KwDefault,
            KwDo,
            KwDouble,
            KwElse,
            KwEnum,
            KwExtern,
            KwFloat,
            KwFor,
            KwGoto,
            KwIf,
            KwInt,
            KwLong,
            KwRegister,
            KwReturn,
            KwShort,
            KwSigned,
            KwSizeof,
            KwStatic,
            KwStruct,
            KwSwitch,
            KwTypedef,
            KwUnion,
            KwUnsigned,
            KwVoid,
            KwVolatile,
            KwWhile,
            KwBool,
            KwComplex,
            KwImaginary,
            KwInline,
            KwRestrict,
            Lacc,
            Racc,
            Semic,
        }

        struct Lexeme
        {
            public readonly Ltype Type;
            public readonly string Str;

            public Lexeme(Ltype type, string str)
            {
                this.Type = type;
                this.Str = str;
            }
        }

        enum Pstate
        {
            None,
            Enum,
            Id,
        }

        interface ITk
        {
            Pstate State { get; }
            Stack<ITk> Scope { get; }
        }

        class NoneTk : ITk
        {
            public Pstate State { get { return Pstate.None; } }
            public Stack<ITk> Scope { get { return null; } }
        }

        class EnumTk : ITk
        {
            public readonly string Name;

            public Pstate State { get { return Pstate.Enum; } }
            public Stack<ITk> Scope { get { return null; } }

            public EnumTk(string name)
            {
                this.Name = name;
            }
        }

        class IdTk : ITk
        {
            public readonly string Name;
            private readonly Stack<ITk> scope;

            public Pstate State { get { return Pstate.Enum; } }
            public Stack<ITk> Scope { get { return this.scope; } }

            public IdTk(string name, Stack<ITk> scope)
            {
                this.Name = name;
                this.scope = scope;
            }
        }
    }
}
