using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inmobiliaria_Alone.Migrations
{
    /// <inheritdoc />
    public partial class AddFotoToInmueble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Foto",
                table: "Inmuebles",
                type: "varchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Foto",
                table: "Inmuebles");
        }
    }
}
