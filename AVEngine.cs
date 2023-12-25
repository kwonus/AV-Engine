namespace AVXFramework
{
    using Blueprint.Blue;
    using Pinshot.Blue;
    using AVSearch;
    using AVXLib;
    using AVSearch.Model.Results;

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
                                // TO DO: Execute
                                return (statement, null /*query*/, true, "ok") ;
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