
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateLibrary.Migrations
{
    public partial class AddCatalogTranslations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Book translations
            migrationBuilder.CreateTable(
                name: "BookTranslations",
                columns: table => new
                {
                    BookTranslationId = table.Column<int>(
                        type: "int",
                        nullable: false)
                        .Annotation(
                            "MySql:ValueGenerationStrategy",
                            MySqlValueGenerationStrategy.IdentityColumn),

                    BookId = table.Column<int>(
                        type: "int",
                        nullable: false),

                    LanguageCode = table.Column<string>(
                        type: "varchar(5)",
                        maxLength: 5,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Title = table.Column<string>(
                        type: "varchar(255)",
                        maxLength: 255,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Description = table.Column<string>(
                        type: "longtext",
                        nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_BookTranslations",
                        x => x.BookTranslationId);

                    table.ForeignKey(
                        name: "FK_BookTranslations_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Genre translations
            migrationBuilder.CreateTable(
                name: "GenreTranslations",
                columns: table => new
                {
                    GenreTranslationId = table.Column<int>(
                        type: "int",
                        nullable: false)
                        .Annotation(
                            "MySql:ValueGenerationStrategy",
                            MySqlValueGenerationStrategy.IdentityColumn),

                    GenreId = table.Column<int>(
                        type: "int",
                        nullable: false),

                    LanguageCode = table.Column<string>(
                        type: "varchar(5)",
                        maxLength: 5,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Name = table.Column<string>(
                        type: "varchar(100)",
                        maxLength: 100,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Description = table.Column<string>(
                        type: "longtext",
                        nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_GenreTranslations",
                        x => x.GenreTranslationId);

                    table.ForeignKey(
                        name: "FK_GenreTranslations_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "GenreId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Author translations
            migrationBuilder.CreateTable(
                name: "AuthorTranslations",
                columns: table => new
                {
                    AuthorTranslationId = table.Column<int>(
                        type: "int",
                        nullable: false)
                        .Annotation(
                            "MySql:ValueGenerationStrategy",
                            MySqlValueGenerationStrategy.IdentityColumn),

                    AuthorId = table.Column<int>(
                        type: "int",
                        nullable: false),

                    LanguageCode = table.Column<string>(
                        type: "varchar(5)",
                        maxLength: 5,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    FirstName = table.Column<string>(
                        type: "varchar(100)",
                        maxLength: 100,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    LastName = table.Column<string>(
                        type: "varchar(100)",
                        maxLength: 100,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Biography = table.Column<string>(
                        type: "longtext",
                        nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_AuthorTranslations",
                        x => x.AuthorTranslationId);

                    table.ForeignKey(
                        name: "FK_AuthorTranslations_Authors_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Authors",
                        principalColumn: "AuthorId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Unique indexes: one translation per language and entity
            migrationBuilder.CreateIndex(
                name: "IX_BookTranslations_BookId_LanguageCode",
                table: "BookTranslations",
                columns: new[] { "BookId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenreTranslations_GenreId_LanguageCode",
                table: "GenreTranslations",
                columns: new[] { "GenreId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthorTranslations_AuthorId_LanguageCode",
                table: "AuthorTranslations",
                columns: new[] { "AuthorId", "LanguageCode" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookTranslations");

            migrationBuilder.DropTable(
                name: "GenreTranslations");

            migrationBuilder.DropTable(
                name: "AuthorTranslations");
        }
    }
}
