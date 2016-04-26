using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leadis_Journey
{
    public enum TokenType
    {
        None,
        Macro,
        Directive,
        LitString,
        LitChar,
        LitNumber,
        Keyword,
        Error,
    }

    public struct Token
    {
        public int Begin;
        public int End;
        public TokenType Type;

        public Token(int begin, int end, TokenType type)
        {
            this.Begin = begin;
            this.End = end;
            this.Type = type;
        }
    }

    public interface IHighlighter : IDisposable
    {
        void RemoveText(int where, int len);
        void InsertText(int where, string text);
        void SetText(string text);
        bool TryGetToken(out Token result);
    }
}
