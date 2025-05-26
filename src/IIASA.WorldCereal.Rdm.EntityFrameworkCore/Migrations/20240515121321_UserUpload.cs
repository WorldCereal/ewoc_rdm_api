using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIASA.WorldCereal.Rdm.Migrations
{
    /// <inheritdoc />
    public partial class UserUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfidenceCropType",
                table: "Rdm_UserDatasets");

            migrationBuilder.DropColumn(
                name: "ConfidenceIrrigationType",
                table: "Rdm_UserDatasets");

            migrationBuilder.DropColumn(
                name: "ConfidenceLandCover",
                table: "Rdm_UserDatasets");

            migrationBuilder.DropColumn(
                name: "TypeOfObservationMethod",
                table: "Rdm_UserDatasets");

            migrationBuilder.DropColumn(
                name: "ConfidenceCropType",
                table: "Rdm_CollectionMetadata");

            migrationBuilder.DropColumn(
                name: "ConfidenceIrrigationType",
                table: "Rdm_CollectionMetadata");

            migrationBuilder.DropColumn(
                name: "ConfidenceLandCover",
                table: "Rdm_CollectionMetadata");

            migrationBuilder.DropColumn(
                name: "TypeOfObservationMethod",
                table: "Rdm_CollectionMetadata");

            migrationBuilder.AddColumn<string[]>(
                name: "Columns",
                table: "Rdm_UserDatasets",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "DatasetExtractedPath",
                table: "Rdm_UserDatasets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EwocCodeCol",
                table: "Rdm_UserDatasets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IrrCol",
                table: "Rdm_UserDatasets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ValDateCol",
                table: "Rdm_UserDatasets",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Columns",
                table: "Rdm_UserDatasets");

            migrationBuilder.DropColumn(
                name: "DatasetExtractedPath",
                table: "Rdm_UserDatasets");

            migrationBuilder.DropColumn(
                name: "EwocCodeCol",
                table: "Rdm_UserDatasets");

            migrationBuilder.DropColumn(
                name: "IrrCol",
                table: "Rdm_UserDatasets");

            migrationBuilder.DropColumn(
                name: "ValDateCol",
                table: "Rdm_UserDatasets");

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceCropType",
                table: "Rdm_UserDatasets",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceIrrigationType",
                table: "Rdm_UserDatasets",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceLandCover",
                table: "Rdm_UserDatasets",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TypeOfObservationMethod",
                table: "Rdm_UserDatasets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceCropType",
                table: "Rdm_CollectionMetadata",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceIrrigationType",
                table: "Rdm_CollectionMetadata",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceLandCover",
                table: "Rdm_CollectionMetadata",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TypeOfObservationMethod",
                table: "Rdm_CollectionMetadata",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
