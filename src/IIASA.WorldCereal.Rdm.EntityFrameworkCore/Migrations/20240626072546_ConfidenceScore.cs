using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIASA.WorldCereal.Rdm.Migrations
{
    /// <inheritdoc />
    public partial class ConfidenceScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConfidenceCropType",
                table: "Rdm_CollectionMetadata",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConfidenceIrrigationType",
                table: "Rdm_CollectionMetadata",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConfidenceLandCover",
                table: "Rdm_CollectionMetadata",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfidenceCropType",
                table: "Rdm_CollectionMetadata");

            migrationBuilder.DropColumn(
                name: "ConfidenceIrrigationType",
                table: "Rdm_CollectionMetadata");

            migrationBuilder.DropColumn(
                name: "ConfidenceLandCover",
                table: "Rdm_CollectionMetadata");
        }
    }
}
