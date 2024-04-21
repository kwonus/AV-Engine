namespace AVXFramework
{
    using Blueprint.Blue;
    using Pinshot.Blue;
    using AVSearch;
    using AVXLib;
    using AVSearch.Model.Results; 
    using AVXLib.Memory;
    using static AVXLib.Framework.Numerics;
    using System.Text;
    using System.Collections.Generic;
    using System;
    using AVSearch.Interfaces;
    using System.IO;

    public class AVEngine
    {
        private PinshotLib QuelleParser;
        private readonly Guid ClientId;

        private static string? _OmegaFile = null;
        public static string Data
        {
            get
            {
                if (_OmegaFile != null)
                    return _OmegaFile;

                string cwd = Directory.GetCurrentDirectory();
                for (string omega = Path.Combine(cwd, "Data", "AVX-Omega.data"); omega.Length > @"X:\Data\AVX-Omega.data".Length; omega = Path.Combine(cwd, "Data", "AVX-Omega.data"))
                {
                    if (File.Exists(omega))
                    {
                        _OmegaFile = omega;
                        return omega;
                    }
                    var parent = Directory.GetParent(cwd);
                    if (parent == null)
                        break;
                    cwd = parent.FullName;
                }
                for (string omega = Path.Combine(cwd, "AVX-Omega.data"); omega.Length > @"X:\AVX-Omega.data".Length; omega = Path.Combine(cwd, "AVX-Omega.data"))
                {
                    if (File.Exists(omega))
                    {
                        _OmegaFile = omega;
                        return omega;
                    }
                    var parent = Directory.GetParent(cwd);
                    if (parent == null)
                        break;
                    cwd = parent.FullName;
                }
                return (@"C:\src\Digital-AV\omega\AVX-Omega.data");
            }
        }

#if USE_NATIVE_LIBRARIES
        private NativeStatement SearchEngine;
#else
        private AVQueryManager SearchEngine;
#endif
        private static void ConditionallyMakePossessive(StringBuilder builder, ushort currentPunctuation, bool s)
        {
            bool posses = (currentPunctuation & Punctuation.Possessive) == Punctuation.Possessive;

            if (posses)
            {
                if (!s)
                    builder.Append("'s");
                else
                    builder.Append('\'');
            }
        }

        private static void AddPrefixPunctuation(StringBuilder builder, ushort previousPunctuation, ushort currentPunctuation)
        {
            bool prevParen = (previousPunctuation & Punctuation.Parenthetical) != 0;
            bool thisParen = (currentPunctuation & Punctuation.Parenthetical) != 0;

            if (thisParen && !prevParen)
            {
                builder.Append('(');
            }
        }
        private static void AddPostfixPunctuation(StringBuilder builder, ushort currentPunctuation, bool? s = null)
        {
            bool eparen  = (currentPunctuation & Punctuation.CloseParen) == Punctuation.CloseParen;
//          bool posses  = (currentPunctuation & Punctuation.Possessive) == Punctuation.Possessive;
            bool exclaim = (currentPunctuation & Punctuation.Clause) == Punctuation.Exclamatory;
            bool declare = (currentPunctuation & Punctuation.Clause) == Punctuation.Declarative;
            bool dash    = (currentPunctuation & Punctuation.Clause) == Punctuation.Dash;
            bool semi    = (currentPunctuation & Punctuation.Clause) == Punctuation.Semicolon;
            bool colon   = (currentPunctuation & Punctuation.Clause) == Punctuation.Colon;
            bool comma   = (currentPunctuation & Punctuation.Clause) == Punctuation.Comma;
            bool quest   = (currentPunctuation & Punctuation.Clause) == Punctuation.Interrogative;

            if (s != null)
            {
                ConditionallyMakePossessive(builder, currentPunctuation, s.Value);
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
                        VerseRendering vrend = this.GetVerse(b, c, v, matches);
                        rendering.Verses[v] = vrend;
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
            WordRendering rendering = new()
            {
                WordKey = writ.WordKey,
                Coordinates = writ.BCVWc,
                Text = ObjectTable.AVXObjects.lexicon.GetLexDisplay(writ.WordKey),
                Modern = ObjectTable.AVXObjects.lexicon.GetLexModern(writ.WordKey, writ.Lemma),
                Punctuation = writ.Punctuation,
                PnPos = QPartOfSpeech.GetPnPos(writ.pnPOS12),
                NuPos = FiveBitEncoding.DecodePOS(writ.POS32),
                Triggers = new()
            };
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
        public bool RenderChapter(StringBuilder output, ChapterRendering rendering, ISettings settings)
        {
            switch (settings.RenderingFormat)
            {
                case ISettings.Formatting_MD:   return this.RenderChapterAsMarkdown(output, rendering, settings);
                case ISettings.Formatting_TEXT: return this.RenderChapterAsText(output, rendering, settings);
                case ISettings.Formatting_HTML: return this.RenderChapterAsHtml(output, rendering, settings);
                case ISettings.Formatting_JSON: return this.RenderChapterAsJson(output, rendering);
                case ISettings.Formatting_YAML: return this.RenderChapterAsYaml(output, rendering);
            }
            return false;
        }
        public bool RenderChapterAsMarkdown(StringBuilder output, ChapterRendering rendering, ISettings settings)
        {
            if (rendering.BookNumber >= 1 && rendering.BookNumber <= 66)
            {
                output.Append('#');
                output.Append(rendering.BookName);
                output.Append(' ');

                var book = ObjectTable.AVXObjects.Mem.Book.Slice(rendering.BookNumber, 1).Span[0];

                if (rendering.ChapterNumber >= 1 && rendering.ChapterNumber <= book.chapterCnt)
                {
                    output.AppendLine(rendering.ChapterNumber.ToString());

                    var chapter = ObjectTable.AVXObjects.Mem.Chapter.Slice(book.chapterIdx + rendering.ChapterNumber - 1, 1).Span[0];

                    for (byte v = 1; v <= chapter.verseCnt; v++)
                    {
                        output.Append("**");
                        output.Append(v.ToString());
                        output.Append("** ");
                        this.RenderVerseAsMarkdown(output, rendering.Verses[v], settings);
                    }
                    return true;
                }
            }
            return false;
        }
        public bool RenderChapterAsHtml(StringBuilder output, ChapterRendering rendering, ISettings settings)
        {
            if (rendering.BookNumber >= 1 && rendering.BookNumber <= 66)
            {
                output.Append("<b>");
                output.Append(rendering.BookName);
                output.Append("</b><br/>");

                var book = ObjectTable.AVXObjects.Mem.Book.Slice(rendering.BookNumber, 1).Span[0];

                if (rendering.ChapterNumber >= 1 && rendering.ChapterNumber <= book.chapterCnt)
                {
                    output.AppendLine(rendering.ChapterNumber.ToString());

                    var chapter = ObjectTable.AVXObjects.Mem.Chapter.Slice(book.chapterIdx + rendering.ChapterNumber - 1, 1).Span[0];

                    for (byte v = 1; v <= chapter.verseCnt; v++)
                    {
                        string verse = v.ToString();
                        output.Append("<span class=\"verse\" id=\"V");
                        output.Append(verse);
                        output.Append("\">");
                        output.Append(verse);
                        output.Append("</span> ");
                        this.RenderVerseAsHtml(output, rendering.Verses[v], settings);
                    }
                    return true;
                }
            }
            return false;
        }
        public bool RenderChapterSideBySideAsHtml(StringBuilder output, ChapterRendering rendering)
        {
            return false;
        }
        public bool RenderChapterAsText(StringBuilder output, ChapterRendering rendering, ISettings settings)
        {
            if (rendering.BookNumber >= 1 && rendering.BookNumber <= 66)
            {
                output.Append(rendering.BookName);
                output.Append(' ');

                var book = ObjectTable.AVXObjects.Mem.Book.Slice(rendering.BookNumber, 1).Span[0];

                if (rendering.ChapterNumber >= 1 && rendering.ChapterNumber <= book.chapterCnt)
                {
                    output.AppendLine(rendering.ChapterNumber.ToString());

                    var chapter = ObjectTable.AVXObjects.Mem.Chapter.Slice(book.chapterIdx + rendering.ChapterNumber - 1, 1).Span[0];

                    for (byte v = 1; v <= chapter.verseCnt; v++)
                    {
                        output.Append(v.ToString());
                        output.Append("\t");
                        this.RenderVerseAsText(output, rendering.Verses[v], settings);
                    }
                    return true;
                }
            }
            return false;
        }
        public bool RenderChapterAsJson(StringBuilder output, ChapterRendering rendering)
        {
            try
            {
                var serializer = new YamlDotNet.Serialization.SerializerBuilder().JsonCompatible().Build();
                string json = serializer.Serialize(rendering);
                return true;
            }
            catch
            {
                ;
            }
            return false;
        }
        public bool RenderChapterAsYaml(StringBuilder output, ChapterRendering rendering)
        {
            try
            {
                YamlDotNet.Serialization.Serializer serializer = new();
                string yaml = serializer.Serialize(rendering);
                output.Append(yaml);
                return true;
            }
            catch
            {
                ;
            }
            return false;
        }
        public bool RenderVerse(StringBuilder output, VerseRendering rendering, ISettings settings)
        {
    
            switch (settings.RenderingFormat)
            {
                case ISettings.Formatting_MD:   return this.RenderVerseAsMarkdown(output, rendering, settings);
                case ISettings.Formatting_TEXT: return this.RenderVerseAsText(output, rendering, settings);
                case ISettings.Formatting_HTML: return this.RenderVerseAsHtml(output, rendering, settings);
                case ISettings.Formatting_JSON: return this.RenderVerseAsJson(output, rendering);
                case ISettings.Formatting_YAML: return this.RenderVerseAsYaml(output, rendering);
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

                case ISettings.Formatting_JSON: return this.RenderVerseAsJson(output, rendering);

                case ISettings.Formatting_YAML: return this.RenderVerseAsYaml(output, rendering);
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
                bool s = entry.EndsWith("s", StringComparison.InvariantCultureIgnoreCase);

                if (space)
                    output.Append(' ');
                else
                    space = true;

                AVEngine.AddPrefixPunctuation(output, previousPunctuation, word.Punctuation);

                output.Append(decoration);
                output.Append(entry);
                AVEngine.ConditionallyMakePossessive(output, word.Punctuation, s);
                output.Append(decoration);
                AVEngine.AddPostfixPunctuation(output, word.Punctuation);

                previousPunctuation = word.Punctuation;
            }
            return space;
        }
        private bool RenderVerseAsHtml(StringBuilder output, VerseRendering rendering, ISettings settings)
        {
            byte previousPunctuation = 0;
            bool space = false;
            foreach (WordRendering word in rendering.Words)
            {
                bool bold = word.Triggers.Count > 0;
                bool italics = (byte)(word.Punctuation & Punctuation.Italics) == Punctuation.Italics;

                string entry = settings.RenderAsAV ? word.Text : word.Modern;
                bool s = entry.EndsWith("s", StringComparison.InvariantCultureIgnoreCase);

                if (space)
                    output.Append(' ');
                else
                    space = true;

                AVEngine.AddPrefixPunctuation(output, previousPunctuation, word.Punctuation);

                if (bold)
                    output.Append("<b>");
                if (italics)
                    output.Append("<em>");
                output.Append("<span id=\"C");
                output.Append(word.Coordinates.elements.ToString());
                output.Append("\" class=\"W");
                output.Append(word.WordKey.ToString());
                output.Append("\" diff=\"");
                output.Append(word.Modernized ? "true" : "false");
                output.Append("\">");
                output.Append(entry);
                AVEngine.ConditionallyMakePossessive(output, word.Punctuation, s);
                output.Append("</span>");
                if (italics)
                    output.Append("</em>");
                if (bold)
                    output.Append("</b>");

                AVEngine.AddPostfixPunctuation(output, word.Punctuation);

                previousPunctuation = word.Punctuation;
            }
            return space;
        }
        private bool RenderVerseAsText(StringBuilder output, VerseRendering rendering, ISettings settings)
        {
            byte previousPunctuation = 0;
            bool space = false;
            foreach (WordRendering word in rendering.Words)
            {
                bool bold = word.Triggers.Count > 0;
                bool italics = (byte)(word.Punctuation & Punctuation.Italics) == Punctuation.Italics;

                string entry = settings.RenderAsAV ? word.Text : word.Modern;
                bool s = entry.EndsWith("s", StringComparison.InvariantCultureIgnoreCase);

                if (space)
                    output.Append(' ');
                else
                    space = true;

                AVEngine.AddPrefixPunctuation(output, previousPunctuation, word.Punctuation);

                if (bold)
                    output.Append('*');
                if (italics)
                    output.Append('[');
                output.Append(entry);
                AVEngine.ConditionallyMakePossessive(output, word.Punctuation, s);
                if (italics)
                    output.Append(']');
                if (bold)
                    output.Append('*');

                AVEngine.AddPostfixPunctuation(output, word.Punctuation);

                previousPunctuation = word.Punctuation;
            }
            return space;
        }
        private bool RenderVerseAsJson(StringBuilder output, VerseRendering rendering)
        {
            try
            {
                var serializer = new YamlDotNet.Serialization.SerializerBuilder().JsonCompatible().Build();
                string json = serializer.Serialize(rendering);
                return true;
            }
            catch
            {
                ;
            }
            return false;
        }
        private bool RenderVerseAsJson(StringBuilder output, SoloVerseRendering rendering)
        {
            try
            {
                var serializer = new YamlDotNet.Serialization.SerializerBuilder().JsonCompatible().Build();
                string json = serializer.Serialize(rendering);
                output.Append(json);
                return true;
            }
            catch
            {
                ;
            }
            return false;
        }
        private bool RenderVerseAsYaml(StringBuilder output, VerseRendering rendering)
        {
            try
            {
                YamlDotNet.Serialization.Serializer serializer = new();
                string yaml = serializer.Serialize(rendering);
                output.Append(yaml);
                return true;
            }
            catch
            {
                ;
            }
            return false;
        }
        private bool RenderVerseAsYaml(StringBuilder output, SoloVerseRendering rendering)
        {
            try
            {
                YamlDotNet.Serialization.Serializer serializer = new();
                string yaml = serializer.Serialize(rendering);
                output.Append(yaml);
                return true;
            }
            catch
            {
                ;
            }
            return false;
        }
        public static AVEngine? SELF { get; private set; } = null;
        public AVEngine()
        {
            ObjectTable.SDK = AVEngine.Data;
            this.QuelleParser = new();
            this.ClientId = Guid.NewGuid();

#if USE_NATIVE_LIBRARIES
            this.SearchEngine = new NativeStatement(SDK);
#else
            this.SearchEngine = new();
#endif        
            AVEngine.SELF = this;
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
        public (QStatement? stmt, QueryResult? search, bool ok, DirectiveResultType directive, string message) Execute(string command)
        {
            var pinshot = this.QuelleParser.Parse(command);
            if (pinshot.root != null)
            {
                DirectiveResultType directive = DirectiveResultType.NotApplicable;

                if (string.IsNullOrWhiteSpace(pinshot.root.error))
                {
                    QStatement statement = QStatement.Create(pinshot.root);

                    if (statement != null)
                    {
                        if (statement.IsValid)
                        {
                            if (statement.Singleton != null)
                            {
                                QSingleton ston = statement.Singleton;
                                var result = statement.Singleton.Execute();
                                return (statement, null, result.ok, directive, result.message);
                            }
                            else if (statement.Commands != null)
                            {
                                var segment = statement.Commands.SelectionCriteria;
                                if (statement.Commands.MacroDirective != null)
                                {
                                    if (segment != null)
                                    {
                                        ExpandableMacro macro = new ExpandableMacro(command, segment, statement.Commands.MacroDirective.Label);
                                        macro.Serialize();
                                        directive = DirectiveResultType.MacroCreated;
                                    }
                                    else
                                    {
                                        directive = DirectiveResultType.MacroCreationFailed;
                                    }
                                }
                                ExpandableHistory item = new ExpandableHistory(command, segment);
                                item.Serialize();

                                var results = statement.Commands.Execute();
                                if (directive == DirectiveResultType.NotApplicable)
                                {
                                    directive = results.directive;
                                    if (directive == DirectiveResultType.ExportReady && statement.Commands.ExportDirective != null)
                                    {
                                        statement.Commands.ExportDirective.Merge(item.Scope.Values);
                                        directive = statement.Commands.ExportDirective.Update();
                                    }
                                }

                                return (statement, results.query, results.ok != SelectionResultType.InvalidStatement, directive, results.ok != SelectionResultType.InvalidStatement ? "ok" : "ERROR: Unexpected parsing error") ;
                            }
                            else
                            {
                                return (statement, null, false, directive, "Internal Error: Unexpected blueprint encountered.");
                            }
                        }
                        else
                        {
                            if (statement.Errors.Count > 0)
                            {
                                var errors = string.Join("; ", statement.Errors);
                                return (statement, null, false, directive, errors);
                            }
                            else
                            {
                                return (statement, null, false, directive, "Query was invalid, but the error list was empty.");
                            }
                        }
                    }
                    else
                    {
                        return (statement, null, false, directive, "Query was invalid.");
                    }
                }
                else
                {
                     return (null, null, false, directive, pinshot.root.error);
                }
            }
            return (null, null, false, DirectiveResultType.NotApplicable, "Unable to parse the statement.");
        }
    }
}