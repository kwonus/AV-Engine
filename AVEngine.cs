namespace AVXFramework
{
    using Blueprint.Blue;
    using Pinshot.Blue;
    using AVSearch;
    using AVXLib;

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
        public (QStatement? stmt, TQuery? find, bool ok, string message) Execute(string command)
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
                                //var expressions = blueprint.Commands.Searches.ToList();
                                //string yaml = ICommand.YamlSerializerRaw(expressions);
                                //string json = ICommand.JsonSerializerRaw(expressions);

#if ASK_FOR_FORMAT
                                Console.Write("Specify yaml and/or json display (default is none) > ");
                                var answer = Console.ReadLine().ToLower();
#else
                                var answer = "yaml";
#endif
                                if (answer.Contains("yaml") || answer.Contains("both"))
                                {
                                    Console.WriteLine("YAML:");
                                    //Console.WriteLine(yaml);
                                }
                                if (answer.Contains("json") || answer.Contains("both"))
                                {
                                    Console.WriteLine("JSON:");
                                    //Console.WriteLine(json_pretty);
                                }

                                //QSettings qsettings = blueprint.Commands.LocalSettings;
                                //TSettings settings = new TSettings(in qsettings);

                                //List<(byte book, byte chapter, byte verse)> scope = new();

                                //TQuery query = this.SearchEngine.Create(in this.ClientId, in expressions);

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