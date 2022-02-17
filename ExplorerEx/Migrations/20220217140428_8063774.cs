using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExplorerEx.Migrations
{
    public partial class _8063774 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookmarkCategoryDbSet",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsExpanded = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkCategoryDbSet", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "BookmarkDbSet",
                columns: table => new
                {
                    FullPath = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryForeignKey = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkDbSet", x => x.FullPath);
                    table.ForeignKey(
                        name: "FK_BookmarkDbSet_BookmarkCategoryDbSet_CategoryForeignKey",
                        column: x => x.CategoryForeignKey,
                        principalTable: "BookmarkCategoryDbSet",
                        principalColumn: "Name");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookmarkDbSet_CategoryForeignKey",
                table: "BookmarkDbSet",
                column: "CategoryForeignKey");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookmarkDbSet");

            migrationBuilder.DropTable(
                name: "BookmarkCategoryDbSet");
        }
    }
}
