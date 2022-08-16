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
    public class SugarContext : ILazyInitialize
    {
        private readonly string dbPath;
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

        public virtual Task LoadDataBase()
        {
            return Task.Run(() =>
            {
                ConnectionClient = new SqlSugarClient(new ConnectionConfig
                {
                    DbType = DbType.Sqlite,
                    ConnectionString = @"Data Source=" + dbPath + ";",
                    InitKeyType = InitKeyType.Attribute
                });
                ConnectionClient.DbMaintenance.CreateDatabase();
            });
        }
    }
}
