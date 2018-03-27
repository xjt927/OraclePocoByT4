﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TextTemplating;
using XJT.Com.EntitySql.Common;

namespace XJT.Com.EntitySql.DatabaseSource
{
    class GenerateCore
    {
        static string constraintTableName = "";
        string ConnectionStringName = "";
        public string ConnectionStringResult = "";
        public string ProviderNameResult = "";
        string Priv = "";
        string Namespace = "";
        string ClassPrefix = "";
        string ClassSuffix = "";
        string SchemaName = null;
        bool IncludeViews = true;
        bool IncludeFunctions = false;
        public static string TableNames = "";
        public static string TableNameLike = "";

        /// <summary>
        /// 转换为大小写操作
        /// </summary> 
        private class LetterConvert
        {
            /// <summary>
            /// 字符串转换成大写
            /// </summary>
            /// <returns></returns>
            public static string StrToUpper(string str)
            {
                return str.ToUpper();
            }
            /// <summary>
            /// 字符串转换成小写
            /// </summary>
            /// <returns></returns>
            public static string StrToLower(string str)
            {
                return str.ToLower();
            }

            /// <summary>
            /// 首字母大写，其他字母小写
            /// </summary>
            /// <param name="str"></param>
            /// <returns></returns>
            public static string ConvertStr(string str)
            {
                string[] strArr = str.Split('_');
                var result = new StringBuilder();
                strArr.ToList().ForEach(item =>
                {
                    if (item.Length > 1)
                    {
                        string first = item.Substring(0, 1);
                        string content = item.Substring(1);
                        result.Append(first + StrToLower(content));
                    }
                });
                return result.ToString();
            }
        }

        public class Table
        {
            public List<Column> Columns;
            public List<TableIndex> Indices;
            public List<FKey> FKeys;
            public string Name;
            public string Schema;
            public string Comments;//Todo:这里修改 
            public bool IsView;
            public bool IsFunction;
            public string CleanName;
            public string ClassName;
            public string SequenceName;
            public bool Ignore;
            public string SQL;

            public Column PK
            {
                get
                {
                    return this.Columns.SingleOrDefault(x => x.IsPK);
                }
            }

            public Column GetColumn(string columnName)
            {
                return Columns.Single(x => string.Compare(x.Name, columnName, true) == 0);
            }

            public Column this[string columnName]
            {
                get
                {
                    return GetColumn(columnName);
                }
            }

            public bool HasPK()
            {
                return ((PK != null) && (string.IsNullOrEmpty(PK.Name) != true));
            }
            public TableIndex GetIndex(string indexName)
            {
                return Indices.Single(x => string.Compare(x.Name, indexName, true) == 0);
            }
        }

        public class ConstraintTable
        {
            public string ConstraintName;
            public string OrTableName;
            public string OrColumnName;
            public string TableName;
            public string ColumnName;
            public string TableComments;
        }

        public class Column
        {
            public string Name;
            public string PropertyName;
            public string PropertyType;
            public bool IsPK;
            public bool IsNullable;
            public bool IsAutoIncrement;
            public bool IsComputed;
            public bool Ignore;
            public int Size;
            public int Precision;
            public string DefaultValue;
            public string Comments;//Todo:这里修改 
            public string ProperPropertyType
            {
                get
                {
                    if (IsNullable)
                    {
                        return PropertyType + CheckNullable(this);
                    }
                    return PropertyType;
                }
            }
        }

        public class ConstraintTables : List<ConstraintTable>
        {
            public ConstraintTables()
            {
            }

            public ConstraintTable GetConstraintTable(string tableName)
            {
                return this.Single(x => string.Compare(x.OrTableName, tableName, true) == 0);
            }

            public ConstraintTable this[string tableName]
            {
                get
                {
                    return GetConstraintTable(tableName);
                }
            }

        }

        public class Tables : List<Table>
        {
            public Tables()
            {
            }

            public Table GetTable(string tableName)
            {
                return this.Single(x => string.Compare(x.Name, tableName, true) == 0);
            }

            public Table this[string tableName]
            {
                get
                {
                    return GetTable(tableName);
                }
            }

        }

        public class IndexColumn
        {
            public string Name;
            public bool IsAsc;
        }

        public class TableIndex
        {
            public string Name;
            public List<IndexColumn> IndexColumns;
            public bool IsUnique;
            public string SQL;
        }

        public class FKey
        {
            public string ToTable;
            public string FromColumn;
            public string ToColumn;
        }

        public class SP
        {
            public string Name;
            public string CleanName;
            public string ClassName;
            public string Schema;
            public string SchemaQualifiedName { get { return Schema + "." + Name; } }
            public List<SPParam> Parameters;
            public SP()
            {
                Parameters = new List<SPParam>();
            }
            public string ArgList
            {
                get
                {
                    StringBuilder sb = new StringBuilder();
                    int loopCount = 1;
                    foreach (var par in Parameters)
                    {
                        sb.AppendFormat("{0} {1}", par.SysType, par.CleanName);
                        if (loopCount < Parameters.Count)
                            sb.Append(",");
                        loopCount++;
                    }
                    return sb.ToString();
                }
            }
        }

        public enum SPParamDir
        {
            OutDirection,
            InDirection,
            InAndOutDirection
        }

        public class SPParam
        {
            public string Name;
            public string CleanName;
            public string SysType;
            public string NullableSysType;
            public string DbType;
            public SPParamDir Direction;
        }


        static Regex rxCleanUp = new Regex(@"[^\w\d_]", RegexOptions.Compiled);

        static string[] cs_keywords = { "abstract", "event", "new", "struct", "as", "explicit", "null",
     "switch", "base", "extern", "object", "this", "bool", "false", "operator", "throw",
     "break", "finally", "out", "true", "byte", "fixed", "override", "try", "case", "float",
     "params", "typeof", "catch", "for", "private", "uint", "char", "foreach", "protected",
     "ulong", "checked", "goto", "public", "unchecked", "class", "if", "readonly", "unsafe",
     "const", "implicit", "ref", "ushort", "continue", "in", "return", "using", "decimal",
     "int", "sbyte", "virtual", "default", "interface", "sealed", "volatile", "delegate",
     "internal", "short", "void", "do", "is", "sizeof", "while", "double", "lock",
     "stackalloc", "else", "long", "static", "enum", "namespace", "string" };

        static Func<string, string> CleanUp = (str) =>
        {
            str = rxCleanUp.Replace(str, "_");

            if (char.IsDigit(str[0]) || cs_keywords.Contains(str))
                str = "@" + str;

            return str;
        };

        static string CheckNullable(Column col)
        {
            string result = "";
            if (col.IsNullable &&
                col.PropertyType != "byte[]" &&
                col.PropertyType != "string" &&
                col.PropertyType != "Microsoft.SqlServer.Types.SqlGeography" &&
                col.PropertyType != "Microsoft.SqlServer.Types.SqlGeometry"
                )
                result = "?";
            return result;
        }

        string GetConnectionString(ref string connectionStringName, out string providerName)
        {
            var _CurrentProject = GetCurrentProject();

            providerName = null;

            string result = "";
            ExeConfigurationFileMap configFile = new ExeConfigurationFileMap();
            configFile.ExeConfigFilename = GetConfigPath();

            if (string.IsNullOrEmpty(configFile.ExeConfigFilename))
                throw new ArgumentNullException("The project does not contain App.config or Web.config file.");


            var config = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(configFile, ConfigurationUserLevel.None);
            var connSection = config.ConnectionStrings;

            //if the connectionString is empty - which is the defauls
            //look for count-1 - this is the last connection string
            //and takes into account AppServices and LocalSqlServer
            if (string.IsNullOrEmpty(connectionStringName))
            {
                if (connSection.ConnectionStrings.Count > 1)
                {
                    connectionStringName = connSection.ConnectionStrings[connSection.ConnectionStrings.Count - 1].Name;
                    result = connSection.ConnectionStrings[connSection.ConnectionStrings.Count - 1].ConnectionString;
                    providerName = connSection.ConnectionStrings[connSection.ConnectionStrings.Count - 1].ProviderName;
                }
            }
            else
            {
                try
                {
                    result = connSection.ConnectionStrings[connectionStringName].ConnectionString;
                    providerName = connSection.ConnectionStrings[connectionStringName].ProviderName;
                }
                catch
                {
                    result = "There is no connection string name called '" + connectionStringName + "'";
                }
            }

            //	if (String.IsNullOrEmpty(providerName))
            //		providerName="System.Data.SqlClient";

            return result;
        }

        string _connectionString = "";
        string _providerName = "";

        void InitConnectionString()
        {
            _connectionString = ConnectionStringResult;
            _providerName = ProviderNameResult;
            if (String.IsNullOrEmpty(_connectionString) || String.IsNullOrEmpty(_providerName))
            {
                _connectionString = GetConnectionString(ref ConnectionStringName, out _providerName);

                if (_connectionString.Contains("|DataDirectory|"))
                {
                    //have to replace it
                    string dataFilePath = GetDataDirectory();
                    _connectionString = _connectionString.Replace("|DataDirectory|", dataFilePath);
                }
            }
        }

        public string ConnectionString
        {
            get
            {
                InitConnectionString();
                return _connectionString;
            }
        }

        public string ProviderName
        {
            get
            {
                InitConnectionString();
                return _providerName;
            }
        }

        public EnvDTE.Project GetCurrentProject()
        {
            var dte = EnvDTEHelper.GetIntegrityServiceInstance();
            EnvDTE.Project pro = null;
            if (dte != null)
            {
                pro = (EnvDTE.Project)dte.Solution.Projects;
            }
            return pro;

            //IServiceProvider _ServiceProvider = (IServiceProvider)Host;
            //if (_ServiceProvider == null)
            //    throw new Exception("Host property returned unexpected value (null)");

            //EnvDTE.DTE dte = (EnvDTE.DTE)_ServiceProvider.GetService(typeof(EnvDTE.DTE));
            //if (dte == null)
            //    throw new Exception("Unable to retrieve EnvDTE.DTE");

            //Array activeSolutionProjects = (Array)dte.ActiveSolutionProjects;
            //if (activeSolutionProjects == null)
            //    throw new Exception("DTE.ActiveSolutionProjects returned null");

            //EnvDTE.Project dteProject = (EnvDTE.Project)activeSolutionProjects.GetValue(0);
            //if (dteProject == null)
            //    throw new Exception("DTE.ActiveSolutionProjects[0] returned null");

            //return dteProject;

        }

        private string GetProjectPath()
        {
            EnvDTE.Project project = GetCurrentProject();
            System.IO.FileInfo info = new System.IO.FileInfo(project.FullName);
            return info.Directory.FullName;
        }

        private string GetConfigPath()
        {
            EnvDTE.Project project = GetCurrentProject();
            foreach (EnvDTE.ProjectItem item in project.ProjectItems)
            {
                // if it is the app.config file, then open it up
                if (item.Name.Equals("App.config", StringComparison.InvariantCultureIgnoreCase) || item.Name.Equals("Web.config", StringComparison.InvariantCultureIgnoreCase))
                    return GetProjectPath() + "\\" + item.Name;
            }
            return String.Empty;
        }

        public string GetDataDirectory()
        {
            EnvDTE.Project project = GetCurrentProject();
            return System.IO.Path.GetDirectoryName(project.FileName) + "\\App_Data\\";
        }

        static string zap_password(string connectionString)
        {
            var rx = new Regex("password=.*;", RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return rx.Replace(connectionString, "password=**zapped**;");
        }


        public string GetColumnDefaultValue(Column col)
        {
            string sysType = string.Format("\"{0}\"", col.DefaultValue);
            switch (col.PropertyType.ToLower())
            {
                case "long":
                case "int":
                case "double":
                case "decimal":
                case "bool":
                    sysType = col.DefaultValue.ToString().Replace("'", "").Replace("\"", "");
                    break;
                case "guid":
                    sysType = string.Format("\"{0}\"", col.DefaultValue);
                    break;
                case "datetime":
                    {
                        if (col.DefaultValue.ToLower() == "current_time" || col.DefaultValue.ToLower() == "current_date" || col.DefaultValue.ToLower() == "current_timestamp")
                            sysType = "SystemMethods.CurrentDateTime";
                        else
                            sysType = "\"" + col.DefaultValue + "\"";
                        break;
                    }
            }
            return sysType;
        }

        public Tables LoadTables(bool makeSingular)
        {
            InitConnectionString();

            //Tools.LogAction("// This file was automatically generated by the PetaPoco T4 Template", EnumCommon.LogEnum.CommonLog);
            //WriteLine("// Do not make changes directly to this file - edit the template instead");
            //WriteLine("// ");
            //WriteLine("// The following connection settings were used to generate this file");
            //WriteLine("// ");
            //WriteLine("//     Connection String Name: `{0}`", ConnectionStringName);
            //WriteLine("//     Provider:               `{0}`", ProviderName);
            //WriteLine("//     Connection String:      `{0}`", zap_password(ConnectionString));
            //WriteLine("//     Schema:                 `{0}`", SchemaName);
            //WriteLine("//     Include Views:          `{0}`", IncludeViews);
            //WriteLine("");

            DbProviderFactory _factory;
            try
            {
                _factory = DbProviderFactories.GetFactory(ProviderName);
            }
            catch (Exception x)
            {
                var error = x.Message.Replace("\r\n", "\n").Replace("\n", " ");
                //Warning(string.Format("Failed to load provider `{0}` - {1}", ProviderName, error));
                //WriteLine("");
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("// Failed to load provider `{0}` - {1}", ProviderName, error);
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("");
                return new Tables();
            }
            //Tools.LogAction(string.Format("//     Factory Name:          `{0}`", _factory.GetType().Name), EnumCommon.LogEnum.CommonLog);

            try
            {
                Tables result;
                using (var conn = _factory.CreateConnection())
                {
                    conn.ConnectionString = ConnectionString;
                    conn.Open();

                    SchemaReader reader = null;

                    if (_factory.GetType().Name == "MySqlClientFactory")
                    {
                        // MySql
                        reader = new MySqlSchemaReader();
                    }
                    else if (_factory.GetType().Name == "SqlCeProviderFactory")
                    {
                        // SQL CE
                        reader = new SqlServerCeSchemaReader();
                    }
                    else if (_factory.GetType().Name == "NpgsqlFactory")
                    {
                        // PostgreSQL
                        reader = new PostGreSqlSchemaReader();
                    }
                    else if (_factory.GetType().Name == "OracleClientFactory")
                    {
                        // Oracle
                        reader = new OracleSchemaReader();
                    }
                    else if (_factory.GetType().Name == "SQLiteFactory")
                    {
                        // Sqlite
                        reader = new SqliteSchemaReader();
                    }
                    else
                    {
                        // Assume SQL Server
                        reader = new SqlServerSchemaReader();
                    }

                    //reader.outer = this;
                    result = reader.ReadSchema(conn, _factory);

                    // Remove unrequired tables/views
                    for (int i = result.Count - 1; i >= 0; i--)
                    {
                        if (SchemaName != null && string.Compare(result[i].Schema, SchemaName, true) != 0)
                        {
                            result.RemoveAt(i);
                            continue;
                        }
                        if ((!IncludeViews && result[i].IsView) || (!IncludeFunctions && result[i].IsFunction))
                        {
                            result.RemoveAt(i);
                            continue;
                        }
                    }
                }

                var rxClean = new Regex("^(Equals|GetHashCode|GetType|ToString|repo|Save|IsNew|Insert|Update|Delete|Exists|SingleOrDefault|Single|First|FirstOrDefault|Fetch|Page|Query)$");
                foreach (var t in result)
                {
                    if (!makeSingular)
                    {
                        t.ClassName = t.CleanName;
                    }
                    t.ClassName = ClassPrefix + t.ClassName + ClassSuffix;

                    foreach (var c in t.Columns)
                    {
                        c.PropertyName = rxClean.Replace(c.PropertyName, "_$1");

                        // Make sure property name doesn't clash with class name
                        if (c.PropertyName == t.ClassName)
                            c.PropertyName = "_" + c.PropertyName;
                    }
                }

                return result;

            }
            catch (Exception x)
            {
                var error = x.Message.Replace("\r\n", "\n").Replace("\n", " ");
                //Warning(string.Format("Failed to read database schema - {0}", error));
                //WriteLine(x.ToString());
                //WriteLine("");
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("// Failed to read database schema - {0}", error);
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("");
                return new Tables();
            }
        }

        public ConstraintTables LoadConstraints(string tableName)
        {
            constraintTableName = tableName;
            DbProviderFactory _factory;
            try
            {
                _factory = DbProviderFactories.GetFactory(ProviderName);
            }
            catch (Exception x)
            {
                var error = x.Message.Replace("\r\n", "\n").Replace("\n", " ");
                //Warning(string.Format("Failed to load provider `{0}` - {1}", ProviderName, error));
                //WriteLine("");
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("// Failed to load provider `{0}` - {1}", ProviderName, error);
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("");
                return new ConstraintTables();
            }
            //WriteLine("//     Factory Name:          `{0}`", _factory.GetType().Name);

            try
            {
                ConstraintTables result;
                using (var conn = _factory.CreateConnection())
                {
                    conn.ConnectionString = ConnectionString;
                    conn.Open();

                    SchemaReader reader = null;

                    if (_factory.GetType().Name == "MySqlClientFactory")
                    {
                        // MySql
                        reader = new MySqlSchemaReader();
                    }
                    else if (_factory.GetType().Name == "SqlCeProviderFactory")
                    {
                        // SQL CE
                        reader = new SqlServerCeSchemaReader();
                    }
                    else if (_factory.GetType().Name == "NpgsqlFactory")
                    {
                        // PostgreSQL
                        reader = new PostGreSqlSchemaReader();
                    }
                    else if (_factory.GetType().Name == "OracleClientFactory")
                    {
                        // Oracle
                        reader = new OracleSchemaReader();
                    }
                    else if (_factory.GetType().Name == "SQLiteFactory")
                    {
                        // Sqlite
                        reader = new SqliteSchemaReader();
                    }
                    else
                    {
                        // Assume SQL Server
                        reader = new SqlServerSchemaReader();
                    }

                    //reader.outer = this;
                    result = reader.ConstraintList(conn, _factory);

                }

                return result;

            }
            catch (Exception x)
            {
                var error = x.Message.Replace("\r\n", "\n").Replace("\n", " ");
                //Warning(string.Format("Failed to read database schema - {0}", error));
                //WriteLine(x.ToString());
                //WriteLine("");
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("// Failed to read ConstraintList schema - {0}", error);
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("");
                return new ConstraintTables();
            }
        }


        List<SP> SPsNotSupported(string providerName)
        {
            //Warning("SP function creation is not supported for " + providerName);
            //WriteLine("");
            //WriteLine("// -----------------------------------------------------------------------------------------");
            //WriteLine("// SP function creation is not supported for  `{0}`", providerName);
            //WriteLine("// -----------------------------------------------------------------------------------------");
            return new List<SP>();
        }

        List<SP> LoadSPs()
        {
            InitConnectionString();

            //WriteLine("// This file was automatically generated by the PetaPoco T4 Template");
            //WriteLine("// Do not make changes directly to this file - edit the template instead");
            //WriteLine("// ");
            //WriteLine("// The following connection settings were used to generate this file");
            //WriteLine("// ");
            //WriteLine("//     Connection String Name: `{0}`", ConnectionStringName);
            //WriteLine("//     Provider:               `{0}`", ProviderName);
            //WriteLine("//     Connection String:      `{0}`", zap_password(ConnectionString));
            //WriteLine("//     Schema:                 `{0}`", SchemaName);
            //WriteLine("//     Include Views:          `{0}`", IncludeViews);
            //WriteLine("");

            DbProviderFactory _factory;
            try
            {
                _factory = DbProviderFactories.GetFactory(ProviderName);
            }
            catch (Exception x)
            {
                var error = x.Message.Replace("\r\n", "\n").Replace("\n", " ");
                //Warning(string.Format("Failed to load provider `{0}` - {1}", ProviderName, error));
                //WriteLine("");
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("// Failed to load provider `{0}` - {1}", ProviderName, error);
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("");
                return new List<SP>();
            }
            //WriteLine("//     Factory Name:          `{0}`", _factory.GetType().Name);

            try
            {
                List<SP> result;
                using (var conn = _factory.CreateConnection())
                {
                    conn.ConnectionString = ConnectionString;
                    conn.Open();

                    SchemaReader reader = null;

                    if (_factory.GetType().Name == "MySqlClientFactory")
                    {
                        // MySql
                        reader = new MySqlSchemaReader();
                        return SPsNotSupported(ProviderName);
                    }
                    else if (_factory.GetType().Name == "SqlCeProviderFactory")
                    {
                        // SQL CE
                        reader = new SqlServerCeSchemaReader();
                        return SPsNotSupported(ProviderName);
                    }
                    else if (_factory.GetType().Name == "NpgsqlFactory")
                    {
                        // PostgreSQL
                        reader = new PostGreSqlSchemaReader();
                        return SPsNotSupported(ProviderName);
                    }
                    else if (_factory.GetType().Name == "OracleClientFactory")
                    {
                        // Oracle
                        reader = new OracleSchemaReader();
                        return SPsNotSupported(ProviderName);
                    }
                    else if (_factory.GetType().Name == "SQLiteFactory")
                    {
                        // Sqlite
                        reader = new SqliteSchemaReader();
                        return SPsNotSupported(ProviderName);
                    }
                    else
                    {
                        // Assume SQL Server
                        reader = new SqlServerSchemaReader();
                    }

                    ////reader.outer = this;
                    result = reader.ReadSPList(conn, _factory);
                    // Remove unrequired procedures
                    for (int i = result.Count - 1; i >= 0; i--)
                    {
                        if (SchemaName != null && string.Compare(result[i].Schema, SchemaName, true) != 0)
                        {
                            result.RemoveAt(i);
                            continue;
                        }
                    }
                }
                return result;
            }
            catch (Exception x)
            {
                var error = x.Message.Replace("\r\n", "\n").Replace("\n", " ");
                //Warning(string.Format("Failed to read database schema - {0}", error));
                //WriteLine("");
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("// Failed to read database schema - {0}", error);
                //WriteLine("// -----------------------------------------------------------------------------------------");
                //WriteLine("");
                return new List<SP>();
            }


        }

        public bool IsTableNameInList(string tableName, Tables tbls)
        {
            if (tbls == null)
                return false;
            foreach (var tbItem in tbls)
            {
                if (String.Equals(tbItem.Name, tableName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public Table GetTableFromListByName(string tableName, Tables tbls)
        {
            if (tbls == null)
                return null;
            foreach (var tbItem in tbls)
            {
                if (String.Equals(tbItem.Name, tableName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return tbItem;
                }
            }
            return null;
        }

        void SaveOutput(string outputFileName)
        {
            //string templateDirectory = Path.GetDirectoryName(Host.TemplateFile);
            //string outputFilePath = Path.Combine(templateDirectory, outputFileName);
            //File.WriteAllText(outputFilePath, this.GenerationEnvironment.ToString());

            //this.GenerationEnvironment.Remove(0, this.GenerationEnvironment.Length);
        }

        abstract class SchemaReader
        {
            public abstract Tables ReadSchema(DbConnection connection, DbProviderFactory factory);
            public abstract List<SP> ReadSPList(DbConnection connection, DbProviderFactory factory);
            public abstract ConstraintTables ConstraintList(DbConnection connection, DbProviderFactory factory);
            //public GeneratedTextTransformation outer;
            public void WriteLine(string o)
            {
                //outer.WriteLine(o);
            }

        }

        public static int GetDatatypePrecision(string type)
        {
            int startPos = type.IndexOf(",");
            if (startPos < 0)
                return -1;
            int endPos = type.IndexOf(")");
            if (endPos < 0)
                return -1;
            string typePrecisionStr = type.Substring(startPos + 1, endPos - startPos - 1);
            int result = -1;
            if (int.TryParse(typePrecisionStr, out result))
                return result;
            else
                return -1;
        }

        static int GetDatatypeSize(string type)
        {
            int startPos = type.IndexOf("(");
            if (startPos < 0)
                return -1;
            int endPos = type.IndexOf(",");
            if (endPos < 0)
            {
                endPos = type.IndexOf(")");
            }
            string typeSizeStr = type.Substring(startPos + 1, endPos - startPos - 1);
            int result = -1;
            if (int.TryParse(typeSizeStr, out result))
                return result;
            else
                return -1;
        }

        // Edit here to get a method to read the proc
        class SqlServerSchemaReader : SchemaReader
        {
            // SchemaReader.ReadSchema


            public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
            {
                var result = new Tables();

                _connection = connection;
                _factory = factory;

                var cmd = _factory.CreateCommand();
                cmd.Connection = connection;
                cmd.CommandText = TABLE_SQL;

                //pull the tables in a reader
                using (cmd)
                {

                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Table tbl = new Table();
                            tbl.Name = rdr["TABLE_NAME"].ToString();
                            tbl.Schema = rdr["TABLE_SCHEMA"].ToString();
                            tbl.IsView = string.Compare(rdr["TABLE_TYPE"].ToString(), "View", true) == 0;
                            tbl.IsFunction = string.Compare(rdr["TABLE_TYPE"].ToString(), "TVF", true) == 0;
                            tbl.CleanName = CleanUp(tbl.Name);
                            tbl.ClassName = Inflector.MakeSingular(tbl.CleanName);

                            result.Add(tbl);
                        }
                    }
                }

                foreach (var tbl in result)
                {
                    tbl.Columns = LoadColumns(tbl);

                    // Mark the primary key
                    string PrimaryKey = GetPK(tbl.Name);
                    var pkColumn = tbl.Columns.SingleOrDefault(x => x.Name.ToLower().Trim() == PrimaryKey.ToLower().Trim());
                    if (pkColumn != null)
                    {
                        pkColumn.IsPK = true;
                    }
                }


                return result;
            }

            public override List<SP> ReadSPList(DbConnection connection, DbProviderFactory factory)
            {
                var result = new List<SP>();

                _connection = connection;
                _factory = factory;

                var cmd = _factory.CreateCommand();
                cmd.Connection = connection;
                cmd.CommandText = SP_NAMES_SQL;

                //pull the tables in a reader
                using (cmd)
                {
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            SP sp = new SP
                            {
                                Name = rdr["sp_name"].ToString(),
                                Schema = rdr["schema_name"].ToString()
                            };
                            sp.CleanName = CleanUp(sp.Name);
                            sp.ClassName = Inflector.MakeSingular(sp.CleanName);
                            result.Add(sp);
                        }
                    }
                }
                foreach (var sp in result)
                {
                    sp.Parameters = LoadSPParams(sp);
                }
                return result;
            }

            public override ConstraintTables ConstraintList(DbConnection connection, DbProviderFactory factory)
            {
                return new ConstraintTables();
            }

            DbConnection _connection;
            DbProviderFactory _factory;

            List<Column> LoadColumns(Table tbl)
            {

                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = COLUMN_SQL;

                    var p = cmd.CreateParameter();
                    p.ParameterName = "@tableName";
                    p.Value = tbl.Name;
                    cmd.Parameters.Add(p);

                    p = cmd.CreateParameter();
                    p.ParameterName = "@schemaName";
                    p.Value = tbl.Schema;
                    cmd.Parameters.Add(p);

                    var result = new List<Column>();
                    using (IDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Column col = new Column { Name = rdr["ColumnName"].ToString() };
                            col.PropertyName = CleanUp(col.Name);
                            col.PropertyType = GetPropertyType(rdr["DataType"].ToString());
                            col.Size = GetDatatypeSize(rdr["DataType"].ToString());
                            col.Precision = GetDatatypePrecision(rdr["DataType"].ToString());
                            col.IsNullable = rdr["IsNullable"].ToString() == "YES";
                            col.IsAutoIncrement = ((int)rdr["IsIdentity"]) == 1;
                            col.IsComputed = ((int)rdr["IsComputed"]) == 1;
                            result.Add(col);
                        }
                    }

                    return result;
                }
            }

            List<SPParam> LoadSPParams(SP sp)
            {
                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = SP_PARAMETERS_SQL;

                    var p = cmd.CreateParameter();
                    p.ParameterName = "@spname";
                    p.Value = sp.Name;
                    cmd.Parameters.Add(p);

                    var result = new List<SPParam>();
                    using (IDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            if (rdr["IS_RESULT"].ToString().ToUpper() == "YES")
                                continue;
                            SPParam param = new SPParam();
                            param.SysType = GetPropertyType(rdr["DATA_TYPE"].ToString());
                            param.NullableSysType = GetNullablePropertyType(rdr["DATA_TYPE"].ToString());
                            param.DbType = GetDbType(rdr["DATA_TYPE"].ToString()).ToString();
                            param.Name = rdr["PARAMETER_NAME"].ToString().Replace("@", "");
                            param.CleanName = CleanUp(param.Name);
                            if (rdr["PARAMETER_MODE"].ToString().ToUpper() == "OUT")
                                param.Direction = SPParamDir.OutDirection;
                            else if (rdr["PARAMETER_MODE"].ToString().ToUpper() == "IN")
                                param.Direction = SPParamDir.InDirection;
                            else
                                param.Direction = SPParamDir.InAndOutDirection;
                            result.Add(param);
                        }
                    }
                    return result;
                }
            }


            string GetPK(string table)
            {

                string sql = @"SELECT c.name AS ColumnName
                FROM sys.indexes AS i
                INNER JOIN sys.index_columns AS ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.objects AS o ON i.object_id = o.object_id
                LEFT OUTER JOIN sys.columns AS c ON ic.object_id = c.object_id AND c.column_id = ic.column_id
                WHERE (i.is_primary_key = 1) AND (o.name = @tableName)";

                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = sql;

                    var p = cmd.CreateParameter();
                    p.ParameterName = "@tableName";
                    p.Value = table;
                    cmd.Parameters.Add(p);

                    var result = "";
                    DbDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        if (reader.Read())
                        {
                            result = reader[0].ToString();
                            if (reader.Read())
                            {
                                result = "";
                            }
                        }
                    }
                    finally
                    {
                        // Always call Close when done reading.
                        reader.Close();
                    }
                    return result;
                }
            }

            string GetPropertyType(string sqlType)
            {
                string propertyType, dbType;
                GetPropertyAndDbType(sqlType, out propertyType, out dbType);
                return propertyType;
            }

            string GetNullablePropertyType(string sqlType)
            {
                string value = GetPropertyType(sqlType);
                if (value.ToUpper() != "STRING" && value.ToUpper() != "BYTE[]")
                    return value + "?";
                else
                    return value;
            }

            string GetDbType(string sqlType)
            {
                string propertyType, dbType;
                GetPropertyAndDbType(sqlType, out propertyType, out dbType);
                return dbType;
            }


            void GetPropertyAndDbType(string sqlType, out string propertyType, out string dbType)
            {
                string sysType = "string";
                string sysDbType = "DbType.String";
                switch (sqlType)
                {
                    case "varchar":
                        sysType = "string";
                        sysDbType = "DbType.AnsiString";
                        break;
                    case "bigint":
                        sysType = "long";
                        sysDbType = "DbType.Int64";
                        break;
                    case "smallint":
                        sysType = "short";
                        sysDbType = "DbType.Int16";
                        break;
                    case "int":
                        sysType = "int";
                        sysDbType = "DbType.Int32";
                        break;
                    case "uniqueidentifier":
                        sysType = "Guid";
                        sysDbType = "DbType.Guid";
                        break;
                    case "smalldatetime":
                    case "datetime":
                    case "datetime2":
                    case "date":
                    case "time":
                        sysType = "DateTime";
                        sysDbType = "DbType.DateTime";
                        break;
                    case "datetimeoffset":
                        sysType = "DateTimeOffset";
                        sysDbType = "DbType.DateTimeOffset";
                        break;
                    case "float":
                        sysType = "double";
                        sysDbType = "DbType.Double";
                        break;
                    case "real":
                        sysType = "float";
                        sysDbType = "DbType.Double";
                        break;
                    case "numeric":
                    case "smallmoney":
                    case "decimal":
                    case "money":
                        sysType = "decimal";
                        sysDbType = "DbType.Decimal";
                        break;
                    case "tinyint":
                        sysType = "byte";
                        sysDbType = "DbType.Byte";
                        break;
                    case "bit":
                        sysType = "bool";
                        sysDbType = "DbType.Boolean";
                        break;
                    case "image":
                    case "binary":
                    case "varbinary":
                    case "timestamp":
                        sysType = "byte[]";
                        sysDbType = "DbType.Binary";
                        break;
                    case "geography":
                        sysType = "Microsoft.SqlServer.Types.SqlGeography";
                        sysDbType = "DbType.";
                        break;
                    case "geometry":
                        sysType = "Microsoft.SqlServer.Types.SqlGeometry";
                        sysDbType = "DbType.";
                        break;
                }
                propertyType = sysType;
                dbType = sysDbType;
            }

            string GetDBType(string sqlType)
            {
                string sysType = "string";
                switch (sqlType)
                {
                    case "bigint":
                        sysType = "long";
                        break;
                    case "smallint":
                        sysType = "short";
                        break;
                    case "int":
                        sysType = "int";
                        break;
                    case "uniqueidentifier":
                        sysType = "Guid";
                        break;
                    case "smalldatetime":
                    case "datetime":
                    case "datetime2":
                    case "date":
                    case "time":
                        sysType = "DateTime";
                        break;
                    case "datetimeoffset":
                        sysType = "DateTimeOffset";
                        break;
                    case "float":
                        sysType = "double";
                        break;
                    case "real":
                        sysType = "float";
                        break;
                    case "numeric":
                    case "smallmoney":
                    case "decimal":
                    case "money":
                        sysType = "decimal";
                        break;
                    case "tinyint":
                        sysType = "byte";
                        break;
                    case "bit":
                        sysType = "bool";
                        break;
                    case "image":
                    case "binary":
                    case "varbinary":
                    case "timestamp":
                        sysType = "byte[]";
                        break;
                    case "geography":
                        sysType = "Microsoft.SqlServer.Types.SqlGeography";
                        break;
                    case "geometry":
                        sysType = "Microsoft.SqlServer.Types.SqlGeometry";
                        break;
                }
                return sysType;
            }


            const string TABLE_SQL = @"SELECT * FROM  INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' OR TABLE_TYPE='VIEW'
								UNION
							SELECT SPECIFIC_CATALOG, SPECIFIC_SCHEMA, SPECIFIC_NAME, 'TVF' FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'FUNCTION' AND DATA_TYPE = 'TABLE'";

            const string COLUMN_SQL = @"SELECT T.[Database] ,
									   T.Owner ,
									   T.TableName ,
									   T.ColumnName ,
									   T.OrdinalPosition ,
									   T.DefaultSetting ,
									   T.IsNullable ,
									   T.DataType ,
									   T.MaxLength ,
									   T.DatePrecision ,
									   T.IsIdentity ,
									   T.IsComputed FROM (
								SELECT
											TABLE_CATALOG AS [Database],
											TABLE_SCHEMA AS Owner,
											TABLE_NAME AS TableName,
											COLUMN_NAME AS ColumnName,
											ORDINAL_POSITION AS OrdinalPosition,
											COLUMN_DEFAULT AS DefaultSetting,
											IS_NULLABLE AS IsNullable, DATA_TYPE AS DataType,
											CHARACTER_MAXIMUM_LENGTH AS MaxLength,
											DATETIME_PRECISION AS DatePrecision,
											COLUMNPROPERTY(object_id('[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']'), COLUMN_NAME, 'IsIdentity') AS IsIdentity,
											COLUMNPROPERTY(object_id('[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']'), COLUMN_NAME, 'IsComputed') as IsComputed
										FROM  INFORMATION_SCHEMA.COLUMNS
										WHERE TABLE_NAME=@tableName AND TABLE_SCHEMA=@schemaName
										--ORDER BY OrdinalPosition ASC
								UNION
								SELECT TABLE_CATALOG AS [Database],
											TABLE_SCHEMA AS Owner,
											TABLE_NAME AS TableName,
											COLUMN_NAME AS ColumnName,
											ORDINAL_POSITION AS OrdinalPosition,
											COLUMN_DEFAULT AS DefaultSetting,
											IS_NULLABLE AS IsNullable, DATA_TYPE AS DataType,
											CHARACTER_MAXIMUM_LENGTH AS MaxLength,
											DATETIME_PRECISION AS DatePrecision,
											COLUMNPROPERTY(object_id('[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']'), COLUMN_NAME, 'IsIdentity') AS IsIdentity,
											COLUMNPROPERTY(object_id('[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']'), COLUMN_NAME, 'IsComputed') as IsComputed  
								FROM INFORMATION_SCHEMA.ROUTINE_COLUMNS
								WHERE TABLE_NAME=@tableName AND TABLE_SCHEMA=@schemaName
								) T
								ORDER BY T.OrdinalPosition ASC";

            const string SP_NAMES_SQL = @"SELECT  o.name AS sp_name, s.name AS schema_name
FROM    sys.objects o
        INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE   o.type = 'P'
        AND o.name NOT IN ( 'fn_diagramobjects', 'sp_alterdiagram',
                            'sp_creatediagram', 'sp_dropdiagram',
                            'sp_helpdiagramdefinition', 'sp_helpdiagrams',
                            'sp_renamediagram', 'sp_upgraddiagrams',
                            'sysdiagrams' )";


            const string SP_PARAMETERS_SQL = @"SELECT * from information_schema.PARAMETERS
                                where SPECIFIC_NAME = @spname
                                order by ORDINAL_POSITION";

        }

        class SqlServerCeSchemaReader : SchemaReader
        {
            // SchemaReader.ReadSchema
            public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
            {
                var result = new Tables();

                _connection = connection;
                _factory = factory;

                var cmd = _factory.CreateCommand();
                cmd.Connection = connection;
                cmd.CommandText = TABLE_SQL;

                //pull the tables in a reader
                using (cmd)
                {
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Table tbl = new Table();
                            tbl.Name = rdr["TABLE_NAME"].ToString();
                            tbl.CleanName = CleanUp(tbl.Name);
                            tbl.ClassName = Inflector.MakeSingular(tbl.CleanName);
                            tbl.Schema = null;
                            tbl.IsView = false;
                            result.Add(tbl);
                        }
                    }
                }

                foreach (var tbl in result)
                {
                    tbl.Columns = LoadColumns(tbl);

                    // Mark the primary key
                    string PrimaryKey = GetPK(tbl.Name);
                    var pkColumn = tbl.Columns.SingleOrDefault(x => x.Name.ToLower().Trim() == PrimaryKey.ToLower().Trim());
                    if (pkColumn != null)
                        pkColumn.IsPK = true;
                }


                return result;
            }

            public override List<SP> ReadSPList(DbConnection connection, DbProviderFactory factory)
            {
                return new List<SP>();
            }
            public override ConstraintTables ConstraintList(DbConnection connection, DbProviderFactory factory)
            {
                return new ConstraintTables();
            }

            DbConnection _connection;
            DbProviderFactory _factory;


            List<Column> LoadColumns(Table tbl)
            {

                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = COLUMN_SQL;

                    var p = cmd.CreateParameter();
                    p.ParameterName = "@tableName";
                    p.Value = tbl.Name;
                    cmd.Parameters.Add(p);

                    var result = new List<Column>();
                    using (IDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Column col = new Column();
                            col.Name = rdr["ColumnName"].ToString();
                            col.PropertyName = CleanUp(col.Name);
                            col.PropertyType = GetPropertyType(rdr["DataType"].ToString());
                            col.Size = GetDatatypeSize(rdr["DataType"].ToString());
                            col.Precision = GetDatatypePrecision(rdr["DataType"].ToString());
                            col.IsNullable = rdr["IsNullable"].ToString() == "YES";
                            col.IsAutoIncrement = rdr["AUTOINC_INCREMENT"] != DBNull.Value;
                            result.Add(col);
                        }
                    }

                    return result;
                }
            }

            string GetPK(string table)
            {

                string sql = @"SELECT KCU.COLUMN_NAME
			FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU
			JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
			ON KCU.CONSTRAINT_NAME=TC.CONSTRAINT_NAME
			WHERE TC.CONSTRAINT_TYPE='PRIMARY KEY'
			AND KCU.TABLE_NAME=@tableName";

                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = sql;

                    var p = cmd.CreateParameter();
                    p.ParameterName = "@tableName";
                    p.Value = table;
                    cmd.Parameters.Add(p);

                    var result = "";
                    DbDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        if (reader.Read())
                        {
                            result = reader[0].ToString();
                            if (reader.Read())
                            {
                                result = "";
                            }
                        }
                    }
                    finally
                    {
                        // Always call Close when done reading.
                        reader.Close();
                    }
                    return result;
                }
            }

            string GetPropertyType(string sqlType)
            {
                string sysType = "string";
                switch (sqlType)
                {
                    case "bigint":
                        sysType = "long";
                        break;
                    case "smallint":
                        sysType = "short";
                        break;
                    case "int":
                        sysType = "int";
                        break;
                    case "uniqueidentifier":
                        sysType = "Guid";
                        break;
                    case "smalldatetime":
                    case "datetime":
                    case "date":
                    case "time":
                        sysType = "DateTime";
                        break;
                    case "float":
                        sysType = "double";
                        break;
                    case "real":
                        sysType = "float";
                        break;
                    case "numeric":
                    case "smallmoney":
                    case "decimal":
                    case "money":
                        sysType = "decimal";
                        break;
                    case "tinyint":
                        sysType = "byte";
                        break;
                    case "bit":
                        sysType = "bool";
                        break;
                    case "image":
                    case "binary":
                    case "varbinary":
                    case "timestamp":
                        sysType = "byte[]";
                        break;
                }
                return sysType;
            }



            const string TABLE_SQL = @"SELECT *
		FROM  INFORMATION_SCHEMA.TABLES
		WHERE TABLE_TYPE='TABLE'";

            const string COLUMN_SQL = @"SELECT
			TABLE_CATALOG AS [Database],
			TABLE_SCHEMA AS Owner,
			TABLE_NAME AS TableName,
			COLUMN_NAME AS ColumnName,
			ORDINAL_POSITION AS OrdinalPosition,
			COLUMN_DEFAULT AS DefaultSetting,
			IS_NULLABLE AS IsNullable, DATA_TYPE AS DataType,
			AUTOINC_INCREMENT,
			CHARACTER_MAXIMUM_LENGTH AS MaxLength,
			DATETIME_PRECISION AS DatePrecision
		FROM  INFORMATION_SCHEMA.COLUMNS
		WHERE TABLE_NAME=@tableName
		ORDER BY OrdinalPosition ASC";

        }


        class PostGreSqlSchemaReader : SchemaReader
        {
            // SchemaReader.ReadSchema
            public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
            {
                var result = new Tables();

                _connection = connection;
                _factory = factory;

                var cmd = _factory.CreateCommand();
                cmd.Connection = connection;
                cmd.CommandText = TABLE_SQL;

                //pull the tables in a reader
                using (cmd)
                {
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Table tbl = new Table();
                            tbl.Name = rdr["table_name"].ToString();
                            tbl.Schema = rdr["table_schema"].ToString();
                            tbl.IsView = string.Compare(rdr["table_type"].ToString(), "View", true) == 0;
                            tbl.CleanName = CleanUp(tbl.Name);
                            tbl.ClassName = Inflector.MakeSingular(tbl.CleanName);
                            result.Add(tbl);
                        }
                    }
                }

                foreach (var tbl in result)
                {
                    tbl.Columns = LoadColumns(tbl);

                    // Mark the primary key
                    string PrimaryKey = GetPK(tbl.Name);
                    var pkColumn = tbl.Columns.SingleOrDefault(x => x.Name.ToLower().Trim() == PrimaryKey.ToLower().Trim());
                    if (pkColumn != null)
                        pkColumn.IsPK = true;
                }


                return result;
            }

            public override List<SP> ReadSPList(DbConnection connection, DbProviderFactory factory)
            {
                return new List<SP>();
            }

            public override ConstraintTables ConstraintList(DbConnection connection, DbProviderFactory factory)
            {
                return new ConstraintTables();
            }

            DbConnection _connection;
            DbProviderFactory _factory;


            List<Column> LoadColumns(Table tbl)
            {

                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = COLUMN_SQL;

                    var p = cmd.CreateParameter();
                    p.ParameterName = "@tableName";
                    p.Value = tbl.Name;
                    cmd.Parameters.Add(p);

                    var result = new List<Column>();
                    using (IDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Column col = new Column();
                            col.Name = rdr["column_name"].ToString();
                            col.PropertyName = CleanUp(col.Name);
                            col.PropertyType = GetPropertyType(rdr["udt_name"].ToString());
                            col.Size = GetDatatypeSize(rdr["udt_name"].ToString());
                            col.Precision = GetDatatypePrecision(rdr["udt_name"].ToString());
                            col.IsNullable = rdr["is_nullable"].ToString() == "YES";
                            col.IsAutoIncrement = rdr["column_default"].ToString().StartsWith("nextval(");
                            result.Add(col);
                        }
                    }

                    return result;
                }
            }

            string GetPK(string table)
            {

                string sql = @"SELECT kcu.column_name
			FROM information_schema.key_column_usage kcu
			JOIN information_schema.table_constraints tc
			ON kcu.constraint_name=tc.constraint_name
			WHERE lower(tc.constraint_type)='primary key'
			AND kcu.table_name=@tablename";

                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = sql;

                    var p = cmd.CreateParameter();
                    p.ParameterName = "@tableName";
                    p.Value = table;
                    cmd.Parameters.Add(p);

                    var result = "";
                    DbDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        if (reader.Read())
                        {
                            result = reader[0].ToString();
                            if (reader.Read())
                            {
                                result = "";
                            }
                        }
                    }
                    finally
                    {
                        // Always call Close when done reading.
                        reader.Close();
                    }
                    return result;
                }
            }

            string GetPropertyType(string sqlType)
            {
                switch (sqlType)
                {
                    case "int8":
                    case "serial8":
                        return "long";

                    case "bool":
                        return "bool";

                    case "bytea	":
                        return "byte[]";

                    case "float8":
                        return "double";

                    case "int4":
                    case "serial4":
                        return "int";

                    case "money	":
                        return "decimal";

                    case "numeric":
                        return "decimal";

                    case "float4":
                        return "float";

                    case "int2":
                        return "short";

                    case "time":
                    case "timetz":
                    case "timestamp":
                    case "timestamptz":
                    case "date":
                        return "DateTime";

                    default:
                        return "string";
                }
            }



            const string TABLE_SQL = @"
			SELECT table_name, table_schema, table_type
			FROM information_schema.tables
			WHERE (table_type='BASE TABLE' OR table_type='VIEW')
				AND table_schema NOT IN ('pg_catalog', 'information_schema');
			";

            const string COLUMN_SQL = @"
			SELECT column_name, is_nullable, udt_name, column_default
			FROM information_schema.columns
			WHERE table_name=@tableName;
			";

        }

        class MySqlSchemaReader : SchemaReader
        {
            // SchemaReader.ReadSchema
            public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
            {
                var result = new Tables();

                try
                {
                    var cmd = factory.CreateCommand();
                    cmd.Connection = connection;
                    cmd.CommandText = TABLE_SQL;

                    //pull the tables in a reader
                    using (cmd)
                    {
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                Table tbl = new Table();
                                tbl.Name = rdr["TABLE_NAME"].ToString();
                                tbl.Schema = rdr["TABLE_SCHEMA"].ToString();
                                tbl.IsView = string.Compare(rdr["TABLE_TYPE"].ToString(), "View", true) == 0;
                                tbl.CleanName = CleanUp(tbl.Name);
                                tbl.ClassName = Inflector.MakeSingular(tbl.CleanName);
                                result.Add(tbl);
                            }
                        }
                    }


                    //this will return everything for the DB
                    var schema = connection.GetSchema("COLUMNS");

                    //loop again - but this time pull by table name
                    foreach (var item in result)
                    {
                        item.Columns = new List<Column>();

                        //pull the columns from the schema
                        var columns = schema.Select("TABLE_NAME='" + item.Name + "'");
                        foreach (var row in columns)
                        {
                            Column col = new Column();
                            col.Name = row["COLUMN_NAME"].ToString();
                            col.PropertyName = CleanUp(col.Name);
                            col.PropertyType = GetPropertyType(row);
                            col.Size = GetDatatypeSize(row["DATA_TYPE"].ToString());
                            col.Precision = GetDatatypePrecision(row["DATA_TYPE"].ToString());
                            col.IsNullable = row["IS_NULLABLE"].ToString() == "YES";
                            col.IsPK = row["COLUMN_KEY"].ToString() == "PRI";
                            col.IsAutoIncrement = row["extra"].ToString().ToLower().IndexOf("auto_increment") >= 0;

                            item.Columns.Add(col);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLine(ex.ToString());
                    return result;
                }

                return result;

            }

            public override List<SP> ReadSPList(DbConnection connection, DbProviderFactory factory)
            {
                return new List<SP>();
            }

            public override ConstraintTables ConstraintList(DbConnection connection, DbProviderFactory factory)
            {
                return new ConstraintTables();
            }

            static string GetPropertyType(DataRow row)
            {
                bool bUnsigned = row["COLUMN_TYPE"].ToString().IndexOf("unsigned") >= 0;
                string propType = "string";
                switch (row["DATA_TYPE"].ToString())
                {
                    case "bigint":
                        propType = bUnsigned ? "ulong" : "long";
                        break;
                    case "int":
                        propType = bUnsigned ? "uint" : "int";
                        break;
                    case "smallint":
                        propType = bUnsigned ? "ushort" : "short";
                        break;
                    case "guid":
                        propType = "Guid";
                        break;
                    case "smalldatetime":
                    case "date":
                    case "datetime":
                    case "timestamp":
                        propType = "DateTime";
                        break;
                    case "float":
                        propType = "float";
                        break;
                    case "double":
                        propType = "double";
                        break;
                    case "numeric":
                    case "smallmoney":
                    case "decimal":
                    case "money":
                        propType = "decimal";
                        break;
                    case "bit":
                    case "bool":
                    case "boolean":
                        propType = "bool";
                        break;
                    case "tinyint":
                        propType = bUnsigned ? "byte" : "sbyte";
                        break;
                    case "image":
                    case "binary":
                    case "blob":
                    case "mediumblob":
                    case "longblob":
                    case "varbinary":
                        propType = "byte[]";
                        break;

                }
                return propType;
            }

            const string TABLE_SQL = @"
			SELECT *
			FROM information_schema.tables
			WHERE (table_type='BASE TABLE' OR table_type='VIEW')
			";

        }

        class OracleSchemaReader : SchemaReader
        {
            // SchemaReader.ReadSchema
            public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
            {
                var result = new Tables();
                try
                {
                    _connection = connection;
                    _factory = factory;

                    if (!string.IsNullOrWhiteSpace(TableNames))
                    {
                        string tbNames = "'" + string.Join("','", TableNames.ToUpper().Split(',')) + "'";
                        TABLE_SQL += string.Format(" and tt.table_name in ({0})", tbNames);
                    }
                    else if (string.IsNullOrWhiteSpace(TableNames) && !string.IsNullOrWhiteSpace(TableNameLike))
                    {
                        TABLE_SQL += string.Format(" and tt.table_name like '%{0}%' ", TableNameLike.ToUpper());
                    }

                    var cmd = _factory.CreateCommand();
                    cmd.Connection = connection;
                    cmd.CommandText = TABLE_SQL;
                    //todo:这里修改,注释掉了
                    //cmd.GetType().GetProperty("BindByName").SetValue(cmd, true, null);

                    //pull the tables in a reader
                    using (cmd)
                    {
                        //Todo:这里修改
                        //执行查询所有表 TABLE_SQL语句
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                Table tbl = new Table();
                                tbl.Name = rdr["TABLE_NAME"].ToString();
                                tbl.Schema = rdr["TABLE_SCHEMA"].ToString();
                                tbl.Comments = rdr["comments"].ToString();
                                tbl.IsView = string.Compare(rdr["TABLE_TYPE"].ToString(), "View", true) == 0;
                                tbl.CleanName = CleanUp(tbl.Name);
                                tbl.ClassName = Inflector.MakeSingular(tbl.CleanName);
                                result.Add(tbl);
                            }
                        }
                    }

                    foreach (var tbl in result)
                    {
                        tbl.Columns = LoadColumns(tbl);

                        // Mark the primary key
                        string PrimaryKey = GetPK(tbl.Name);
                        var pkColumn = tbl.Columns.SingleOrDefault(x => x.Name.ToLower().Trim() == PrimaryKey.ToLower().Trim());
                        if (pkColumn != null)
                            pkColumn.IsPK = true;
                    }
                }
                catch (Exception ex)
                {
                    WriteLine(ex.ToString());
                    return result;
                }

                return result;
            }

            public override List<SP> ReadSPList(DbConnection connection, DbProviderFactory factory)
            {
                return new List<SP>();
            }

            //查询外键关联表
            public override ConstraintTables ConstraintList(DbConnection connection, DbProviderFactory factory)
            {
                var result = new ConstraintTables();
                try
                {
                    _connection = connection;
                    _factory = factory;

                    var cmd = _factory.CreateCommand();
                    cmd.Connection = connection;
                    cmd.CommandText = constraint_SQL;

                    using (cmd)
                    {
                        //执行查询表外键的关联表 constraint_SQL语句
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                ConstraintTable tbl = new ConstraintTable();
                                tbl.ConstraintName = rdr["ConstraintName"].ToString();
                                tbl.OrTableName = rdr["OrTableName"].ToString();
                                tbl.OrColumnName = rdr["OrColumnName"].ToString();
                                tbl.TableName = rdr["TableName"].ToString();
                                tbl.ColumnName = rdr["ColumnName"].ToString();
                                tbl.TableComments = rdr["TableComments"].ToString();
                                result.Add(tbl);
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    WriteLine(ex.ToString());
                    return result;
                }

                return result;
            }


            DbConnection _connection;
            DbProviderFactory _factory;

            //Todo:这里修改
            //查询表中字段
            List<Column> LoadColumns(Table tbl)
            {
                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = COLUMN_SQL;
                    //todo:这里修改,注释掉了
                    //cmd.GetType().GetProperty("BindByName").SetValue(cmd, true, null);

                    var p = cmd.CreateParameter();
                    p.ParameterName = ":tableName";
                    p.Value = tbl.Name;
                    cmd.Parameters.Add(p);

                    var result = new List<Column>();
                    using (IDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Column col = new Column();
                            col.Name = rdr["ColumnName"].ToString();
                            col.PropertyName = CleanUp(col.Name);
                            col.PropertyType = GetPropertyType(rdr["DataType"].ToString(), (rdr["DataType"] == DBNull.Value ? null : rdr["DataType"].ToString()));
                            col.Size = GetDatatypeSize(rdr["DataType"].ToString());
                            col.Precision = GetDatatypePrecision(rdr["DataType"].ToString());
                            col.IsNullable = rdr["IsNullable"].ToString() == "YES";
                            col.Comments = rdr["Comments"].ToString();//Todo:这里修改
                            col.IsAutoIncrement = false;
                            result.Add(col);
                        }
                    }

                    return result;
                }
            }

            string GetPK(string table)
            {

                string sql = @"select column_name from USER_CONSTRAINTS uc
  inner join USER_CONS_COLUMNS ucc on uc.constraint_name = ucc.constraint_name
where uc.constraint_type = 'P'
and uc.table_name = upper(:tableName)
and ucc.position = 1";

                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = sql;
                    //todo:这里修改,注释掉了
                    //cmd.GetType().GetProperty("BindByName").SetValue(cmd, true, null);

                    var p = cmd.CreateParameter();
                    p.ParameterName = ":tableName";
                    p.Value = table;
                    cmd.Parameters.Add(p);

                    var result = "";
                    DbDataReader reader = cmd.ExecuteReader();
                    try
                    {
                        if (reader.Read())
                        {
                            result = reader[0].ToString();
                            if (reader.Read())
                            {
                                result = "";
                            }
                        }
                    }
                    finally
                    {
                        // Always call Close when done reading.
                        reader.Close();
                    }
                    return result;
                }
            }

            string GetPropertyType(string sqlType, string dataScale)
            {
                string sysType = "string";
                switch (sqlType.ToLower())
                {
                    case "bigint":
                        sysType = "long";
                        break;
                    case "smallint":
                        sysType = "short";
                        break;
                    case "int":
                        sysType = "int";
                        break;
                    case "uniqueidentifier":
                        sysType = "Guid";
                        break;
                    case "smalldatetime":
                    case "datetime":
                    case "date":
                        sysType = "DateTime";
                        break;
                    case "float":
                        sysType = "double";
                        break;
                    case "real":
                    case "numeric":
                    case "smallmoney":
                    case "decimal":
                    case "money":
                    case "number":
                        sysType = "decimal";
                        break;
                    case "tinyint":
                        sysType = "byte";
                        break;
                    case "bit":
                        sysType = "bool";
                        break;
                    case "image":
                    case "binary":
                    case "varbinary":
                    case "timestamp":
                        sysType = "byte[]";
                        break;
                }

                if (sqlType == "number" && dataScale == "0")
                    return "long";

                return sysType;
            }



            //Todo:这里修改
            //	const string TABLE_SQL=@"select TABLE_NAME, 'Table' TABLE_TYPE, USER TABLE_SCHEMA
            //from USER_TABLES 
            //union all
            //select VIEW_NAME, 'View', USER
            //from USER_VIEWS";

            string TABLE_SQL = @"
select tt.table_name,tt.table_type,tt.table_schema,utc.comments from (
select TABLE_NAME, 'Table' TABLE_TYPE, USER TABLE_SCHEMA
from USER_TABLES 
union all
select VIEW_NAME, 'View', USER
from USER_VIEWS
) tt
-- user_tab_comments 用户拥有的表和视图的注释
left join user_tab_comments utc
on utc.table_name =tt. TABLE_NAME  
where 1=1 
";


            string constraint_SQL = @"
select b.constraint_name ConstraintName, b.table_name OrTableName ,  b.column_name OrColumnName,
c.table_name TableName,c.column_name ColumnName,d.comments TableComments
from user_constraints a
left join user_cons_columns b on a.constraint_name = b.constraint_name
left join user_cons_columns c on a.r_constraint_name = c.constraint_name
left join user_tab_comments d on d.TABLE_NAME = c.table_name
where a.constraint_type = 'R' and a.table_name = '" + constraintTableName + "'";

            //Todo:这里修改
            //原始语句
            //const string COLUMN_SQL=@"select table_name TableName,
            //column_name ColumnName,
            //data_type DataType,
            //data_scale DataScale,
            //nullable IsNullable
            //from USER_TAB_COLS utc
            //where table_name = :tableName
            //order by column_id";

            //自己修改，COMMENTS 查看字段说明
            const string COLUMN_SQL = @"select utc.table_name  TableName,
        utc.column_name ColumnName,
        utc.data_type   DataType,
        utc.data_scale  DataScale,
        utc.nullable    IsNullable,
        t2.COMMENTS
   from USER_TAB_COLS utc, user_col_comments t2
  where utc.table_name = :tableName
    AND utc.TABLE_NAME = t2.TABLE_NAME
    AND utc.COLUMN_NAME = t2.COLUMN_NAME
  order by utc.column_id";

        }


        class SqliteSchemaReader : SchemaReader
        {
            // SchemaReader.ReadSchema
            public override Tables ReadSchema(DbConnection connection, DbProviderFactory factory)
            {
                var result = new Tables();
                _connection = connection;
                _factory = factory;
                var cmd = _factory.CreateCommand();
                cmd.Connection = connection;
                cmd.CommandText = TABLE_SQL;
                //cmd.GetType().GetProperty("BindByName").SetValue(cmd, true, null);
                //pull the tables in a reader
                using (cmd)
                {
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Table tbl = new Table();
                            tbl.Name = rdr["name"].ToString();
                            tbl.Schema = "";
                            tbl.IsView = string.Compare(rdr["type"].ToString(), "view", true) == 0;
                            tbl.CleanName = CleanUp(tbl.Name);
                            tbl.ClassName = Inflector.MakeSingular(tbl.CleanName);
                            tbl.SQL = rdr["sql"].ToString();
                            result.Add(tbl);
                        }
                    }
                }
                foreach (var tbl in result)
                {
                    tbl.Columns = LoadColumns(tbl);
                    tbl.Indices = LoadIndices(tbl.Name);
                    tbl.FKeys = LoadFKeys(tbl.Name);
                }
                return result;
            }

            public override List<SP> ReadSPList(DbConnection connection, DbProviderFactory factory)
            {
                return new List<SP>();
            }

            public override ConstraintTables ConstraintList(DbConnection connection, DbProviderFactory factory)
            {
                return null;
            }

            DbConnection _connection;
            DbProviderFactory _factory;

            List<Column> LoadColumns(Table tbl)
            {
                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = string.Format(COLUMN_SQL, tbl.Name);

                    var result = new List<Column>();
                    using (IDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            Column col = new Column();
                            col.Name = rdr["name"].ToString();
                            col.PropertyName = CleanUp(col.Name);
                            col.PropertyType = GetPropertyType(rdr["type"].ToString(), (rdr["type"] == DBNull.Value ? null : rdr["type"].ToString()));
                            col.Size = GetDatatypeSize(rdr["type"].ToString());
                            col.Precision = GetDatatypePrecision(rdr["type"].ToString());
                            col.IsNullable = rdr["notnull"].ToString() == "0";
                            col.IsAutoIncrement = false;
                            col.IsPK = rdr["pk"].ToString() != "0";
                            if (col.IsPK)
                                col.IsAutoIncrement = tbl.SQL.ToUpper().Contains("AUTOINCREMENT");
                            else
                                col.IsAutoIncrement = false;
                            col.DefaultValue = rdr["dflt_value"] == DBNull.Value ? null : rdr["dflt_value"].ToString();
                            result.Add(col);
                        }
                    }
                    return result;
                }
            }

            List<TableIndex> LoadIndices(string tableName)
            {
                var result = new List<TableIndex>();
                using (var cmd1 = _factory.CreateCommand())
                {
                    cmd1.Connection = _connection;
                    cmd1.CommandText = string.Format(INDEX_SQL, tableName);
                    using (IDataReader rdr1 = cmd1.ExecuteReader())
                    {
                        while (rdr1.Read())
                        {
                            TableIndex indx = new TableIndex();
                            indx.Name = rdr1["name"].ToString();
                            indx.SQL = rdr1["sql"].ToString();
                            indx.IndexColumns = new List<IndexColumn>();
                            indx.IsUnique = indx.SQL.ToUpper().Contains("UNIQUE");
                            using (var cmd2 = _factory.CreateCommand())
                            {
                                cmd2.Connection = _connection;
                                cmd2.CommandText = string.Format(INDEX_INFO_SQL, indx.Name);
                                using (IDataReader rdr2 = cmd2.ExecuteReader())
                                {
                                    while (rdr2.Read())
                                    {
                                        IndexColumn col = new IndexColumn();
                                        col.Name = rdr2["name"].ToString();
                                        indx.IndexColumns.Add(col);
                                    }
                                }
                            }
                            result.Add(indx);
                        }
                    }
                }
                return result;
            }

            List<FKey> LoadFKeys(string tblName)
            {
                using (var cmd = _factory.CreateCommand())
                {
                    cmd.Connection = _connection;
                    cmd.CommandText = string.Format(FKEY_INFO_SQL, tblName);

                    var result = new List<FKey>();
                    using (IDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            FKey key = new FKey();
                            key.ToTable = rdr["table"].ToString();
                            key.ToColumn = rdr["to"].ToString();
                            key.FromColumn = rdr["from"].ToString();
                            result.Add(key);
                        }
                    }
                    return result;
                }
            }


            string GetPropertyType(string sqlType, string dataScale)
            {
                string sysType = "string";
                switch (sqlType.ToLower())
                {
                    case "integer":
                    case "int":
                    case "tinyint":
                    case "smallint":
                    case "mediumint":
                    case "int2":
                    case "int8":
                        sysType = "long";
                        break;
                    case "bigint":
                    case "unsigned big int":
                        sysType = "long";
                        break;
                    case "uniqueidentifier":
                        sysType = "Guid";
                        break;
                    case "smalldatetime":
                    case "datetime":
                    case "date":
                        sysType = "DateTime";
                        break;
                    case "float":
                    case "double precision":
                    case "double":
                        sysType = "double";
                        break;
                    case "real":
                    case "numeric":
                    case "smallmoney":
                    case "decimal":
                    case "money":
                    case "number":
                        sysType = "decimal";
                        break;
                    case "bit":
                        sysType = "bool";
                        break;
                    case "image":
                    case "binary":
                    case "varbinary":
                    case "timestamp":
                        sysType = "byte[]";
                        break;
                }

                if (sqlType == "number" && dataScale == "0")
                    return "long";

                return sysType;
            }

            const string TABLE_SQL = @"SELECT name, type , sql FROM sqlite_master WHERE type IN ('table','view') and name not in ('sqlite_sequence') ";
            const string COLUMN_SQL = @"pragma table_info({0})";

            const string INDEX_SQL = @"SELECT name , sql  FROM sqlite_master WHERE type IN ('index') and lower(tbl_name) = lower('{0}')";
            const string INDEX_INFO_SQL = @"pragma index_info({0})";

            const string FKEY_INFO_SQL = @"pragma foreign_key_list({0})";

        }

        /// <summary>
        /// Summary for the Inflector class
        /// </summary>
        public static class Inflector
        {
            private static readonly List<InflectorRule> _plurals = new List<InflectorRule>();
            private static readonly List<InflectorRule> _singulars = new List<InflectorRule>();
            private static readonly List<string> _uncountables = new List<string>();

            /// <summary>
            /// Initializes the <see cref="Inflector"/> class.
            /// </summary>
            static Inflector()
            {
                AddPluralRule("$", "s");
                AddPluralRule("s$", "s");
                AddPluralRule("(ax|test)is$", "$1es");
                AddPluralRule("(octop|vir)us$", "$1i");
                AddPluralRule("(alias|status)$", "$1es");
                AddPluralRule("(bu)s$", "$1ses");
                AddPluralRule("(buffal|tomat)o$", "$1oes");
                AddPluralRule("([ti])um$", "$1a");
                AddPluralRule("sis$", "ses");
                AddPluralRule("(?:([^f])fe|([lr])f)$", "$1$2ves");
                AddPluralRule("(hive)$", "$1s");
                AddPluralRule("([^aeiouy]|qu)y$", "$1ies");
                AddPluralRule("(x|ch|ss|sh)$", "$1es");
                AddPluralRule("(matr|vert|ind)ix|ex$", "$1ices");
                AddPluralRule("([m|l])ouse$", "$1ice");
                AddPluralRule("^(ox)$", "$1en");
                AddPluralRule("(quiz)$", "$1zes");

                AddSingularRule("s$", String.Empty);
                AddSingularRule("ss$", "ss");
                AddSingularRule("(n)ews$", "$1ews");
                AddSingularRule("([ti])a$", "$1um");
                AddSingularRule("((a)naly|(b)a|(d)iagno|(p)arenthe|(p)rogno|(s)ynop|(t)he)ses$", "$1$2sis");
                AddSingularRule("(^analy)ses$", "$1sis");
                AddSingularRule("([^f])ves$", "$1fe");
                AddSingularRule("(hive)s$", "$1");
                AddSingularRule("(tive)s$", "$1");
                AddSingularRule("([lr])ves$", "$1f");
                AddSingularRule("([^aeiouy]|qu)ies$", "$1y");
                AddSingularRule("(s)eries$", "$1eries");
                AddSingularRule("(m)ovies$", "$1ovie");
                AddSingularRule("(x|ch|ss|sh)es$", "$1");
                AddSingularRule("([m|l])ice$", "$1ouse");
                AddSingularRule("(bus)es$", "$1");
                AddSingularRule("(o)es$", "$1");
                AddSingularRule("(shoe)s$", "$1");
                AddSingularRule("(cris|ax|test)es$", "$1is");
                AddSingularRule("(octop|vir)i$", "$1us");
                AddSingularRule("(alias|status)$", "$1");
                AddSingularRule("(alias|status)es$", "$1");
                AddSingularRule("^(ox)en", "$1");
                AddSingularRule("(vert|ind)ices$", "$1ex");
                AddSingularRule("(matr)ices$", "$1ix");
                AddSingularRule("(quiz)zes$", "$1");

                AddIrregularRule("person", "people");
                AddIrregularRule("man", "men");
                AddIrregularRule("child", "children");
                AddIrregularRule("sex", "sexes");
                AddIrregularRule("tax", "taxes");
                AddIrregularRule("move", "moves");

                AddUnknownCountRule("equipment");
                AddUnknownCountRule("information");
                AddUnknownCountRule("rice");
                AddUnknownCountRule("money");
                AddUnknownCountRule("species");
                AddUnknownCountRule("series");
                AddUnknownCountRule("fish");
                AddUnknownCountRule("sheep");
            }

            /// <summary>
            /// Adds the irregular rule.
            /// </summary>
            /// <param name="singular">The singular.</param>
            /// <param name="plural">The plural.</param>
            private static void AddIrregularRule(string singular, string plural)
            {
                AddPluralRule(String.Concat("(", singular[0], ")", singular.Substring(1), "$"), String.Concat("$1", plural.Substring(1)));
                AddSingularRule(String.Concat("(", plural[0], ")", plural.Substring(1), "$"), String.Concat("$1", singular.Substring(1)));
            }

            /// <summary>
            /// Adds the unknown count rule.
            /// </summary>
            /// <param name="word">The word.</param>
            private static void AddUnknownCountRule(string word)
            {
                _uncountables.Add(word.ToLower());
            }

            /// <summary>
            /// Adds the plural rule.
            /// </summary>
            /// <param name="rule">The rule.</param>
            /// <param name="replacement">The replacement.</param>
            private static void AddPluralRule(string rule, string replacement)
            {
                _plurals.Add(new InflectorRule(rule, replacement));
            }

            /// <summary>
            /// Adds the singular rule.
            /// </summary>
            /// <param name="rule">The rule.</param>
            /// <param name="replacement">The replacement.</param>
            private static void AddSingularRule(string rule, string replacement)
            {
                _singulars.Add(new InflectorRule(rule, replacement));
            }

            /// <summary>
            /// Makes the plural.
            /// </summary>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            public static string MakePlural(string word)
            {
                return ApplyRules(_plurals, word);
            }

            /// <summary>
            /// Makes the singular.
            /// </summary>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            public static string MakeSingular(string word)
            {
                return ApplyRules(_singulars, word);
            }

            /// <summary>
            /// Applies the rules.
            /// </summary>
            /// <param name="rules">The rules.</param>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            private static string ApplyRules(IList<InflectorRule> rules, string word)
            {
                string result = word;
                if (!_uncountables.Contains(word.ToLower()))
                {
                    for (int i = rules.Count - 1; i >= 0; i--)
                    {
                        string currentPass = rules[i].Apply(word);
                        if (currentPass != null)
                        {
                            result = currentPass;
                            break;
                        }
                    }
                }
                return result;
            }

            /// <summary>
            /// Converts the string to title case.
            /// </summary>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            public static string ToTitleCase(string word)
            {
                return Regex.Replace(ToHumanCase(AddUnderscores(word)), @"\b([a-z])",
                    delegate (Match match) { return match.Captures[0].Value.ToUpper(); });
            }

            /// <summary>
            /// Converts the string to human case.
            /// </summary>
            /// <param name="lowercaseAndUnderscoredWord">The lowercase and underscored word.</param>
            /// <returns></returns>
            public static string ToHumanCase(string lowercaseAndUnderscoredWord)
            {
                return MakeInitialCaps(Regex.Replace(lowercaseAndUnderscoredWord, @"_", " "));
            }

            /// <summary>
            /// Adds the underscores.
            /// </summary>
            /// <param name="pascalCasedWord">The pascal cased word.</param>
            /// <returns></returns>
            public static string AddUnderscores(string pascalCasedWord)
            {
                return Regex.Replace(Regex.Replace(Regex.Replace(pascalCasedWord, @"([A-Z]+)([A-Z][a-z])", "$1_$2"), @"([a-z\d])([A-Z])", "$1_$2"), @"[-\s]", "_").ToLower();
            }

            /// <summary>
            /// Makes the initial caps.
            /// </summary>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            public static string MakeInitialCaps(string word)
            {
                return String.Concat(word.Substring(0, 1).ToUpper(), word.Substring(1).ToLower());
            }

            /// <summary>
            /// Makes the initial lower case.
            /// </summary>
            /// <param name="word">The word.</param>
            /// <returns></returns>
            public static string MakeInitialLowerCase(string word)
            {
                return String.Concat(word.Substring(0, 1).ToLower(), word.Substring(1));
            }


            /// <summary>
            /// Determine whether the passed string is numeric, by attempting to parse it to a double
            /// </summary>
            /// <param name="str">The string to evaluated for numeric conversion</param>
            /// <returns>
            /// 	<c>true</c> if the string can be converted to a number; otherwise, <c>false</c>.
            /// </returns>
            public static bool IsStringNumeric(string str)
            {
                double result;
                return (double.TryParse(str, NumberStyles.Float, NumberFormatInfo.CurrentInfo, out result));
            }

            /// <summary>
            /// Adds the ordinal suffix.
            /// </summary>
            /// <param name="number">The number.</param>
            /// <returns></returns>
            public static string AddOrdinalSuffix(string number)
            {
                if (IsStringNumeric(number))
                {
                    int n = int.Parse(number);
                    int nMod100 = n % 100;

                    if (nMod100 >= 11 && nMod100 <= 13)
                        return String.Concat(number, "th");

                    switch (n % 10)
                    {
                        case 1:
                            return String.Concat(number, "st");
                        case 2:
                            return String.Concat(number, "nd");
                        case 3:
                            return String.Concat(number, "rd");
                        default:
                            return String.Concat(number, "th");
                    }
                }
                return number;
            }

            /// <summary>
            /// Converts the underscores to dashes.
            /// </summary>
            /// <param name="underscoredWord">The underscored word.</param>
            /// <returns></returns>
            public static string ConvertUnderscoresToDashes(string underscoredWord)
            {
                return underscoredWord.Replace('_', '-');
            }


            #region Nested type: InflectorRule

            /// <summary>
            /// Summary for the InflectorRule class
            /// </summary>
            private class InflectorRule
            {
                /// <summary>
                ///
                /// </summary>
                public readonly Regex regex;

                /// <summary>
                ///
                /// </summary>
                public readonly string replacement;

                /// <summary>
                /// Initializes a new instance of the <see cref="InflectorRule"/> class.
                /// </summary>
                /// <param name="regexPattern">The regex pattern.</param>
                /// <param name="replacementText">The replacement text.</param>
                public InflectorRule(string regexPattern, string replacementText)
                {
                    regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                    replacement = replacementText;
                }

                /// <summary>
                /// Applies the specified word.
                /// </summary>
                /// <param name="word">The word.</param>
                /// <returns></returns>
                public string Apply(string word)
                {
                    if (!regex.IsMatch(word))
                        return null;

                    string replace = regex.Replace(word, replacement);
                    if (word == word.ToUpper())
                        replace = replace.ToUpper();

                    return replace;
                }
            }

            #endregion
        }

        // https://raw.github.com/damieng/DamienGKit
        // http://damieng.com/blog/2009/11/06/multiple-outputs-from-t4-made-easy-revisited

        // Manager class records the various blocks so it can split them up
        public class Manager
        {
            private class Block
            {
                public String Name;
                public int Start, Length;
                public bool IncludeInDefault;
            }

            private Block currentBlock;
            private List<Block> files = new List<Block>();
            private Block footer = new Block();
            private Block header = new Block();
            private ITextTemplatingEngineHost host;
            private StringBuilder template;
            protected List<String> generatedFileNames = new List<String>();

            public static Manager Create(ITextTemplatingEngineHost host, StringBuilder template)
            {
                return (host is IServiceProvider) ? new VSManager(host, template) : new Manager(host, template);
            }

            public void StartNewFile(String name)
            {
                if (name == null)
                    throw new ArgumentNullException("name");
                CurrentBlock = new Block { Name = name };
            }

            public void StartFooter(bool includeInDefault = true)
            {
                CurrentBlock = footer;
                footer.IncludeInDefault = includeInDefault;
            }

            public void StartHeader(bool includeInDefault = true)
            {
                CurrentBlock = header;
                header.IncludeInDefault = includeInDefault;
            }

            public void EndBlock()
            {
                if (CurrentBlock == null)
                    return;
                CurrentBlock.Length = template.Length - CurrentBlock.Start;
                if (CurrentBlock != header && CurrentBlock != footer)
                    files.Add(CurrentBlock);
                currentBlock = null;
            }

            public virtual void Process(bool split, bool sync = true)
            {
                if (split)
                {
                    EndBlock();
                    String headerText = template.ToString(header.Start, header.Length);
                    String footerText = template.ToString(footer.Start, footer.Length);
                    //String outputPath = Path.GetDirectoryName(host.TemplateFile); 
                    files.Reverse();
                    if (!footer.IncludeInDefault)
                        template.Remove(footer.Start, footer.Length);
                    foreach (Block block in files)
                    {
                        String fileName = Path.Combine(Tools.GeneratePath + "\\Entity\\", block.Name);
                        String content = headerText + template.ToString(block.Start, block.Length) + footerText;
                        generatedFileNames.Add(fileName);
                        CreateFile(fileName, content);
                        Tools.LogAction("生成路径：" + fileName, EnumCommon.LogEnum.EntityLog);
                        template.Remove(block.Start, block.Length);
                    }
                    if (!header.IncludeInDefault)
                        template.Remove(header.Start, header.Length);
                }
            }

            protected virtual void CreateFile(String fileName, String content)
            {
                var path1 = System.IO.Path.GetDirectoryName(fileName);
                if (!Directory.Exists(path1))
                {
                    if (path1 != null)
                        Directory.CreateDirectory(path1);
                }
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
                if (IsFileContentDifferent(fileName, content))
                    File.WriteAllText(fileName, content);
            }

            public virtual String GetCustomToolNamespace(String fileName)
            {
                return null;
            }

            public virtual String DefaultProjectNamespace
            {
                get { return null; }
            }

            protected bool IsFileContentDifferent(String fileName, String newContent)
            {
                return !(File.Exists(fileName) && File.ReadAllText(fileName) == newContent);
            }

            private Manager(ITextTemplatingEngineHost host, StringBuilder template)
            {
                this.host = host;
                this.template = template;
            }

            private Block CurrentBlock
            {
                get { return currentBlock; }
                set
                {
                    if (CurrentBlock != null)
                        EndBlock();
                    if (value != null)
                        value.Start = template.Length;
                    currentBlock = value;
                }
            }

            private class VSManager : Manager
            {
                private EnvDTE.ProjectItem templateProjectItem;
                private EnvDTE.DTE dte;
                private Action<String> checkOutAction;
                private Action<IEnumerable<String>> projectSyncAction;

                public override String DefaultProjectNamespace
                {
                    get
                    {
                        return templateProjectItem.ContainingProject.Properties.Item("DefaultNamespace").Value.ToString();
                    }
                }

                public override String GetCustomToolNamespace(string fileName)
                {
                    return dte.Solution.FindProjectItem(fileName).Properties.Item("CustomToolNamespace").Value.ToString();
                }

                public override void Process(bool split, bool sync)
                {
                    if (templateProjectItem.ProjectItems == null)
                        return;
                    base.Process(split, sync);
                    if (sync)
                        projectSyncAction.EndInvoke(projectSyncAction.BeginInvoke(generatedFileNames, null, null));
                }

                protected override void CreateFile(String fileName, String content)
                {
                    if (IsFileContentDifferent(fileName, content))
                    {
                        CheckoutFileIfRequired(fileName);
                        File.WriteAllText(fileName, content);
                    }
                }

                internal VSManager(ITextTemplatingEngineHost host, StringBuilder template)
                    : base(host, template)
                {
                    var hostServiceProvider = (IServiceProvider)host;
                    if (hostServiceProvider == null)
                        throw new ArgumentNullException("Could not obtain IServiceProvider");
                    dte = (EnvDTE.DTE)hostServiceProvider.GetService(typeof(EnvDTE.DTE));
                    if (dte == null)
                        throw new ArgumentNullException("Could not obtain DTE from host");
                    templateProjectItem = dte.Solution.FindProjectItem(host.TemplateFile);
                    checkOutAction = (String fileName) => dte.SourceControl.CheckOutItem(fileName);
                    projectSyncAction = (IEnumerable<String> keepFileNames) => ProjectSync(templateProjectItem, keepFileNames);
                }

                private static void ProjectSync(EnvDTE.ProjectItem templateProjectItem, IEnumerable<String> keepFileNames)
                {
                    var keepFileNameSet = new HashSet<String>(keepFileNames);
                    var projectFiles = new Dictionary<String, EnvDTE.ProjectItem>();
                    var originalFilePrefix = Path.GetFileNameWithoutExtension(templateProjectItem.get_FileNames(0)) + ".";
                    foreach (EnvDTE.ProjectItem projectItem in templateProjectItem.ProjectItems)
                        projectFiles.Add(projectItem.get_FileNames(0), projectItem);

                    // Remove unused items from the project
                    foreach (var pair in projectFiles)
                        if (!keepFileNames.Contains(pair.Key) && !(Path.GetFileNameWithoutExtension(pair.Key) + ".").StartsWith(originalFilePrefix))
                            pair.Value.Delete();

                    // Add missing files to the project
                    foreach (String fileName in keepFileNameSet)
                        if (!projectFiles.ContainsKey(fileName))
                            templateProjectItem.ProjectItems.AddFromFile(fileName);
                }

                private void CheckoutFileIfRequired(String fileName)
                {
                    var sc = dte.SourceControl;
                    if (sc != null && sc.IsItemUnderSCC(fileName) && !sc.IsItemCheckedOut(fileName))
                        checkOutAction.EndInvoke(checkOutAction.BeginInvoke(fileName, null, null));
                }
            }
        }
    }
}
