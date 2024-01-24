namespace AVXFramework
{
    using Blueprint.Blue;
    using Pinshot.Blue;
    using AVSearch;
    using AVXLib;
    using AVSearch.Model.Results; 
    using Blueprint.Model.Implicit;
    using YamlDotNet.Core.Tokens;
    using AVXLib.Memory;
    using static AVXLib.Framework.Numerics;
    using System.Text;

    public class AVEngine
    {
        private QStatement? QuelleModel;
        private PinshotLib QuelleParser;
        private readonly Guid ClientId;

#if USE_NATIVE_LIBRARIES
        private NativeStatement SearchEngine;
#else
        private AVQueryManager SearchEngine;
#endif
        private static void AddPunctuation(StringBuilder builder, ushort previousPunctuation, ushort currentPunctuation, bool s)
        {
            bool prevParen = (previousPunctuation & Punctuation.Parenthetical) != 0;
            bool thisParen = (currentPunctuation & Punctuation.Parenthetical) != 0;

            if (thisParen && !prevParen)
            {
                builder.Insert(0, '(');
            }

            bool eparen  = (currentPunctuation & Punctuation.CloseParen) == Punctuation.CloseParen;
            bool posses  = (currentPunctuation & Punctuation.Possessive) == Punctuation.Possessive;
            bool exclaim = (currentPunctuation & Punctuation.Clause) == Punctuation.Exclamatory;
            bool declare = (currentPunctuation & Punctuation.Clause) == Punctuation.Declarative;
            bool dash    = (currentPunctuation & Punctuation.Clause) == Punctuation.Dash;
            bool semi    = (currentPunctuation & Punctuation.Clause) == Punctuation.Semicolon;
            bool colon   = (currentPunctuation & Punctuation.Clause) == Punctuation.Colon;
            bool comma   = (currentPunctuation & Punctuation.Clause) == Punctuation.Comma;
            bool quest   = (currentPunctuation & Punctuation.Clause) == Punctuation.Interrogative;

            if (posses)
            {
                if (!s)
                    builder.Append("'s");
                else
                    builder.Append('\'');
            }
            if (eparen)
                builder.Append(')');
            if (declare)
                builder.Append('.');
            else if (comma)
                builder.Append(',');
            else if (semi)
                builder.Append(';');
            else if (colon)
                builder.Append(':');
            else if (quest)
                builder.Append('?');
            else if (exclaim)
                builder.Append('!');
            else if (dash)
                builder.Append("--");
        }
        public string Render(TextWriter output, byte b, byte c, byte v, QFormat.QFormatVal format, QLexicalDisplay.QDisplayVal lex, bool showDiffs)
        {
            return string.Empty;
        }
        public string Render(TextWriter output, byte b, byte c, byte v, QFormat.QFormatVal format, QLexicalDisplay.QDisplayVal lex, Dictionary<BCVW, QueryTag> tags)
        {
            if (b >= 1 && b <= 66)
            {
                var book = ObjectTable.AVXObjects.Mem.Book.Slice(b, 1).Span[0];
                if (c >= 1 && c <= book.chapterCnt)
                {
                    var chapter = ObjectTable.AVXObjects.Mem.Chapter.Slice(book.chapterIdx + c - 1, 1).Span[0];

                    if (v >= 1 && v <= chapter.verseCnt)
                    {
                        output.Write(book.abbr4.ToString() + " " + c.ToString() + ':' + v.ToString());
                        var writ = ObjectTable.AVXObjects.Mem.Written.Slice(chapter.writIdx, chapter.writCnt).Span;

                        for (int w = 0; w < chapter.writCnt; w++)
                        {
                            if (writ[w].BCVWc.V == v)
                            {
                                bool bold = tags.ContainsKey(writ[w].BCVWc);
                                bool italics = (byte)(writ[w].Punctuation & Punctuation.Italics) == Punctuation.Italics;

                                string decoration = bold ? (italics ? "***" : "**") : (italics ? "*" : string.Empty);

                                string entry = lex == QLexicalDisplay.QDisplayVal.AV
                                    ? ObjectTable.AVXObjects.lexicon.GetLexDisplay(writ[w].WordKey)
                                    : ObjectTable.AVXObjects.lexicon.GetLexDisplay(writ[w].WordKey);

                                if (entry != null)
                                {
                                    output.Write(' ');
                                    output.Write(decoration);

                                    byte previousPunctuation = (w > 0) ? writ[w-1].Punctuation : (byte)0;
                                    bool s = entry.EndsWith("s", StringComparison.InvariantCultureIgnoreCase);
                                    StringBuilder token = new StringBuilder(entry);
                                    AVEngine.AddPunctuation(token, previousPunctuation, writ[w].Punctuation, s);

                                    output.Write(token.ToString());
                                    output.Write(decoration);
                                }
                            }
                        }
                        output.WriteLine();
                    }
                }
            }
            return string.Empty;
        }

        public AVEngine(string home, string sdk)
        {
            ObjectTable.SDK = sdk;
            this.QuelleModel = null;
            this.QuelleParser = new();
            this.ClientId = Guid.NewGuid();

#if USE_NATIVE_LIBRARIES
            this.SearchEngine = new NativeStatement(SDK);
#else
            this.SearchEngine = new();
#endif        
    }
    public void Release()
        {
#if USE_NATIVE_LIBRARIES
            this.SearchEngine.Release();
#else
            this.SearchEngine.ReleaseAll(this.ClientId);
#endif
        }
        ~AVEngine()
        {
            this.Release();
        }
        public (QStatement? stmt, QueryResult? find, bool ok, string message) Execute(string command)
        {
            var pinshot = this.QuelleParser.Parse(command);
            if (pinshot.root != null)
            {
                if (string.IsNullOrWhiteSpace(pinshot.root.error))
                {
                    QStatement statement = QStatement.Create(pinshot.root);

                    if (statement != null)
                    {
                        if (statement.IsValid)
                        {
                            if (statement.Singleton != null)
                            {
                                var result = statement.Singleton.Execute();
                                return (statement, null, result.ok, result.message);
                            }
                            else if (statement.Commands != null)
                            {
                                foreach (var segment in statement.Commands.Segments)
                                {
                                    if (segment.MacroLabel != null)
                                    {
                                        ExpandableMacro macro = new ExpandableMacro(segment, segment.MacroLabel.Label);
                                        statement.Context.AddMacro(macro);
                                    }
                                    ExpandableHistory item = new ExpandableHistory(segment, (UInt64)(statement.Commands.Context.History.Count));
                                    statement.Context.AddHistory(item);
                                }
                                var results = statement.Commands.Execute();
                                return (statement, results.query, results.ok, results.ok ? "ok" : "TO DO: Add error message") ;
                            }
                            else
                            {
                                return (statement, null, false, "Internal Error: Unexpected blueprint encountered.");
                            }
                        }
                        else
                        {
                            if (statement.Errors.Count > 0)
                            {
                                var errors = string.Join("; ", statement.Errors);
                                return (statement, null, false, errors);
                            }
                            else
                            {
                                return (statement, null, false, "Query was invalid, but the error list was empty.");
                            }
                        }
                    }
                    else
                    {
                        return (statement, null, false, "Query was invalid.");
                    }
                }
                else
                {
                    return (null, null, false, pinshot.root.error);
                }
            }
            return (null, null, false, "Unable to parse the statement.");
        }
    }
}