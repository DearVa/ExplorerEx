using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorerEx.DAL.Interfaces
{
    public interface ILazyInitialize
    {
        Task LoadDataBase();
    }

    public interface IDbBehavior
    {
        void Save();

        Task SaveAsync();
    }
}
