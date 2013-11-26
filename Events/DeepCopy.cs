using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.SecurityModel;
using Sitecore.Shell.Framework.Pipelines;
using Sitecore.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;

namespace Nemetos.DeepCopy.Events
{
    public class DeepCopy
    {
        /// <summary>
        /// Fix the datasource for the copying items
        /// 
        /// </summary>
        /// <param name="args">The arguments.</param><contract><requires name="args" condition="not null"/></contract>
        public virtual void Execute(CopyItemsArgs args)
        {
            Assert.ArgumentNotNull((object)args, "args");
            Item destination = GetDatabase(args).GetItem(args.Parameters["destination"]);
            Assert.IsNotNull((object)destination, args.Parameters["destination"]);
            ArrayList arrayList = new ArrayList();
            List<Item> items = GetItems(args);
            string str = ((object)destination.Uri).ToString();
            foreach (Item obj1 in items)
            {
                if (obj1 != null)
                {
                    Log.Audit((object)this, "Copy item: {0}", AuditFormatter.FormatItem(obj1), str);
                    var currentItem = obj1;
                    Item[] childItems = obj1.Axes.GetDescendants();
                    FixDataSource(currentItem, destination.Children[obj1.Name], args);
                    foreach (var item in childItems)
                    {
                        // path of the item that will be copy
                        string itemPath = item.Paths.FullPath;
                        //parent of the root item that will copy
                        string currentPath = currentItem.Paths.FullPath;
                        // destination item path
                        string destinationPath = destination.Paths.FullPath;
                        //relative path to the root parent
                        string relativePath = obj1.Name + itemPath.Replace(currentPath, "");
                        // item copied  
                        var relativeItem = GetDatabase(args).GetItem(destinationPath + "/" + relativePath);
                        FixDataSource(item, relativeItem, args);
                    }
                }
            }
            args.Copies = arrayList.ToArray(typeof(Item)) as Item[];
        }
        /// <summary>
        /// Return the current Database 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Database GetDatabase(CopyItemsArgs args)
        {
            string str = args.Parameters["database"];
            Database database = Factory.GetDatabase(str);
            Assert.IsNotNull((object)database, str);
            return database;
        }

        /// <summary>
        /// Gets the items.
        /// 
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>
        /// The items.
        /// </returns>
        private List<Item> GetItems(CopyItemsArgs args)
        {
            List<Item> list = new List<Item>();
            Database database = GetDatabase(args);
            foreach (string path in new ListString(args.Parameters["items"], '|'))
            {
                Item obj = database.GetItem(path);
                if (obj != null)
                    list.Add(obj);
            }
            return list;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="originalItem"></param>
        /// <param name="targetItem"></param>
        private void FixDataSource(Item originalItem, Item targetItem, CopyItemsArgs args)
        {
            //get rendering and parsing xml 
            if (targetItem != null)
            {
                string renderingXml = targetItem["__Renderings"];
                XmlDocument docXml = new XmlDocument();
                if (!string.IsNullOrEmpty(renderingXml))
                {
                    docXml.LoadXml(renderingXml);
                    XmlNodeList nodeList = docXml.SelectNodes("r/d/r");
                    bool wasFound = false;
                    foreach (XmlNode node in nodeList)
                    {
                        XmlAttributeCollection attributes = node.Attributes;
                        if (attributes["s:ds"] != null)
                        {
                            string dataSourceValue = attributes["s:ds"].Value;
                            if (!string.IsNullOrEmpty(dataSourceValue))
                            {
                                Item dataSourceItem = GetDatabase(args).GetItem(dataSourceValue);

                                if (dataSourceItem != null)
                                {
                                    //get relative path for spots,tab,accordeon,etc 
                                    string relativePath = dataSourceItem.Paths.FullPath.Replace(originalItem.Paths.FullPath, "");
                                    Item newdataSourceItem = GetDatabase(args).GetItem(targetItem.Paths.FullPath + relativePath);
                                    if (newdataSourceItem != null)
                                        attributes["s:ds"].Value = newdataSourceItem.ID.ToString();
                                    wasFound = true;
                                }
                                else
                                    attributes["s:ds"].Value = string.Empty;
                            }
                        }
                    }
                    using (new SecurityDisabler())
                    {
                        if (wasFound)
                        {
                            targetItem.Editing.BeginEdit();
                            targetItem["__Renderings"] = docXml.InnerXml;
                            targetItem.Editing.EndEdit();
                        }
                    }
                }
            }
        }
    }
}