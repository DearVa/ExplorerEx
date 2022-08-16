using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.DAL.Interfaces;
using ExplorerEx.Model;

namespace ExplorerEx.DAL.SqlSugar
{
    public class FileViewSugarContext : IFileViewDbContext
    {
        public void Add(FileView item)
        {
            throw new NotImplementedException();
        }

        public Task AddAsync(FileView item)
        {
            throw new NotImplementedException();
        }

        public ISet<FileView> GetFileViews()
        {
            throw new NotImplementedException();
        }

        public Task LoadDataBase()
        {
            throw new NotImplementedException();
        }

        public void SaveChanges()
        {
            throw new NotImplementedException();
        }

        public Task SaveChangesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
