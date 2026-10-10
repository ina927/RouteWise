using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RouteWise.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    NormalizedUsername = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Journeys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    TravelMode = table.Column<string>(type: "TEXT", nullable: false),
                    Objective = table.Column<string>(type: "TEXT", nullable: false),
                    EndMode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Journeys_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    JourneyId = table.Column<string>(type: "TEXT", nullable: false),
                    SenderId = table.Column<string>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RemovedFromShared = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_Journeys_JourneyId",
                        column: x => x.JourneyId,
                        principalTable: "Journeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JourneyPoints",
                columns: table => new
                {
                    JourneyId = table.Column<string>(type: "TEXT", nullable: false),
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaceId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    PlannedTime = table.Column<TimeOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyPoints", x => new { x.JourneyId, x.Id });
                    table.ForeignKey(
                        name: "FK_JourneyPoints_Journeys_JourneyId",
                        column: x => x.JourneyId,
                        principalTable: "Journeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JourneySegments",
                columns: table => new
                {
                    JourneyId = table.Column<string>(type: "TEXT", nullable: false),
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    FromPointId = table.Column<string>(type: "TEXT", nullable: false),
                    ToPointId = table.Column<string>(type: "TEXT", nullable: false),
                    DistanceMeters = table.Column<double>(type: "REAL", nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneySegments", x => new { x.JourneyId, x.Id });
                    table.ForeignKey(
                        name: "FK_JourneySegments_JourneyPoints_JourneyId_FromPointId",
                        columns: x => new { x.JourneyId, x.FromPointId },
                        principalTable: "JourneyPoints",
                        principalColumns: new[] { "JourneyId", "Id" });
                    table.ForeignKey(
                        name: "FK_JourneySegments_JourneyPoints_JourneyId_ToPointId",
                        columns: x => new { x.JourneyId, x.ToPointId },
                        principalTable: "JourneyPoints",
                        principalColumns: new[] { "JourneyId", "Id" });
                    table.ForeignKey(
                        name: "FK_JourneySegments_Journeys_JourneyId",
                        column: x => x.JourneyId,
                        principalTable: "Journeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_JourneyId",
                table: "Invitations",
                column: "JourneyId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RecipientId",
                table: "Invitations",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SenderId",
                table: "Invitations",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_JourneyPoints_JourneyId_Position",
                table: "JourneyPoints",
                columns: new[] { "JourneyId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Journeys_OwnerId",
                table: "Journeys",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_JourneySegments_JourneyId_FromPointId",
                table: "JourneySegments",
                columns: new[] { "JourneyId", "FromPointId" });

            migrationBuilder.CreateIndex(
                name: "IX_JourneySegments_JourneyId_Position",
                table: "JourneySegments",
                columns: new[] { "JourneyId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_JourneySegments_JourneyId_ToPointId",
                table: "JourneySegments",
                columns: new[] { "JourneyId", "ToPointId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "JourneySegments");

            migrationBuilder.DropTable(
                name: "JourneyPoints");

            migrationBuilder.DropTable(
                name: "Journeys");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
