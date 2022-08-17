using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExplorerEx.DAL.Interfaces;
using ExplorerEx.Model;

namespace ExplorerEx.DAL.SqlSugar
{
    public class FileViewSugarContext : SugarContext, IFileViewDbContext
    {
        private readonly SugarCache<FileView> fileSugarCache;

        public FileViewSugarContext():base("FileViews.db")
        {
            fileSugarCache = new SugarCache<FileView>(ConnectionClient);
        }

        public override Task LoadDataBase()
        {
            return Task.Run(async() => {
                await base.LoadDataBase();
                ConnectionClient.CodeFirst.InitTables<FileView>();
                fileSugarCache.LoadDatabase();
            });
        }

        public void Add(FileView item)
        {
            fileSugarCache.Add(item);
        }

        public Task AddAsync(FileView item)
        {
            return Task.Run(()=>Add(item));
        }

        public FileView? FindFirstOrDefault(Func<FileView, bool> match)
        {
            return fileSugarCache.Find(match);
        }

        public ISet<FileView> GetFileViews()
        {
            return fileSugarCache.QueryAll().ToHashSet();
        }

        public void Save()
        {
            fileSugarCache.Save();
        }

        public Task SaveAsync()
        {
            return Task.Run(() => Save());
        }
    }
}
