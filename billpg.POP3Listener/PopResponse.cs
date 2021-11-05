/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Text;

namespace billpg.pop3
{
    internal class PopResponse
    {
        private readonly ResponseClass respType;
        private readonly NextLineFn nextLineGen;

        internal bool IsQuit => respType == ResponseClass.OKQuit || respType == ResponseClass.Critical;
        internal bool IsNormal => IsQuit == false;
        internal bool IsOK => respType == ResponseClass.OK || respType == ResponseClass.OKQuit;
        internal bool IsError => IsOK == false;
        internal bool IsSingleLine => nextLineGen == null;
        internal bool IsMultiLine => IsSingleLine == false;
        internal string Code { get; }
        internal string Text { get; }

        private PopResponse(ResponseClass respType, string code, string text, NextLineFn nextlineGen)
        {
            /* Text is not allowed to contain any [ or ] and not allowed to end with _. */
            if (text.Contains("[") || text.Contains("]") || text.Contains("\r") || text.Contains("\n") || text.EndsWith(" _"))
                throw new ApplicationException("Response text contains disallowed characters.");

            this.respType = respType;
            this.Code = code;
            this.Text = text;
            this.nextLineGen = nextlineGen;
        }

        private enum ResponseClass
        {
            OK,
            OKQuit,
            Error,
            Critical
        }

        internal static PopResponse OKSingle(string text)
            => new PopResponse(ResponseClass.OK, null, text, null);

        internal static PopResponse OKSingle(string code, string text)
            => new PopResponse(ResponseClass.OK, code, text, null);

        internal static PopResponse OKMulti(string text, IEnumerable<string> lines)
            => new PopResponse(ResponseClass.OK, null, text, ItterableToLineGen(lines));

        internal static PopResponse OKMulti(string text, NextLineFn nextLineGen)
            => new PopResponse(ResponseClass.OK, null, text, nextLineGen);

        internal static PopResponse ERR(string text)
            => new PopResponse(ResponseClass.Error, null, text, null);

        internal static PopResponse ERR(string code, string text)
            => new PopResponse(ResponseClass.Error, code, text, null);

        internal static PopResponse Critical(string code, string text)
            => new PopResponse(ResponseClass.Critical, code, text, null);

        internal static PopResponse Quit(string text)
            => new PopResponse(ResponseClass.OKQuit, null, text, null);

        private static NextLineFn ItterableToLineGen(IEnumerable<string> lines)
        {
            /* Start the iterator. */
            var lineIter = lines.GetEnumerator();

            /* Return a function that returns the next line. */
            return NextLineInternal;
            string NextLineInternal()
            {
                /* Move onto the next line and return either the line or null if complete. */
                if (lineIter.MoveNext())
                    return lineIter.Current;
                else
                    return null;
            }
        }

        internal string NextLine()
        {
            /* Return nulls if there is no multi-line response. */
            if (IsSingleLine)
                return null;

            /* Return the next line. */
            return nextLineGen();
        }

        internal string FirstLine
            => (IsOK ? "+OK" : "-ERR") + " " + (Code == null ? $"[{Code}] " : "") + Text;
    }
}

