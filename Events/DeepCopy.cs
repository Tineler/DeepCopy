namespace Nemetos.DeepCopy.Events
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Fields;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Events;
    using Sitecore.SecurityModel;
    using Sitecore.Shell.Framework.Pipelines;
    using Sitecore.Text;

    /// <summary>
    /// The deep copy.
    /// </summary>
    public class DeepCopy
    {
        /// <summary>
        /// Gets or sets database to work with from configuration (only used for event handlers).
        /// </summary>
        public string Database
        {
            get;
            set;
        }

        /// <summary>
        /// Fix the datasource for the copying items
        /// </summary>
        /// <param name="args">The arguments.</param><contract><requires name="args" condition="not null"/></contract>
        public virtual void Execute(CopyItemsArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            var destination = GetDatabase(args).GetItem(args.Parameters["destination"]);
            Assert.IsNotNull(destination, args.Parameters["destination"]);

            var items = GetItems(args);
            var str = destination.Uri.ToString();
            for (var i = 0; i < items.Count && i < args.Copies.Count(); i++)
            {
                var originalItem = items[i];
                var copyItem = args.Copies[i];

                if (originalItem == null || copyItem == null)
                {
                    continue;
                }

                Log.Audit(this, "Copy item: {0}", AuditFormatter.FormatItem(originalItem), str);

                FixDataSource(originalItem, copyItem, args);

                var originalChildren = originalItem.Axes.GetDescendants();
                var copyChildren = copyItem.Axes.GetDescendants();

                for (var j = 0; j < originalChildren.Count() && j < copyChildren.Count(); j++)
                {
                    FixDataSource(originalChildren[j], copyChildren[j], args);
                }
            }

            args.Copies = new ArrayList().ToArray(typeof(Item)) as Item[];
        }

        /// <summary>
        /// The on item added.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        public void OnItemAdded(object sender, EventArgs args)
        {
            var item = Event.ExtractParameter(args, 0) as Item;

            if (item == null || item.Database == null)
            {
                return;
            }

            if (string.CompareOrdinal(item.Database.Name, this.Database) != 0)
            {
                return;
            }

            // check if the branch-item (or better - the child of the Branch-Item) even has sublayouts with assigned datasources - old ones don't - let them alone
            if (item.Branch != null && item.Branch.InnerItem != null && item.Branch.InnerItem.HasChildren && item.Branch.InnerItem.Children.Count == 1 && item.Branch.InnerItem.Children[0] != null)
            { // usually branches have just one child - for now, just handle these
                var branchChild = item.Branch.InnerItem.Children[0];
                FixDataSource(branchChild, item, null);

                // copy all languages if enabled (and only if the event is fired on the topmost item that was created
                if (item.TemplateID.ToString() == branchChild.TemplateID.ToString() && !string.IsNullOrEmpty(item.Branch.InnerItem["Create All Language Versions"]) && item.Branch.InnerItem["Create All Language Versions"] == "1")
                {
                    CopyLanguageVersions(branchChild, item);
                }
            }
        }

        /// <summary>
        /// copies all existing language versions from the branch template to the newly created items
        /// </summary>
        /// <param name="branchRootItem">The branch root item.</param>
        /// <param name="targetRootItem">The target root item.</param>
        private static void CopyLanguageVersions(Item branchRootItem, Item targetRootItem)
        {
            Assert.ArgumentNotNull(branchRootItem, "branchRootItem");
            Assert.ArgumentNotNull(targetRootItem, "targetRootItem");

            var items = targetRootItem.Branch.InnerItem.Axes.GetDescendants();

            if (items == null || items.Length <= 0)
            {
                return;
            }

            var branchRootPath = branchRootItem.Paths.FullPath;
            var targetRootPath = targetRootItem.Paths.FullPath;

            foreach (var sourceItem in items)
            {
                if (sourceItem == null)
                {
                    continue;
                }

                var targetItem = sourceItem.Database.GetItem(targetRootPath + sourceItem.Paths.FullPath.Replace(branchRootPath, string.Empty));
                if (targetItem == null)
                {
                    continue;
                }

                foreach (var lang in sourceItem.Languages)
                {
                    var branchVersion = sourceItem.Versions.GetLatestVersion(lang);
                    var targetVersion = targetItem.Versions.GetLatestVersion(lang);
                    if (branchVersion == null || targetVersion == null || branchVersion.Versions.Count <= 0
                        || targetVersion.Versions.Count != 0)
                    {
                        continue;
                    }

                    targetVersion.Versions.AddVersion();
                    targetVersion = targetItem.Versions.GetLatestVersion(lang);
                    var wasNotInEditMode = false;

                    if (!targetVersion.Editing.IsEditing)
                    {
                        targetVersion.Editing.BeginEdit();
                        wasNotInEditMode = true;
                    }

                    foreach (Field field in branchVersion.Fields)
                    {
                        if (!field.Shared)
                        {
                            targetVersion[field.Name] = branchVersion[field.Name];
                        }
                    }

                    if (wasNotInEditMode)
                    {
                        targetVersion.Editing.EndEdit();
                    }
                }
            }
        }

        /// <summary>
        /// Return the current Database 
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns>The database.</returns>
        private static Database GetDatabase(CopyItemsArgs args)
        {
            if (args == null)
            {
                Database db;
                try
                {
                    db = Factory.GetDatabase("master");
                }
                catch
                {
                    db = Factory.GetDatabase("web");
                }

                return db;
            }

            var str = args.Parameters["database"];
            var database = Factory.GetDatabase(str);
            Assert.IsNotNull(database, str);
            return database;
        }

        /// <summary>
        /// Gets the items.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>
        /// The items.
        /// </returns>
        private static List<Item> GetItems(CopyItemsArgs args)
        {
            var database = GetDatabase(args);

            return new ListString(args.Parameters["items"], '|').Select(database.GetItem).Where(obj => obj != null).ToList();
        }

        /// <summary>
        /// Fixes the data source.
        /// </summary>
        /// <param name="originalItem">
        /// The original item.
        /// </param>
        /// <param name="targetItem">
        /// The target item.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        private static void FixDataSource(Item originalItem, Item targetItem, CopyItemsArgs args)
        {
            if (targetItem == null)
            {
                return;
            }

            var renderingXml = targetItem["__Renderings"];
            var docXml = new XmlDocument();
            if (string.IsNullOrEmpty(renderingXml))
            {
                return;
            }

            docXml.LoadXml(renderingXml);
            var nodeList = docXml.SelectNodes("r/d/r");
            var wasFound = false;
            if (nodeList != null)
            {
                foreach (XmlNode node in nodeList)
                {
                    var attributes = node.Attributes;
                    if (attributes == null || (attributes["s:ds"] == null && attributes["ds"] == null))
                    {
                        continue;
                    }

                    var attributeName = attributes["s:ds"] != null ? "s:ds" : "ds";
                    var dataSourceValue = attributes[attributeName].Value;
                    if (string.IsNullOrEmpty(dataSourceValue))
                    {
                        continue;
                    }

                    var dataSourceItem = GetDatabase(args).GetItem(dataSourceValue);

                    if (dataSourceItem != null)
                    {
                        var relativePath = dataSourceItem.Paths.FullPath.Replace(
                            originalItem.Paths.FullPath,
                            string.Empty);
                        var newdataSourceItem = GetDatabase(args).GetItem(targetItem.Paths.FullPath + relativePath);
                        if (newdataSourceItem != null)
                        {
                            attributes[attributeName].Value = newdataSourceItem.ID.ToString();
                        }

                        wasFound = true;
                    }
                    else
                    {
                        attributes[attributeName].Value = string.Empty;
                    }
                }
            }

            if (!wasFound)
            {
                return;
            }

            using (new SecurityDisabler())
            {
                targetItem.Editing.BeginEdit();
                targetItem["__Renderings"] = docXml.InnerXml;
                targetItem.Editing.EndEdit();
            }
        }
    }
}