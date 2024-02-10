namespace AVXFramework
{
    using Blueprint.Blue;
    using Pinshot.Blue;
    using AVSearch;
    using AVXLib;
    using AVSearch.Model.Results; 
    using Blueprint.Model.Implicit;
    using AVXLib.Memory;
    using static AVXLib.Framework.Numerics;
    using System.Text;
    using System.Collections.Generic;
    using System;

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
        // =================== //
        // THE NEW WAY WILL BE //
        // =================== //
        //
        // pass a defined HighlightMatch object into RenderChapter() function and get a ChapterRendering object back
        // or //
        // pass a defined HighlightMatch object into RenderVerse() function and get a VerseRendering object back

        // if ISettings.LexicalDisplay == Lexion_BOTH, then we treat it as ISettings.Lexion_AVX when spans is non null
        // otherwise, when ISettings.LexicalDisplay == Lexion_BOTH: this produces "side-by-side rendering
        // ISettings.Lexion_UNDEFINED is interpretted as ISettings.Lexion_AV
        public ChapterRendering GetRendering(QLexicalDomain lex, byte b, byte c, Dictionary<UInt32, HighlightMatch>? matches = null, bool sideBySideRendering = false) 
        {
            var rendering = new ChapterRendering(b, c, lex);
            if (b >= 1 && b <= 66)
            {
                var book = ObjectTable.AVXObjects.Mem.Book.Slice(b, 1).Span[0];
                if (c >= 1 && c <= book.chapterCnt)
                {
                    var chapter = ObjectTable.AVXObjects.Mem.Chapter.Slice(book.chapterIdx + c - 1, 1).Span[0];

                    for (byte v = 1; v <= chapter.verseCnt; v++)
                    {
                        VerseRendering vrend = new VerseRendering(v);
                        rendering.Verses[v] = vrend;

                        var writ = ObjectTable.AVXObjects.Mem.Written.Slice((int)(book.writIdx + chapter.writIdx), chapter.writCnt).Span;
                        int cnt = (int)chapter.writCnt;
                        bool located = false;
                        for (int w = 0; w < cnt; /**/)
                        {
                            if (writ[w].BCVWc.V == v)
                            {
                                located = true;

                                WordRendering wrend = this.GetRendering(book, chapter, v, writ[w], matches: matches, differenceRendering: sideBySideRendering);
                                vrend.Words[w] = wrend;
                                w++;
                                w++;
                            }
                            else if (located)
                            {
                                break;
                            }
                            else
                            {
                                w += writ[w].BCVWc.WC;
                            }
                        }
                    }
                }
            }
            return rendering;
        }
        // if ISettings.LexicalDisplay == Lexion_BOTH, then we treat it as ISettings.Lexion_AVX when spans is non null
        // otherwise, when ISettings.LexicalDisplay == Lexion_BOTH: this produces "side-by-side rendering
        // ISettings.Lexion_UNDEFINED is interpretted as ISettings.Lexion_AV
        public VerseRendering GetRendering(QLexicalDomain lex, byte b, byte c, byte v, Dictionary<UInt32, HighlightMatch>? matches = null)
        {
            var rendering = new VerseRendering(v);
            if (b >= 1 && b <= 66)
            {
                var book = ObjectTable.AVXObjects.Mem.Book.Slice(b, 1).Span[0];
                if (c >= 1 && c <= book.chapterCnt)
                {
                    var chapter = ObjectTable.AVXObjects.Mem.Chapter.Slice(book.chapterIdx + c - 1, 1).Span[0];

                    if (v >= 1 && v <= chapter.verseCnt)
                    {
                        var writ = ObjectTable.AVXObjects.Mem.Written.Slice((int)(book.writIdx + chapter.writIdx), chapter.writCnt).Span;
                        int cnt = (int)chapter.writCnt;
                        bool located = false;
                        rendering.Words = new WordRendering[cnt];
                        for (int w = 0; w < cnt; /**/)
                        {
                            if (writ[w].BCVWc.V == v)
                            {
                                located = true;

                                WordRendering wrend = this.GetRendering(book, chapter, v, writ[w], matches);
                                rendering.Words[w] = wrend;
                                w++;
                            }
                            else if (located)
                            {
                                break;
                            }
                            else
                            {
                                w += writ[w].BCVWc.WC;
                            }
                        }
                    }
                }
            }
            return rendering;
        }
        private WordRendering GetRendering(Book book, Chapter chapter, byte v, Written writ, Dictionary<UInt32, HighlightMatch>? matches = null, bool differenceRendering = false) // ISettings.Lexion_BOTH is interpretted as ISettings.Lexion_AVX // ISettings.Lexion_UNDEFINED is interpretted as ISettings.Lexion_AV
        {
            WordRendering rendering = new();
            rendering.Coordinates = writ.BCVWc;
            rendering.Text = ObjectTable.AVXObjects.lexicon.GetLexDisplay(writ.WordKey);
            rendering.Modern = ObjectTable.AVXObjects.lexicon.GetLexModern(writ.WordKey);
            rendering.Punctuation = writ.Punctuation;
            rendering.Triggers = new();
            rendering.HighlightSpans = new();

            var spans = matches.Where(tag => writ.BCVWc >= tag.Value.Start && writ.BCVWc <= tag.Value.Until);

            if (matches != null)
            {
                foreach (var span in spans)
                {
                    if (span.Value.Start == writ.BCVWc)
                    {
                        rendering.HighlightSpans[span.Key] = (UInt16) BCVW.GetDistance(writ.BCVWc, span.Value.Until);
                    }

                    HighlightMatch match = span.Value;

                    foreach (Highlight tag in match.Highlights.Values)
                    {
                        if ((tag.Coordinates == writ.BCVWc) && !rendering.Triggers.ContainsKey(span.Key))
                            rendering.Triggers[span.Key] = tag.Feature;
                    }
                }
            }
            if (differenceRendering == true)
            {
                if (rendering.Modern != rendering.Text)
                {
                    rendering.Triggers[UInt32.MaxValue] = "Modernized";
                    rendering.HighlightSpans[UInt32.MaxValue] = 1;
                }
            }
            return rendering;
        }

        //
        // afterwards //
        // the *Rendering object can be serialized to Yaml or Json using YamlDotNet
        // or //
        // programatically converted to html
        // or //
        // programatically converted to markdown
        // or //
        // programatically converted to text
        //
        // These currently stubbed out methods to not really accomplish that vision
        //
        // Therefore, while Yaml and Json can be handled generically, we will also implement these functions:
        public string RenderChapter(ChapterRendering rendering, QFormat.QFormatVal format)
        {
            switch (format)
            {
                case QFormat.QFormatVal.MD:   return this.RenderChapterAsMarkdown(rendering);
                case QFormat.QFormatVal.TEXT: return this.RenderChapterAsText(rendering);
                case QFormat.QFormatVal.HTML: return this.RenderChapterAsHtml(rendering);
                case QFormat.QFormatVal.JSON: return string.Empty; // utilize YamlDotNet
                case QFormat.QFormatVal.YAML: return string.Empty; // utilize YamlDotNet
            }
            return string.Empty;
        }
        private string RenderChapterAsMarkdown(ChapterRendering rendering)
        {
            return string.Empty;
        }
        private string RenderChapterAsHtml(ChapterRendering rendering)
        {
            return string.Empty;
        }
        private string RenderChapterAsText(ChapterRendering rendering)
        {
            return string.Empty;
        }
        public string RenderVerse(SoloVerseRendering rendering, QFormat.QFormatVal format)
        {
            switch (format)
            {
                case QFormat.QFormatVal.MD:   return this.RenderVerseAsMarkdown(rendering);
                case QFormat.QFormatVal.TEXT: return this.RenderVerseAsText(rendering);
                case QFormat.QFormatVal.HTML: return this.RenderVerseAsHtml(rendering);
                case QFormat.QFormatVal.JSON: return string.Empty; // utilize YamlDotNet
                case QFormat.QFormatVal.YAML: return string.Empty; // utilize YamlDotNet
            }
            return string.Empty;
        }
        private string RenderVerseAsMarkdown(SoloVerseRendering rendering)
        {
            return string.Empty;
        }
        private string RenderVerseAsHtml(SoloVerseRendering rendering)
        {
            return string.Empty;
        }
        private string RenderVerseAsText(SoloVerseRendering rendering)
        {
            return string.Empty;
        }

        public string RenderVerseAsMarkdownTemporary(TextWriter output, byte b, byte c, byte v, QLexicalDisplay.QDisplayVal lex, Dictionary<BCVW, QueryTag> tags)
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
                        var writ = ObjectTable.AVXObjects.Mem.Written.Slice((int)(book.writIdx + chapter.writIdx), chapter.writCnt).Span;
                        int cnt = (int)chapter.writCnt;
                        bool located = false;
                        for (int w = 0; w < cnt; /**/)
                        {
                            if (writ[w].BCVWc.V == v)
                            {
                                located = true;
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

                                    byte previousPunctuation = (w > 0) ? writ[w - 1].Punctuation : (byte)0;
                                    bool s = entry.EndsWith("s", StringComparison.InvariantCultureIgnoreCase);
                                    StringBuilder token = new StringBuilder(entry);
                                    AVEngine.AddPunctuation(token, previousPunctuation, writ[w].Punctuation, s);

                                    output.Write(token.ToString());
                                    output.Write(decoration);
                                }
                                w++;
                            }
                            else if (located)
                            {
                                break;
                            }
                            else
                            {
                                w += writ[w].BCVWc.WC;
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