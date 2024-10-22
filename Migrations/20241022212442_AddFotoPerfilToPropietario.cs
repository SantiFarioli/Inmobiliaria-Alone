using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inmobiliaria_Alone.Migrations
{
    /// <inheritdoc />
    public partial class AddFotoPerfilToPropietario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoPerfil",
                table: "Propietarios",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoPerfil",
                table: "Propietarios");
        }
    }
}
