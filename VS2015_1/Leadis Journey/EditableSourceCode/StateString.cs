using System;
using System.Collections;
using System.Collections.Generic;

namespace Leadis_Journey
{
    public class StateString<T>
        : IComparable
        , IComparable<string>
        , IEnumerable<char>
        , IEquatable<string>
        where T : new()
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
                yield return new T();
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

        #region Interfaces implementation
        public int CompareTo(string other)
        {
            return text.CompareTo(other);
        }

        public bool Equals(string other)
        {
            return text.Equals(other);
        }

        public IEnumerator<char> GetEnumerator()
        {
            return (text as IEnumerable<char>).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (text as IEnumerable<char>).GetEnumerator();
        }

        public int CompareTo(object obj)
        {
            return (text as IComparable).CompareTo(obj);
        }
        #endregion
    }
}
