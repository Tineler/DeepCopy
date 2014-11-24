namespace Nemetos.DeepCopy.Commands
{
    using Sitecore;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Shell.Framework;
    using Sitecore.Shell.Framework.Commands;
    using Sitecore.Shell.Framework.Pipelines;
    using Sitecore.Text;
    using Sitecore.Web.UI.Sheer;
    using System;
    using System.Collections.Specialized;



    [Serializable]
    public class DeepCopyTo : Command
    {
        public DeepCopyTo()
            : base()
        {

        }

        /// <summary>
        /// Executes the command in the specified context.
        /// 
        /// </summary>
        /// <param name="context">The context.</param>
        public override void Execute(CommandContext context)
        {
            CopyTo(context.Items);
        }

        /// <summary>
        /// Queries the state of the command.
        /// 
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>
        /// The state of the command.
        /// </returns>
        public override CommandState QueryState(CommandContext context)
        {
            Error.AssertObject((object)context, "context");
            if (context.Items.Length != 1)
                return CommandState.Disabled;
            Item obj = context.Items[0];
            if (obj.Appearance.ReadOnly || !obj.Access.CanRead())
                return CommandState.Disabled;
            else
                return base.QueryState(context);
        }
        /// <summary>
        /// Copies to.
        /// 
        /// </summary>
        /// <param name="items">The items.</param>
        public static void CopyTo(Item[] items)
        {
            Assert.ArgumentNotNull((object)items, "items");
            if (items.Length <= 0)
                return;
            Start("uiDeepCopyItems", (ClientPipelineArgs)new CopyItemsArgs(), items[0].Database, items[0].Language, items);
        }

        /// <summary>
        /// Starts the specified pipeline name.
        /// 
        /// </summary>
        /// <param name="pipelineName">Name of the pipeline.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="database">The database.</param>
        /// <param name="language">The language.</param>
        /// <param name="items">The items.</param>
        /// <returns/>
        private static NameValueCollection Start(string pipelineName, ClientPipelineArgs args, Database database, Language language, Item[] items)
        {
            Assert.ArgumentNotNullOrEmpty(pipelineName, "pipelineName");
            Assert.ArgumentNotNull((object)args, "args");
            Assert.ArgumentNotNull((object)database, "database");
            Assert.ArgumentNotNull((object)items, "items");
            Assert.ArgumentNotNull(language, "language");

            NameValueCollection nameValueCollection = new NameValueCollection();
            ListString listString = new ListString('|');
            for (int index = 0; index < items.Length; ++index)
                listString.Add(items[index].ID.ToString());
            nameValueCollection.Add("database", database.Name);
            nameValueCollection.Add("items", listString.ToString());
            nameValueCollection.Add("language", language.CultureInfo.TwoLetterISOLanguageName);
            args.Parameters = nameValueCollection;
            Context.ClientPage.Start(pipelineName, args);
            return nameValueCollection;
        }
    }

}


