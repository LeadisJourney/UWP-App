using System;
using System.Collections;
using System.Collections.Generic;

namespace Leadis_Journey
{
    public class StateString<T, DefaultCtor>
        where DefaultCtor : T, new()
    {
        private string text;
        private List<T> states;

        public StateString()
        {
            this.text = string.Empty;
            this.states = new List<T>();
        }

        static private IEnumerable<T> InitStates(int n)
        {
            while (--n >= 0)
                yield return new DefaultCtor();
            yield break;
        }

        public void Insert(int where, string text)
        {
            this.text = this.text.Insert(where, text);
            this.states.InsertRange(where, InitStates(text.Length));
        }

        public void Remove(int where, int count)
        {
            this.text = this.text.Remove(where, count);
            this.states.RemoveRange(where, count);
        }

        public void Set(string text)
        {
            this.text = text;
            this.states = new List<T>(InitStates(text.Length));
        }

        public string Chars
        {
            get
            {
                return this.text;
            }
        }

        public IReadOnlyList<T> States
        {
            get
            {
                return this.states.AsReadOnly();
            }
        }

        public int Length
        {
            get
            {
                return this.text.Length;
            }
        }
    }
}
