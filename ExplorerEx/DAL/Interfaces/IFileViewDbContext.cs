using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.Model;

namespace ExplorerEx.DAL.Interfaces
{
    public interface IFileViewDbContext
    {
        Task LoadDataBase();

        ISet<FileView> GetFileViews();

        void Add(FileView item);
        Task AddAsync(FileView item);

        void SaveChanges();
        Task SaveChangesAsync();
    }
}
