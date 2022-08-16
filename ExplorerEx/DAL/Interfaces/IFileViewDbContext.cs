using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.Model;

namespace ExplorerEx.DAL.Interfaces
{
    public interface IFileViewDbContext : ILazyInitialize ,IDbBehavior
    {

        ISet<FileView> GetFileViews();

        void Add(FileView item);
        Task AddAsync(FileView item);

    }
}
