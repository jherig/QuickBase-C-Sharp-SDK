﻿/*
 * Copyright © 2013 Intuit Inc. All rights reserved.
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.opensource.org/licenses/eclipse-1.0.php
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Intuit.QuickBase.Core;
using Intuit.QuickBase.Core.Exceptions;
using System.Text.RegularExpressions;

namespace Intuit.QuickBase.Client
{
    public class QTable : IQTable
    {
        internal bool IsLoaded { get; private set; }

        // Constructors
        internal QTable(QColumnFactoryBase columnFactory, QRecordFactoryBase recordFactory, IQApplication application, string tableId)
        {
            if (columnFactory == null)
            {
                columnFactory = QColumnFactory.GetInstance();
                recordFactory = QRecordFactory.GetInstance();
                CommonConstruction(columnFactory, recordFactory, application, tableId);
                IsLoaded = false;
            }
            else
            {
                CommonConstruction(columnFactory, recordFactory, application, tableId);
                Load();
            }
        }

        internal QTable(QColumnFactoryBase columnFactory, QRecordFactoryBase recordFactory, IQApplication application, string tableName, string pNoun)
        {
            CreateTable createTable = new CreateTable.Builder(application.Client.Ticket, application.Token, application.Client.AccountDomain, application.ApplicationId)
                .SetTName(tableName)
                .SetPNoun(pNoun)
                .Build();
            XElement xml = createTable.Post();
            string tableId = xml.Element("newdbid").Value;

            TableName = tableName;
            RecordNames = pNoun;
            CommonConstruction(columnFactory, recordFactory, application, tableId);
            RefreshColumns(); //grab basic columns that QB automatically makes
            IsLoaded = true;
        }

        private void CommonConstruction(QColumnFactoryBase columnFactory, QRecordFactoryBase recordFactory, IQApplication application, string tableId)
        {
            ColumnFactory = columnFactory;
            RecordFactory = recordFactory;
            Application = application;
            TableId = tableId;
            Records = new QRecordCollection(Application, this);
            Columns = new QColumnCollection(Application, this);
            KeyFID = -1;
            KeyCIdx = -1;
        }

        // Properties
        private IQApplication Application { get; set; }

        public string TableId { get; private set; }

        public string TableName { get; private set; }

        public string RecordNames { get; private set; }

        public QRecordCollection Records { get; private set; }

        public QColumnCollection Columns { get; private set; }

        private QColumnFactoryBase ColumnFactory { get; set; }

        private QRecordFactoryBase RecordFactory { get; set; }

        public int KeyFID { get; private set; }

        public int KeyCIdx { get; private set; }

        private static readonly string[] QuerySeparator = {"}OR{"};
        private static readonly Regex QueryCheckRegex = new Regex(@"[\)}]AND[\({]");

        // Methods
        public void Clear()
        {
            Records.Clear();
            Columns.Clear();
        }

        public string GenCsv()
        {
            GenResultsTable genResultsTable = new GenResultsTable.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetOptions("csv")
                .Build();
            XElement xml = genResultsTable.Post();
            return xml.Value;
        }

        public string GenCsv(int queryId)
        {
            GenResultsTable genResultsTable = new GenResultsTable.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQid(queryId)
                .SetOptions("csv")
                .Build();
            XElement xml = genResultsTable.Post();
            return xml.Value;
        }

        public string GenCsv(Query query)
        {
            GenResultsTable genResultsTable = new GenResultsTable.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQuery(query.ToString())
                .SetOptions("csv")
                .Build();
            XElement xml = genResultsTable.Post();
            return xml.Value;
        }

        public string GenHtml(string options = "", string colList = "a")
        {
            GenResultsTable genResultsTable = new GenResultsTable.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetOptions(options)
                .SetCList(colList)
                .Build();
            XElement xml = genResultsTable.Post();
            return xml.Value;
        }

        public string GenHtml(int queryId, string options = "", string colList = "a")
        {
            GenResultsTable genResultsTable = new GenResultsTable.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQid(queryId)
                .SetOptions(options)
                .SetCList(colList)
                .Build();
            XElement xml = genResultsTable.Post();
            return xml.Value;
        }

        public string GenHtml(Query query, string options = "", string colList = "a")
        {
            GenResultsTable genResultsTable = new GenResultsTable.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQuery(query.ToString())
                .SetOptions(options)
                .SetCList(colList)
                .Build();
            XElement xml = genResultsTable.Post();
            return xml.Value;
        }

        public XElement GetTableSchema()
        {
            GetSchema tblSchema = new GetSchema(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId);
            return tblSchema.Post();
        }

        public TableInfo GetTableInfo()
        {
            GetDbInfo getTblInfo = new GetDbInfo(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId);
            XElement xml = getTblInfo.Post();

            string dbName = xml.Element("dbname").Value;
            long lastRecModTime = long.Parse(xml.Element("lastRecModTime").Value);
            long lastModifiedTime = long.Parse(xml.Element("lastModifiedTime").Value);
            long createTime = long.Parse(xml.Element("createdTime").Value);
            int numRecords = int.Parse(xml.Element("numRecords").Value);
            string mgrId = xml.Element("mgrID").Value;
            string mgrName = xml.Element("mgrName").Value;
            string version = xml.Element("version").Value;

            return new TableInfo(dbName, lastRecModTime, lastModifiedTime, createTime, numRecords, mgrId, mgrName,
                                    version);
        }

        public int GetServerRecordCount()
        {
            GetNumRecords tblRecordCount = new GetNumRecords(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId);
            XElement xml = tblRecordCount.Post();
            return int.Parse(xml.Element("num_records").Value);
        }

        private void DoQuery(DoQuery qry, bool clear)
        {
            if (clear) Records.Clear();
            try
            {
                XElement xml = qry.Post();
                LoadColumns(xml); //In case the schema changes due to another user, or from a previous query that has a differing subset of columns TODO: remove this requirement
                LoadRecords(xml);
            }
            catch (TooManyCriteriaInQueryException)
            {
                //If and only if all elements of a query are OR operations, we can split the query in 99 element chunks
                string query = qry.Query;
                if (string.IsNullOrEmpty(query) || QueryCheckRegex.IsMatch(query))
                    throw;
                string[] args = query.Split(QuerySeparator, StringSplitOptions.None);
                int argCnt = args.Length;
                if (argCnt < 100) //We've no idea how to split this, apparently...
                    throw;
                if (args[0].StartsWith("{")) args[0] = args[0].Substring(1); //remove leading {
                if (args[argCnt - 1].EndsWith("}")) args[argCnt - 1] = args[argCnt - 1].Substring(0, args[argCnt - 1].Length - 1); // remove trailing }
                if (args[argCnt - 1].EndsWith("}OR")) args[argCnt - 1] = args[argCnt - 1].Substring(0, args[argCnt - 1].Length - 3); // remove trailing }OR
                int sentArgs = 0;
                while (sentArgs < argCnt)
                {
                    int useArgs = Math.Min(99, argCnt - sentArgs);
                    string[] argsToSend = args.Skip(sentArgs).Take(useArgs).ToArray();
                    string sendQuery = "{" + string.Join("}OR{", argsToSend) + "}";
                    DoQuery.Builder qBuild = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId).SetQuery(sendQuery).SetFmt(true);
                    if (!string.IsNullOrEmpty(qry.Collist)) qBuild = qBuild.SetCList(qry.Collist);
                    if (!string.IsNullOrEmpty(qry.Options)) qBuild = qBuild.SetOptions(qry.Options);
                    DoQuery dqry = qBuild.Build();
                    XElement xml = dqry.Post();
                    if (sentArgs == 0) LoadColumns(xml);
                    LoadRecords(xml);
                    sentArgs += useArgs;
                }
            }
            catch (Exception ex) when (ex is ViewTooLargeException || ex is OperationTookTooLongException)
            {
                //split into smaller queries auto-magically
                List<string> optionsList = new List<string>();
                string query = qry.Query;
                string collist = qry.Collist;
                int maxCount = 0;
                int baseSkip = 0;
                if (!string.IsNullOrEmpty(qry.Options))
                {
                    string[] optArry = qry.Options.Split('.');
                    foreach (string opt in optArry)
                    {
                        if (opt.StartsWith("num-"))
                        {
                            maxCount = int.Parse(opt.Substring(4));
                        }
                        else if (opt.StartsWith("skp-"))
                        {
                            baseSkip = int.Parse(opt.Substring(4));
                        }
                        else
                        {
                            optionsList.Add(opt);
                        }
                    }
                }
                if (maxCount == 0)
                {
                    DoQueryCount dqryCnt;
                    if (string.IsNullOrEmpty(query))
                        dqryCnt = new DoQueryCount.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId).Build();
                    else
                        dqryCnt = new DoQueryCount.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId).SetQuery(query).Build();
                    XElement cntXml = dqryCnt.Post();
                    maxCount = int.Parse(cntXml.Element("numMatches").Value);
                }
                int stride = maxCount / 2;
                int fetched = 0;
                while (fetched < maxCount)
                {
                    List<string> optLst = new List<string>();
                    optLst.AddRange(optionsList);
                    optLst.Add("skp-" + (fetched + baseSkip));
                    optLst.Add("num-" + stride);
                    string options = string.Join(".", optLst);
                    DoQuery dqry;
                    if (string.IsNullOrEmpty(query))
                        dqry = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                            .SetCList(collist)
                            .SetOptions(options)
                            .SetFmt(true)
                            .Build();
                    else
                        dqry = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                            .SetQuery(query)
                            .SetCList(collist)
                            .SetOptions(options)
                            .SetFmt(true)
                            .Build();
                    try
                    {
                        XElement xml = dqry.Post();
                        if (fetched == 0) LoadColumns(xml);
                        LoadRecords(xml);
                        fetched += stride;
                    }
                    catch (ViewTooLargeException)
                    {
                        stride /= 2;
                    }
                    catch (ApiRequestLimitExceededException extime)
                    {
                        TimeSpan waitTime = extime.WaitUntil - DateTime.Now;
                        System.Threading.Thread.Sleep(waitTime);
                    }
                }
            }
        }

        public void Query(bool clear = true)
        {
            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetCList("a")
                .SetFmt(true)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(string options, bool clear = true)
        {
            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetCList("a")
                .SetFmt(true)
                .SetOptions(options)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(int[] colList, bool clear = true)
        {
            string clmnList = GetColumnList(colList);

            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetCList(clmnList)
                .SetFmt(true)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(int[] colList, string options, bool clear = true)
        {
            string clmnList = GetColumnList(colList);

            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetCList(clmnList)
                .SetOptions(options)
                .SetFmt(true)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(Query query, bool clear = true)
        {
            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQuery(query.ToString())
                .SetCList("a")
                .SetFmt(true)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(Query query, string options, bool clear = true)
        {
            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQuery(query.ToString())
                .SetCList("a")
                .SetOptions(options)
                .SetFmt(true)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(Query query, int[] colList, bool clear = true)
        {
            string clmnList = GetColumnList(colList);

            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQuery(query.ToString())
                .SetCList(clmnList)
                .SetFmt(true)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(Query query, int[] colList, int[] sortList, bool clear = true)
        {
            string solList = GetSortList(sortList);
            string clmnList = GetColumnList(colList);

            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQuery(query.ToString())
                .SetCList(clmnList)
                .SetSList(solList)
                .SetFmt(true)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(Query query, int[] colList, int[] sortList, string options, bool clear = true)
        {
            string solList = GetSortList(sortList);
            string clmnList = GetColumnList(colList);

            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQuery(query.ToString())
                .SetCList(clmnList)
                .SetSList(solList)
                .SetOptions(options)
                .SetFmt(true)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(int queryId, bool clear = true)
        {
            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQid(queryId)
                .SetFmt(true)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public void Query(int queryId, string options, bool clear = true)
        {
            DoQuery doQuery = new DoQuery.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQid(queryId)
                .SetFmt(true)
                .SetOptions(options)
                .Build();
            this.DoQuery(doQuery, clear);
        }

        public int QueryCount(Query query)
        {
            DoQueryCount doQuery = new DoQueryCount.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQuery(query.ToString())
                .Build();
            XElement xml = doQuery.Post();
            return int.Parse(xml.Element("numMatches").Value);
        }

        public int QueryCount(int queryId)
        {
            DoQueryCount doQuery = new DoQueryCount.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQid(queryId)
                .Build();
            XElement xml = doQuery.Post();
            return int.Parse(xml.Element("numMatches").Value);
        }

        public void PurgeRecords()
        {
            PurgeRecords purge = new PurgeRecords.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId).Build();
            purge.Post();
            Records.Clear();
        }

        public void PurgeRecords(int queryId)
        {
            PurgeRecords purge = new PurgeRecords.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQid(queryId)
                .Build();
            purge.Post();
            Records.Clear();
        }

        public void PurgeRecords(Query query)
        {
            PurgeRecords purge = new PurgeRecords.Builder(Application.Client.Ticket, Application.Token, Application.Client.AccountDomain, TableId)
                .SetQuery(query.ToString())
                .Build();
            purge.Post();
            Records.Clear();
        }

        public void AcceptChanges()
        {
            Records.RemoveRecords();
            foreach (IQColumn_int col in Columns)
            {
                col.AcceptChanges(Application, TableId);
            }
            //optimize record uploads
            List<IQRecord> addRecs = Records.Where(record => record.RecordState == RecordState.New && record.UncleanState == false).ToList();
            List<IQRecord> modRecs = Records.Where(record => record.RecordState == RecordState.Modified && record.UncleanState == false).ToList();
            List<IQRecord> uncleanRecs = Records.Where(record => record.UncleanState).ToList();
            int acnt = addRecs.Count;
            int mcnt = modRecs.Count;
            bool hasFileColumn = Columns.Any(c => c.ColumnType == FieldType.file);
            if (!hasFileColumn && ((acnt + mcnt) > 0))  // if no file-type columns involved, use csv upload method for reducing API calls and speeding processing.
            {
                List<String> csvLines = new List<string>(acnt + mcnt);
                String colList = String.Join(".", KeyFID == -1 ? Columns.Where(col => (col.ColumnVirtual == false && col.ColumnLookup == false && col.ColumnSummary == false && string.IsNullOrEmpty(col.ColumnRole)) || col.ColumnType == FieldType.recordid).Select(col => col.ColumnId.ToString())
                                                             : Columns.Where(col => (col.ColumnVirtual == false && col.ColumnLookup == false && col.ColumnSummary == false && string.IsNullOrEmpty(col.ColumnRole)) || col.ColumnId == KeyFID).Select(col => col.ColumnId.ToString()));
                if (acnt > 0)
                {
                    csvLines.AddRange(addRecs.Select(record => record.GetAsCSV(colList)));
                }
                if (mcnt > 0)
                {
                    csvLines.AddRange(modRecs.Select(record => record.GetAsCSV(colList)));
                }
                ImportFromCSV.Builder csvBuilder = new ImportFromCSV.Builder(Application.Client.Ticket, Application.Token,
                    Application.Client.AccountDomain, TableId, String.Join("\r\n", csvLines.ToArray()));
                csvBuilder.SetCList(colList);
                csvBuilder.SetTimeInUtc(true);
                ImportFromCSV csvUpload = csvBuilder.Build();

                XElement xml = csvUpload.Post();

                if (acnt > 0) // set in-memory recordId with server value for all newly added values
                {
                    XElement xRids = xml.Element("rids");
                    using (IEnumerator<XElement> xNodes = xRids.Elements("rid").GetEnumerator())
                    {
                        //set records as in server now
                        foreach (IQRecord rec in addRecs)
                        {
                            xNodes.MoveNext();
                            ((IQRecord_int) rec).ForceUpdateState(
                                Int32.Parse(xNodes.Current.Value));
                        }

                        foreach (IQRecord rec in modRecs)
                        {
                            ((IQRecord_int) rec).ForceUpdateState();
                        }
                    }
                }
            }
            else
            {
                foreach (IQRecord rec in addRecs)
                    rec.AcceptChanges();
                foreach (IQRecord rec in modRecs)
                    rec.AcceptChanges();
            }
            foreach (IQRecord rec in uncleanRecs)
                rec.AcceptChanges();
        }

        public IQRecord NewRecord()
        {
            return RecordFactory.CreateInstance(Application, this, Columns);
        }

        public override string ToString()
        {
            return TableName;
        }

        internal void Load()
        {
            TableName = GetTableInfo().DbName;
            RefreshColumns();
            IsLoaded = true;
        }

        private void LoadRecords(XElement xml)
        {
            foreach (XElement recordNode in xml.Element("table").Element("records").Elements("record"))
            {
                IQRecord record = RecordFactory.CreateInstance(Application, this, Columns, recordNode);
                Records.Add(record);
            }
        }

        public void RefreshColumns()
        {
            LoadColumns(GetTableSchema());
        }

        private void LoadColumns(XElement xml)
        {
            Columns.Clear();
            IEnumerable<XElement> columnNodes = xml.Element("table").Element("fields").Elements("field");
            foreach (XElement columnNode in columnNodes)
            {
                int columnId = int.Parse(columnNode.Attribute("id").Value);
                FieldType type =
                    (FieldType) Enum.Parse(typeof(FieldType), columnNode.Attribute("field_type").Value, true);
                string label = columnNode.Element("label").Value;
                bool hidden = columnNode.Element("appears_by_default")?.Value == "0";
                bool canAddChoices = columnNode.Element("allow_new_choices")?.Value == "1";
                string role = null;
                if (columnNode.Attribute("role") != null)
                {
                    role = columnNode.Attribute("role").Value;
                }
                bool virt = false, lookup = false, summary = false;
                if (columnNode.Attribute("mode") != null)
                {
                    string mode = columnNode.Attribute("mode").Value;
                    virt = mode == "virtual";
                    lookup = mode == "lookup";
                    summary = mode == "summary";
                }

                bool allowHTML = columnNode.Element("allowHTML")?.Value == "1";
                IQColumn col = ColumnFactory.CreateInstance(columnId, label, type, role, virt, lookup, summary, hidden, allowHTML, canAddChoices);
                if (columnNode.Element("choices") != null)
                {
                    foreach (XElement choicenode in columnNode.Element("choices").Elements("choice"))
                    {
                        object value;
                        switch (type)
                        {
                            case FieldType.rating:
                                value = Int32.Parse(choicenode.Value);
                                break;
                            default:
                                value = choicenode.Value;
                                break;
                        }
                        ((IQColumn_int) col).AddChoice(value, true);
                    }
                }
                Dictionary<string, int> colComposites = ((IQColumn_int)col).GetComposites();
                if (columnNode.Element("compositeFields") != null)
                {
                    foreach (XElement compositenode in columnNode.Element("compositeFields").Elements("compositeField"))
                    {
                        colComposites.Add(compositenode.Attribute("key").Value,
                            Int32.Parse(compositenode.Attribute("id").Value));
                    }
                }
                Columns.Add(col);
            }
            XElement keyFidNode = xml.Element("table").Element("original").Element("key_fid");
            KeyFID = keyFidNode != null ? Int32.Parse(keyFidNode.Value) : Columns.Find(c => c.ColumnType == FieldType.recordid).ColumnId;
            KeyCIdx = Columns.FindIndex(c => c.ColumnId == KeyFID);
        }

        private static string GetColumnList(ICollection<int> colList)
        {
            if (colList.Count > 0)
            {
                const int RECORDID_COLUMN_ID = 3;
                string columns = String.Empty;
                List<int> columnList = new List<int>(colList.Count + 1) { RECORDID_COLUMN_ID };
                columnList.AddRange(colList.Where(columnId => columnId != RECORDID_COLUMN_ID));

                // Seed the list with the column ID of Record#ID

                columns = columnList.Aggregate(columns, (current, columnId) => current + (columnId + "."));
                return columns.TrimEnd('.');
            }
            return "a";
        }

        private static string GetSortList(IEnumerable<int> sortList)
        {
            string solList = sortList.Aggregate(String.Empty, (current, sol) => current + (sol + "."));
            return solList.TrimEnd('.');
        }
    }
}