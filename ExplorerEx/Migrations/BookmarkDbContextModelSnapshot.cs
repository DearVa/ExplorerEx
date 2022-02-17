﻿// <auto-generated />
using ExplorerEx.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ExplorerEx.Migrations
{
    [DbContext(typeof(BookmarkDbContext))]
    partial class BookmarkDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.2");

            modelBuilder.Entity("ExplorerEx.Model.BookmarkCategory", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsExpanded")
                        .HasColumnType("INTEGER");

                    b.HasKey("Name");

                    b.ToTable("BookmarkCategoryDbSet");
                });

            modelBuilder.Entity("ExplorerEx.Model.BookmarkItem", b =>
                {
                    b.Property<string>("FullPath")
                        .HasColumnType("TEXT");

                    b.Property<string>("CategoryForeignKey")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("FullPath");

                    b.HasIndex("CategoryForeignKey");

                    b.ToTable("BookmarkDbSet");
                });

            modelBuilder.Entity("ExplorerEx.Model.BookmarkItem", b =>
                {
                    b.HasOne("ExplorerEx.Model.BookmarkCategory", "Category")
                        .WithMany("Children")
                        .HasForeignKey("CategoryForeignKey");

                    b.Navigation("Category");
                });

            modelBuilder.Entity("ExplorerEx.Model.BookmarkCategory", b =>
                {
                    b.Navigation("Children");
                });
#pragma warning restore 612, 618
        }
    }
}
