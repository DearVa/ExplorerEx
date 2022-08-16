using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.DAL.Interfaces;
using ExplorerEx.Model;

namespace ExplorerEx.DAL.SqlSugar
{
    public class FileViewSugarContext : SugarContext, IFileViewDbContext
    {
        public FileViewSugarContext():base("FileViews.db")
        {
            
        }
        public void Add(FileView item)
        {
            ConnectionClient.Insertable<FileView>(item).ExecuteCommand();
        }

        public Task AddAsync(FileView item)
        {
            return ConnectionClient.Insertable<FileView>(item).ExecuteCommandAsync();
        }

        public ISet<FileView> GetFileViews()
        {
            return ConnectionClient.Queryable<FileView>().ToArray().ToHashSet();
        }

        public void Save()
        {
       
        }

        public Task SaveAsync()
        {
            return Task.CompletedTask;
        }
    }
}
