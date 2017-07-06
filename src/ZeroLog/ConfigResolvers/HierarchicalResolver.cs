using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZeroLog.Appenders;
using ZeroLog.Appenders.Builders;
using ZeroLog.Config;

namespace ZeroLog.ConfigResolvers
{
    public class HierarchicalResolver : IConfigurationResolver
    {
        private Node _root;
        private Encoding AppenderEncoding { get; set; }

        public IList<IAppender> ResolveAppenders(string name) => Resolve(name).Appenders.ToList();
        public Level ResolveLevel(string name) => Resolve(name).Level;
        public LogEventPoolExhaustionStrategy ResolveExhaustionStrategy(string name) => Resolve(name).Strategy;

        public event Action Updated = delegate { };

        public void Build(ZeroLogConfiguration config)
        {
            var oldRoot = _root;
            var newRoot = new Node();

            foreach (var loggerWithAppenders in CreateLoggersWithAppenders(config).OrderBy(x => x.logger.Name))
            {
                AddNode(newRoot, loggerWithAppenders.logger, loggerWithAppenders.appenders);
            }

            Initialize(newRoot);

            _root = newRoot;

            Updated();

            oldRoot?.Close();
        }

        private static List<(LoggerDefinition logger, IAppender[] appenders)> CreateLoggersWithAppenders(ZeroLogConfiguration config)
        {
            var appendersByNames = config.Appenders.ToDictionary(x => x.Name, AppenderFactory.BuildAppender);

            var loggerWithAppenders = new List<(LoggerDefinition, IAppender[])>();

            config.RootLogger.Name = string.Empty;
            config.RootLogger.IncludeParentAppenders = false;

            loggerWithAppenders.Add((config.RootLogger, config.RootLogger.AppenderReferences.Select(x => appendersByNames[x]).ToArray()));

            foreach (var loggerDefinition in config.Loggers)
            {
                loggerWithAppenders.Add((loggerDefinition, loggerDefinition.AppenderReferences.Select(x => appendersByNames[x]).ToArray()));
            }

            return loggerWithAppenders;
        }

        private static void AddNode(Node root, LoggerDefinition logger, IAppender[] appenders)
        {
            var parts = logger.Name.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
            var node = root;
            var path = "";

            foreach (var part in parts)
            {
                path = (path + "." + part).Trim('.');

                if (!node.Children.ContainsKey(part))
                    node.Children[part] = new Node {Appenders = node.Appenders, Level = node.Level, Strategy = node.Strategy};

                node = node.Children[part];
            }

            node.Appenders = (logger.IncludeParentAppenders ? appenders.Union(node.Appenders) : appenders).Distinct();
            node.Strategy = logger.LogEventPoolExhaustionStrategy;
            node.Level = logger.Level;
        }

        private Node Resolve(string name)
        {
            var parts = name.Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
            var node = _root;

            foreach (var part in parts)
            {
                if (!node.Children.ContainsKey(part))
                    break;

                node = node.Children[part];
            }

            return node;
        }

        public void Initialize(Encoding encoding)
        {
            AppenderEncoding = encoding;

            Initialize(_root);
        }

        private void Initialize(Node node)
        {
            if (node.Appenders != null)
                node.Appenders = node.Appenders.Select(x => new GuardedAppender(x, TimeSpan.FromSeconds(15))).ToList();

            if (AppenderEncoding != null && node.Appenders != null)
            {
                foreach (var appender in node.Appenders)
                {
                    appender.SetEncoding(AppenderEncoding);
                }
            }

            foreach (var n in node.Children)
            {
                Initialize(n.Value);
            }
        }

        public void Dispose()
        {
            _root?.Close();
        }

        private class Node
        {
            public readonly Dictionary<string, Node> Children = new Dictionary<string, Node>();
            public IEnumerable<IAppender> Appenders;
            public Level Level;
            public LogEventPoolExhaustionStrategy Strategy;

            public void Close()
            {
                foreach (var appender in Appenders)
                {
                    appender.Close();
                }

                foreach (var child in Children.Values)
                {
                    child.Close();
                }
            }
        }
    }
}