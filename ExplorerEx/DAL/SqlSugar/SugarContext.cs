using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.DAL.Interfaces;
using SqlSugar;

namespace ExplorerEx.DAL.SqlSugar
{
    internal class SugarContext : ILazyInitialize
    {
        private readonly string dbPath;
        private ConnectionConfig? Config;
        protected SqlSugarClient? ConnectionClient;

        protected SugarContext(string DatabaseFilename)
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            dbPath = Path.Combine(path, DatabaseFilename);

        }

        public Task LoadDataBase()
        {
            return Task.Run(() =>
            {
                Config = new ConnectionConfig
                {
                    DbType = DbType.Sqlite,
                    ConnectionString = @"Data Source=" + dbPath + ";Version=3",
                    InitKeyType = InitKeyType.Attribute
                };
                ConnectionClient = new SqlSugarClient(Config);
            });
        }
    }
}
