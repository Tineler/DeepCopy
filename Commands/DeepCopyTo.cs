namespace Nemetos.DeepCopy.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    using Sitecore;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Shell.Framework.Commands;
    using Sitecore.Shell.Framework.Pipelines;
    using Sitecore.Text;
    using Sitecore.Web.UI.Sheer;

    /// <summary>
    /// The deep copy to command.
    /// </summary>
    [Serializable]
    public class DeepCopyTo : Command
    {
        /// <summary>
        /// Copies to.
        /// </summary>
        /// <param name="items">
        /// The items.
        /// </param>
        public static void CopyTo(Item[] items)
        {
            Assert.ArgumentNotNull(items, "items");
            if (items.Length <= 0)
            {
                return;
            }

            Start(
                "uiDeepCopyItems",
                new CopyItemsArgs(),
                items[0].Database,
                items[0].Language,
                items);
        }

        /// <summary>
        /// Executes the command in the specified context.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        public override void Execute(CommandContext context)
        {
            CopyTo(context.Items);
        }

        /// <summary>
        /// Queries the state of the command.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <returns>
        /// The state of the command.
        /// </returns>
        public override CommandState QueryState(CommandContext context)
        {
            Error.AssertObject(context, "context");
            if (context.Items.Length != 1)
            {
                return CommandState.Disabled;
            }

            var obj = context.Items[0];
            if (obj.Appearance.ReadOnly || !obj.Access.CanRead())
            {
                return CommandState.Disabled;
            }

            return base.QueryState(context);
        }

        /// <summary>
        /// Starts the specified pipeline name.
        /// </summary>
        /// <param name="pipelineName">
        /// Name of the pipeline.
        /// </param>
        /// <param name="args">
        /// The arguments.
        /// </param>
        /// <param name="database">
        /// The database.
        /// </param>
        /// <param name="language">
        /// The language.
        /// </param>
        /// <param name="items">
        /// The items.
        /// </param>
        private static void Start(string pipelineName, ClientPipelineArgs args, Database database, Language language, IEnumerable<Item> items)
        {
            Assert.ArgumentNotNullOrEmpty(pipelineName, "pipelineName");
            Assert.ArgumentNotNull(args, "args");
            Assert.ArgumentNotNull(database, "database");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(items, "items");

            var listString = new ListString('|');
            foreach (var item in items)
            {
                listString.Add(item.ID.ToString());
            }

            var nameValueCollection = new NameValueCollection
                                          {
                                              { "database", database.Name },
                                              { "items", listString.ToString() },
                                              { "language", language.CultureInfo.TwoLetterISOLanguageName }
                                          };

            args.Parameters = nameValueCollection;
            Context.ClientPage.Start(pipelineName, args);
        }
    }
}