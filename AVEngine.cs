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
    using AVSearch.Interfaces;

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
        public ChapterRendering GetChapter(byte b, byte c, Dictionary<UInt32, QueryMatch> matches, bool sideBySideRendering = false) 
        {
            var rendering = new ChapterRendering(b, c);
            if (b >= 1 && b <= 66)
            {
                var book = ObjectTable.AVXObjects.Mem.Book.Slice(b, 1).Span[0];
                if (c >= 1 && c <= book.chapterCnt)
                {
                    var chapter = ObjectTable.AVXObjects.Mem.Chapter.Slice(book.chapterIdx + c - 1, 1).Span[0];

                    for (byte v = 1; v <= chapter.verseCnt; v++)
                    {
                        var writ = ObjectTable.AVXObjects.Mem.Written.Slice((int)(book.writIdx + chapter.writIdx), chapter.writCnt).Span;
                        VerseRendering vrend = new VerseRendering(writ[0].BCVWc);
                        rendering.Verses[v] = vrend;

                        int cnt = (int)chapter.writCnt;
                        bool located = false;
                        for (int w = 0; w < cnt; /**/)
                        {
                            if (writ[w].BCVWc.V == v)
                            {
                                located = true;

                                WordRendering wrend = this.GetWord(book, chapter, v, writ[w], matches: matches);
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
        public VerseRendering GetVerse(byte b, byte c, byte v, Dictionary<UInt32, QueryMatch> matches)
        {
            bool located = false;
            VerseRendering rendering = new VerseRendering(0, 0, 0, 0);
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

                        int i = 0;
                        for (int w = 0; w < cnt; /**/)
                        {
                            if (writ[w].BCVWc.V == v)
                            {
                                if (!located)
                                {
                                    rendering = new VerseRendering(writ[w].BCVWc);
                                    located = true;
                                }

                                if (i < rendering.Words.Length)
                                {
                                    WordRendering wrend = this.GetWord(book, chapter, v, writ[w], matches);
                                    rendering.Words[i++] = wrend;
                                    w++;
                                }
                                else
                                {
                                    break;
                                }
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
        private WordRendering GetWord(Book book, Chapter chapter, byte v, Written writ, Dictionary<UInt32, QueryMatch> matches) // ISettings.Lexion_BOTH is interpretted as ISettings.Lexion_AVX // ISettings.Lexion_UNDEFINED is interpretted as ISettings.Lexion_AV
        {
            WordRendering rendering = new();
            rendering.Coordinates = writ.BCVWc;
            rendering.Text = ObjectTable.AVXObjects.lexicon.GetLexDisplay(writ.WordKey);
            rendering.Modern = ObjectTable.AVXObjects.lexicon.GetLexModern(writ.WordKey);
            rendering.Punctuation = writ.Punctuation;
            rendering.Triggers = new();

            var spans = matches.Where(tag => writ.BCVWc >= tag.Value.Start && writ.BCVWc <= tag.Value.Until);

            foreach (var span in spans)
            {
                foreach (QueryTag tag in span.Value.Highlights)
                {
                    if ((tag.Coordinates == writ.BCVWc) && !rendering.Triggers.ContainsKey(span.Key))
                    {
                        UInt32 key = (UInt32)(rendering.Triggers.Count + 1);
                        rendering.Triggers[key] = tag.Feature.Text;
                    }
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
        public bool RenderChapter(StringBuilder output, ChapterRendering rendering, ISettings settings, bool renderSideBySide = false)
        {
            switch (settings.RenderingFormat)
            {
                case ISettings.Formatting_MD:   return this.RenderChapterAsMarkdown(output, rendering, settings);
                case ISettings.Formatting_TEXT: return this.RenderChapterAsText(output, rendering, settings);
                case ISettings.Formatting_HTML: return this.RenderChapterAsHtml(output, rendering, settings);
                case ISettings.Formatting_JSON: return this.RenderChapterAsJson(output, rendering, settings);
                case ISettings.Formatting_YAML: return this.RenderChapterAsYaml(output, rendering, settings);
            }
            return false;
        }
        private bool RenderChapterAsMarkdown(StringBuilder output, ChapterRendering rendering, ISettings settings, bool renderSideBySide = false)
        {
            return false;
        }
        private bool RenderChapterAsHtml(StringBuilder output, ChapterRendering rendering, ISettings settings, bool renderSideBySide = false)
        {
            return false;
        }
        private bool RenderChapterAsText(StringBuilder output, ChapterRendering rendering, ISettings settings, bool renderSideBySide = false)
        {
            return false;
        }
        private bool RenderChapterAsJson(StringBuilder output, ChapterRendering rendering, ISettings settings, bool renderSideBySide = false)
        {
            return false;
        }
        private bool RenderChapterAsYaml(StringBuilder output, ChapterRendering rendering, ISettings settings, bool renderSideBySide = false)
        {
            return false;
        }
        public bool RenderVerse(StringBuilder output, VerseRendering rendering, ISettings settings)
        {
    
            switch (settings.RenderingFormat)
            {
                case ISettings.Formatting_MD:   return this.RenderVerseAsMarkdown(output, rendering, settings);
                case ISettings.Formatting_TEXT: return this.RenderVerseAsText(output, rendering, settings);
                case ISettings.Formatting_HTML: return this.RenderVerseAsHtml(output, rendering, settings);
                case ISettings.Formatting_JSON: return this.RenderVerseAsJson(output, rendering, settings);
                case ISettings.Formatting_YAML: return this.RenderVerseAsYaml(output, rendering, settings);
            }
            return false;
        }
        public bool RenderVerseSolo(StringBuilder output, SoloVerseRendering rendering, ISettings settings)
        {
            string result = string.Empty;
            string label = rendering.BookAbbreviation4.Trim() + " " + rendering.ChapterNumber.ToString() + ':' + rendering.Coordinates.V.ToString();
            switch (settings.RenderingFormat)
            {
                case ISettings.Formatting_MD:   output.Append("__");
                                                output.Append(label);
                                                output.Append("__ ");
                                                return this.RenderVerseAsMarkdown(output, rendering, settings);

                case ISettings.Formatting_TEXT: output.Append(label);
                                                output.Append(" ");
                                                return this.RenderVerseAsText(output, rendering, settings);
        
                case ISettings.Formatting_HTML: output.Append("<b>");
                                                output.Append(label);
                                                output.Append("</b> ");
                                                return this.RenderVerseAsHtml(output, rendering, settings); 

                case ISettings.Formatting_JSON: return this.RenderVerseAsJson(output, rendering, settings);

                case ISettings.Formatting_YAML: return this.RenderVerseAsYaml(output, rendering, settings);
            }
            return false;
        }
        private bool RenderVerseAsMarkdown(StringBuilder output, VerseRendering rendering, ISettings settings)
        {
            byte previousPunctuation = 0;
            bool space = false;
            foreach (WordRendering word in rendering.Words)
            {
                bool bold = word.Triggers.Count > 0;
                bool italics = (byte)(word.Punctuation & Punctuation.Italics) == Punctuation.Italics;

                string decoration = bold ? (italics ? "***" : "**") : (italics ? "*" : string.Empty);

                string entry = settings.RenderAsAV ? word.Text : word.Modern;
                if (space)
                    output.Append(' ');
                else
                    space = true;

                output.Append(decoration);

                bool s = entry.EndsWith("s", StringComparison.InvariantCultureIgnoreCase);
                StringBuilder token = new StringBuilder(entry);
                AVEngine.AddPunctuation(token, previousPunctuation, word.Punctuation, s);

                output.Append(token.ToString());
                output.Append(decoration);

                previousPunctuation = word.Punctuation;
            }
            return space;
        }
        private bool RenderVerseAsHtml(StringBuilder output, VerseRendering rendering, ISettings settings)
        {
            return false;
        }
        private bool RenderVerseAsText(StringBuilder output, VerseRendering rendering, ISettings settings)
        {
            return false;
        }
        private bool RenderVerseAsJson(StringBuilder output, VerseRendering rendering, ISettings settings)
        {
            return false;
        }
        private bool RenderVerseAsJson(StringBuilder output, SoloVerseRendering rendering, ISettings settings)
        {
            return false;
        }
        private bool RenderVerseAsYaml(StringBuilder output, VerseRendering rendering, ISettings settings)
        {
            return false;
        }
        private bool RenderVerseAsYaml(StringBuilder output, SoloVerseRendering rendering, ISettings settings)
        {
            return false;
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
                                return (statement, results.query, results.ok, results.ok ? "ok" : "ERROR: Unexpected parsing error") ;
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