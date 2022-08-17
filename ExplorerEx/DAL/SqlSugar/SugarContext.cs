using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents.DocumentStructures;
using ExplorerEx.DAL.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X509.Qualified;
using SqlSugar;

namespace ExplorerEx.DAL.SqlSugar
{
    public abstract class SugarContext : ILazyInitialize 
    {
        private readonly string dbPath;
        protected SqlSugarClient ConnectionClient;
        
        protected SugarContext(string databaseFilename)
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "SqlSugar");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            dbPath = Path.Combine(path, databaseFilename);
            ConnectionClient = new SqlSugarClient(new ConnectionConfig
            {
                DbType = DbType.Sqlite,
                ConnectionString = @"Data Source=" + dbPath + ";",
                InitKeyType = InitKeyType.Attribute,
                IsAutoCloseConnection = true
            });
        }


        public virtual Task LoadDataBase()
        {
            return Task.Run(() =>
            {
                ConnectionClient.DbMaintenance.CreateDatabase();
            });
        }

    }

}

